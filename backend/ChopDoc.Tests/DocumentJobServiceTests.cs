using ChopDoc.Application.DTOs;
using ChopDoc.Application.Options;
using ChopDoc.Application.Services;
using ChopDoc.Domain.Abstractions;
using ChopDoc.Domain.Entities;
using ChopDoc.Domain.Enums;
using ChopDoc.Infrastructure.Conversion;
using ChopDoc.Infrastructure.Export;
using ChopDoc.Infrastructure.Processing;
using ChopDoc.Tests.Helpers;
using Microsoft.Extensions.Options;

namespace ChopDoc.Tests;

/// <summary>
/// Pipeline tests over the real converter, splitter, validator, and exporter, with persistence
/// and storage faked out. They cover how the stages fit together rather than each stage alone.
/// </summary>
public class DocumentJobServiceTests
{
    /// <summary>
    /// The size limit applies to the delivered file. This document's HTML intermediate is over
    /// the limit while its plain-text export is comfortably under, so it must not be split.
    /// </summary>
    [Fact]
    public async Task SubmitAsync_WhenExportedOutputFitsLimit_ProducesSinglePart()
    {
        var pdf = PdfFixtures.CreateTextPdf(BuildPages(12));
        var (limitMb, intermediateBytes, exportedBytes) = await SizeLimitBetweenIntermediateAndExport(pdf);
        Assert.True(exportedBytes < intermediateBytes);

        var result = await Submit(pdf, "PlainText", limitMb);

        Assert.Equal(nameof(JobStatus.Completed), result.Status);
        Assert.Single(result.Parts);
        Assert.Equal("part 1 of 1", result.Parts[0].SequenceLabel);
    }

    /// <summary>The same document in a format that stays large does have to be split.</summary>
    [Fact]
    public async Task SubmitAsync_WhenExportedOutputExceedsLimit_SplitsIntoSequencedParts()
    {
        var pdf = PdfFixtures.CreateTextPdf(BuildPages(12));
        var (limitMb, _, _) = await SizeLimitBetweenIntermediateAndExport(pdf);

        var result = await Submit(pdf, "Html", limitMb);

        Assert.Equal(nameof(JobStatus.Completed), result.Status);
        Assert.True(result.Parts.Count > 1);
        Assert.Equal(
            Enumerable.Range(1, result.Parts.Count),
            result.Parts.Select(p => p.PartNumber));
        Assert.All(result.Parts, p => Assert.Equal(result.Parts.Count, p.TotalParts));

        // Every delivered file is within the limit that was validated.
        Assert.All(result.Parts, p => Assert.True(p.SizeBytes <= result.SizeLimitBytes));
    }

    [Fact]
    public async Task SubmitAsync_WhenPdfHasNoTextLayer_FailsWithReasonInHistory()
    {
        var result = await Submit(PdfFixtures.CreateNoTextPdf(), "Html", sizeLimitMb: 2);

        Assert.Equal(nameof(JobStatus.Failed), result.Status);
        Assert.Equal("SCANNED_DOCUMENT", result.ErrorCode);
        Assert.Empty(result.Parts);
        Assert.Contains(result.History, h => h.Status == nameof(JobStatus.Failed));
    }

    [Fact]
    public async Task SubmitAsync_WhenOutputFormatIsNotSupported_PersistsFailedJob()
    {
        var result = await Submit(PdfFixtures.CreateTextPdf("hello"), "Rtf", sizeLimitMb: 2);

        Assert.Equal(nameof(JobStatus.Failed), result.Status);
        Assert.Equal("UNSUPPORTED_OUTPUT_FORMAT", result.ErrorCode);
    }

    private static string[] BuildPages(int count) =>
        Enumerable.Range(1, count)
            .Select(i => $"Page {i} body text " + new string('x', 200))
            .ToArray();

    /// <summary>
    /// Picks a limit that sits between the HTML intermediate and the exported text, which is the
    /// only window where the two sizing rules disagree.
    /// </summary>
    private static async Task<(double LimitMb, long IntermediateBytes, long ExportedBytes)>
        SizeLimitBetweenIntermediateAndExport(byte[] pdf)
    {
        await using var stream = new MemoryStream(pdf);
        var converted = await new PdfDocumentConverter().ConvertPdfToHtmlAsync(stream);
        var exported = new HtmlOutputExporter()
            .Export(converted.Content, "doc", 1, 1, OutputFormat.PlainText)
            .Content.LongLength;

        var intermediate = converted.Content.LongLength;
        var midpoint = (intermediate + exported) / 2;

        return (midpoint / (1024.0 * 1024.0), intermediate, exported);
    }

    private static async Task<JobDetailDto> Submit(byte[] pdf, string outputFormat, double sizeLimitMb)
    {
        var service = new DocumentJobService(
            new InMemoryJobRepository(),
            new InMemoryFileStorage(),
            new PdfDocumentConverter(),
            new MarkedDocumentSplitter(),
            new DocumentOutputValidator(),
            new HtmlOutputExporter(),
            Options.Create(new DocumentProcessingOptions()));

        await using var stream = new MemoryStream(pdf);
        return await service.SubmitAsync(new SubmitJobRequest
        {
            FileStream = stream,
            FileName = "sample.pdf",
            OutputFormat = outputFormat,
            SizeLimitMb = sizeLimitMb
        });
    }

    private sealed class InMemoryJobRepository : IJobRepository
    {
        private readonly Dictionary<Guid, DocumentJob> _jobs = new();

        public Task AddAsync(DocumentJob job, CancellationToken cancellationToken = default)
        {
            _jobs[job.Id] = job;
            return Task.CompletedTask;
        }

        public Task<DocumentJob?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult(_jobs.TryGetValue(id, out var job) ? job : null);

        public Task<IReadOnlyList<DocumentJob>> ListAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<DocumentJob>>(_jobs.Values.ToList());

        public Task UpdateAsync(DocumentJob job, CancellationToken cancellationToken = default)
        {
            _jobs[job.Id] = job;
            return Task.CompletedTask;
        }
    }

    private sealed class InMemoryFileStorage : IFileStorage
    {
        private readonly Dictionary<string, byte[]> _files = new();

        public async Task<string> SaveAsync(
            Stream content,
            string relativeFolder,
            string fileName,
            CancellationToken cancellationToken = default)
        {
            using var buffer = new MemoryStream();
            await content.CopyToAsync(buffer, cancellationToken);
            return Store(relativeFolder, fileName, buffer.ToArray());
        }

        public Task<string> SaveAsync(
            byte[] content,
            string relativeFolder,
            string fileName,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(Store(relativeFolder, fileName, content));

        public Task<Stream> OpenReadAsync(string storedPath, CancellationToken cancellationToken = default) =>
            Task.FromResult<Stream>(new MemoryStream(_files[storedPath]));

        public Task<byte[]> ReadAllBytesAsync(string storedPath, CancellationToken cancellationToken = default) =>
            Task.FromResult(_files[storedPath]);

        private string Store(string relativeFolder, string fileName, byte[] content)
        {
            var path = $"{relativeFolder}/{fileName}";
            _files[path] = content;
            return path;
        }
    }
}
