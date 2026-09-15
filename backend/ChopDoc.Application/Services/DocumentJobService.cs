using ChopDoc.Application.DTOs;
using ChopDoc.Application.Mapping;
using ChopDoc.Application.Options;
using ChopDoc.Domain.Abstractions;
using ChopDoc.Domain.Entities;
using ChopDoc.Domain.Enums;
using ChopDoc.Domain.Exceptions;
using Microsoft.Extensions.Options;

namespace ChopDoc.Application.Services;

public sealed class DocumentJobService : IDocumentJobService
{
    private readonly IJobRepository _jobs;
    private readonly IFileStorage _files;
    private readonly IDocumentConverter _converter;
    private readonly IDocumentSplitter _splitter;
    private readonly IOutputValidator _validator;
    private readonly DocumentProcessingOptions _options;

    public DocumentJobService(
        IJobRepository jobs,
        IFileStorage files,
        IDocumentConverter converter,
        IDocumentSplitter splitter,
        IOutputValidator validator,
        IOptions<DocumentProcessingOptions> options)
    {
        _jobs = jobs;
        _files = files;
        _converter = converter;
        _splitter = splitter;
        _validator = validator;
        _options = options.Value;
    }

    public async Task<JobDetailDto> SubmitAsync(SubmitJobRequest request, CancellationToken cancellationToken = default)
    {
        if (request.FileStream is null || !request.FileStream.CanRead)
            throw new ArgumentException("A readable file stream is required.");

        if (string.IsNullOrWhiteSpace(request.FileName))
            throw new ArgumentException("File name is required.");

        var sizeLimitBytes = ToBytes(request.SizeLimitMb ?? _options.DefaultSizeLimitMb);
        var safeFileName = Path.GetFileName(request.FileName);
        var parsedFormat = TryParseOutputFormat(request.OutputFormat, out var outputFormat);

        var sourcePath = await _files.SaveAsync(
            request.FileStream,
            "sources",
            $"{Guid.NewGuid():N}_{safeFileName}",
            cancellationToken);

        // Persist every request (assessment 4.6), including intake failures.
        var job = new DocumentJob(safeFileName, sourcePath, outputFormat, sizeLimitBytes);
        await _jobs.AddAsync(job, cancellationToken);

        var extension = Path.GetExtension(safeFileName);
        if (!extension.Equals(".pdf", StringComparison.OrdinalIgnoreCase))
        {
            job.MarkFailed(new UnsupportedOrCorruptedDocumentException("Only PDF input is accepted."));
            await _jobs.UpdateAsync(job, cancellationToken);
            return await GetRequiredDetailAsync(job.Id, cancellationToken);
        }

        if (!parsedFormat || outputFormat == OutputFormat.Unspecified)
        {
            job.MarkFailed(new UnsupportedOutputFormatException(request.OutputFormat ?? "(null)"));
            await _jobs.UpdateAsync(job, cancellationToken);
            return await GetRequiredDetailAsync(job.Id, cancellationToken);
        }

        await ProcessPipelineAsync(job, cancellationToken);
        return await GetRequiredDetailAsync(job.Id, cancellationToken);
    }

    public async Task<JobDetailDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var job = await _jobs.GetByIdAsync(id, cancellationToken);
        return job is null ? null : JobMapping.ToDetail(job);
    }

    public async Task<IReadOnlyList<JobSummaryDto>> ListAsync(CancellationToken cancellationToken = default)
    {
        var jobs = await _jobs.ListAsync(cancellationToken);
        return jobs
            .OrderByDescending(j => j.CreatedAtUtc)
            .Select(JobMapping.ToSummary)
            .ToList();
    }

    public async Task<(Stream Content, string FileName, string ContentType)?> OpenPartAsync(
        Guid jobId,
        Guid partId,
        CancellationToken cancellationToken = default)
    {
        var job = await _jobs.GetByIdAsync(jobId, cancellationToken);
        var part = job?.Parts.FirstOrDefault(p => p.Id == partId);
        if (part is null)
            return null;

        var stream = await _files.OpenReadAsync(part.StoredPath, cancellationToken);
        var contentType = part.FileName.EndsWith(".html", StringComparison.OrdinalIgnoreCase)
            ? "text/html"
            : "application/octet-stream";

        return (stream, part.FileName, contentType);
    }

    private async Task ProcessPipelineAsync(DocumentJob job, CancellationToken cancellationToken)
    {
        try
        {
            job.MarkConverting();
            await _jobs.UpdateAsync(job, cancellationToken);

            await using var source = await _files.OpenReadAsync(job.StoredSourcePath, cancellationToken);
            var conversion = await _converter.ConvertAsync(source, job.RequestedOutputFormat, cancellationToken);

            job.MarkSplitting();
            await _jobs.UpdateAsync(job, cancellationToken);

            var baseName = Path.GetFileNameWithoutExtension(job.OriginalFileName);
            var splitParts = _splitter.SplitIfNeeded(
                conversion.Content,
                baseName,
                conversion.FileExtension,
                job.SizeLimitBytes);

            job.MarkValidating();
            await _jobs.UpdateAsync(job, cancellationToken);

            var validation = _validator.Validate(splitParts, job.SizeLimitBytes);
            if (!validation.IsValid)
            {
                job.MarkNeedsReview(new OutputValidationException(validation.FailureReason ?? "Unknown validation failure."));
                await _jobs.UpdateAsync(job, cancellationToken);
                return;
            }

            var persistedParts = new List<DocumentPart>();
            foreach (var part in splitParts)
            {
                var path = await _files.SaveAsync(
                    part.Content,
                    $"jobs/{job.Id:N}/parts",
                    part.FileName,
                    cancellationToken);

                persistedParts.Add(new DocumentPart(
                    job.Id,
                    part.PartNumber,
                    part.TotalParts,
                    path,
                    part.Content.LongLength,
                    part.FileName));
            }

            job.ReplaceParts(persistedParts);
            job.MarkCompleted();
            await _jobs.UpdateAsync(job, cancellationToken);
        }
        catch (UnsplittableContentException ex)
        {
            job.MarkNeedsReview(ex);
            await _jobs.UpdateAsync(job, cancellationToken);
        }
        catch (DomainException ex)
        {
            job.MarkFailed(ex);
            await _jobs.UpdateAsync(job, cancellationToken);
        }
        catch (Exception ex)
        {
            job.MarkFailed("UNEXPECTED_ERROR", ex.Message);
            await _jobs.UpdateAsync(job, cancellationToken);
        }
    }

    private async Task<JobDetailDto> GetRequiredDetailAsync(Guid id, CancellationToken cancellationToken)
    {
        var refreshed = await _jobs.GetByIdAsync(id, cancellationToken)
            ?? throw new InvalidOperationException("Job disappeared after processing.");
        return JobMapping.ToDetail(refreshed);
    }

    private static bool TryParseOutputFormat(string? value, out OutputFormat format)
    {
        if (Enum.TryParse(value, ignoreCase: true, out format)
            && Enum.IsDefined(format)
            && format != OutputFormat.Unspecified)
        {
            return true;
        }

        format = OutputFormat.Unspecified;
        return false;
    }

    private static long ToBytes(double sizeLimitMb)
    {
        if (sizeLimitMb <= 0)
            throw new ArgumentOutOfRangeException(nameof(sizeLimitMb), "Size limit must be greater than zero.");

        return (long)(sizeLimitMb * 1024 * 1024);
    }
}
