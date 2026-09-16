using ChopDoc.Application.DTOs;
using ChopDoc.Application.Mapping;
using ChopDoc.Application.Options;
using ChopDoc.Domain.Abstractions;
using ChopDoc.Domain.Entities;
using ChopDoc.Domain.Enums;
using ChopDoc.Domain.Exceptions;
using ChopDoc.Domain.Models;
using Microsoft.Extensions.Options;

namespace ChopDoc.Application.Services;

public sealed class DocumentJobService : IDocumentJobService
{
    private readonly IJobRepository _jobs;
    private readonly IFileStorage _files;
    private readonly IDocumentConverter _converter;
    private readonly IDocumentSplitter _splitter;
    private readonly IOutputValidator _validator;
    private readonly IOutputExporter _exporter;
    private readonly DocumentProcessingOptions _options;

    public DocumentJobService(
        IJobRepository jobs,
        IFileStorage files,
        IDocumentConverter converter,
        IDocumentSplitter splitter,
        IOutputValidator validator,
        IOutputExporter exporter,
        IOptions<DocumentProcessingOptions> options)
    {
        _jobs = jobs;
        _files = files;
        _converter = converter;
        _splitter = splitter;
        _validator = validator;
        _exporter = exporter;
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

        var job = new DocumentJob(safeFileName, sourcePath, outputFormat, sizeLimitBytes);
        await _jobs.AddAsync(job, cancellationToken);

        var extension = Path.GetExtension(safeFileName);
        if (!extension.Equals(".pdf", StringComparison.OrdinalIgnoreCase))
        {
            job.MarkFailed(new UnsupportedOrCorruptedDocumentException("Only PDF input is accepted."));
            await _jobs.UpdateAsync(job, cancellationToken);
            return await GetRequiredDetailAsync(job.Id, cancellationToken);
        }

        if (!parsedFormat || outputFormat == OutputFormat.Unspecified || !_exporter.Supports(outputFormat))
        {
            job.MarkFailed(new UnsupportedOutputFormatException(request.OutputFormat ?? outputFormat.ToString()));
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
        return (stream, part.FileName, GuessContentType(part.FileName));
    }

    private static string GuessContentType(string fileName)
    {
        var ext = Path.GetExtension(fileName).ToLowerInvariant();
        return ext switch
        {
            ".html" or ".htm" => "text/html",
            ".md" => "text/markdown",
            ".txt" => "text/plain",
            ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            ".xml" => "application/xml",
            ".json" => "application/json",
            ".csv" => "text/csv",
            _ => "application/octet-stream"
        };
    }

    private async Task ProcessPipelineAsync(DocumentJob job, CancellationToken cancellationToken)
    {
        try
        {
            // Keep status/history in memory; one SaveChanges at the end avoids EF graph churn.
            job.MarkConverting();

            await using var source = await _files.OpenReadAsync(job.StoredSourcePath, cancellationToken);
            var converted = await _converter.ConvertPdfToHtmlAsync(source, cancellationToken);

            foreach (var warning in converted.Warnings)
                job.AddWarning(warning);

            job.MarkSplitting();

            var baseName = Path.GetFileNameWithoutExtension(job.OriginalFileName);
            var intermediateParts = _splitter.SplitIfNeeded(
                converted.Content,
                baseName,
                ".html",
                job.SizeLimitBytes,
                MeasureExportedSize(baseName, job.RequestedOutputFormat));

            // Export into memory before validating: the limit applies to the artifact that is
            // handed off, and nothing reaches storage until validation has passed.
            var exportedParts = ExportParts(intermediateParts, baseName, job.RequestedOutputFormat);

            job.MarkValidating();

            var validation = Validate(intermediateParts, exportedParts, converted.PageMarkers, job.SizeLimitBytes);
            if (!validation.IsValid)
            {
                job.MarkNeedsReview(new OutputValidationException(validation.FailureReason ?? "Unknown validation failure."));
                await _jobs.UpdateAsync(job, cancellationToken);
                return;
            }

            var persistedParts = new List<DocumentPart>(exportedParts.Count);
            foreach (var part in exportedParts)
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
        catch (OperationCanceledException)
        {
            // The caller is gone, so record the outcome on a token that is still usable rather
            // than leaving the job stuck mid-pipeline.
            job.MarkFailed("REQUEST_CANCELLED", "The request was cancelled before processing finished.");
            await _jobs.UpdateAsync(job, CancellationToken.None);
        }
        catch (Exception ex)
        {
            // Exception details belong in the logs, not in a field the API hands to clients.
            job.MarkFailed("UNEXPECTED_ERROR", $"Processing failed unexpectedly ({ex.GetType().Name}).");
            await _jobs.UpdateAsync(job, cancellationToken);
        }
    }

    /// <summary>Lets the splitter size a candidate part in the format the job asked for.</summary>
    private ExportedSizeProbe MeasureExportedSize(string baseName, OutputFormat format) =>
        htmlContent => _exporter.Export(htmlContent, baseName, 1, 1, format).Content.LongLength;

    private List<ExportedPart> ExportParts(
        IReadOnlyList<SplitPartContent> intermediateParts,
        string baseName,
        OutputFormat format)
    {
        var exported = new List<ExportedPart>(intermediateParts.Count);
        foreach (var part in intermediateParts)
        {
            var result = _exporter.Export(
                part.Content,
                baseName,
                part.PartNumber,
                part.TotalParts,
                format);

            exported.Add(new ExportedPart(
                part.PartNumber,
                part.TotalParts,
                result.Content,
                result.FileName));
        }

        return exported;
    }

    /// <summary>
    /// Structure and completeness are checked on the intermediate, where the section markers
    /// live; size and sequence are checked on the exported parts, which are what get delivered.
    /// </summary>
    private ValidationResult Validate(
        IReadOnlyList<SplitPartContent> intermediateParts,
        IReadOnlyList<ExportedPart> exportedParts,
        IReadOnlyCollection<string> expectedMarkers,
        long sizeLimitBytes)
    {
        var structure = _validator.ValidateStructure(intermediateParts, expectedMarkers);
        return structure.IsValid
            ? _validator.ValidateExportedParts(exportedParts, sizeLimitBytes)
            : structure;
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
