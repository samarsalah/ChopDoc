using ChopDoc.Domain.Enums;
using ChopDoc.Domain.Exceptions;
using ChopDoc.Infrastructure.Conversion;
using ChopDoc.Tests.Helpers;

namespace ChopDoc.Tests;

public class PdfToHtmlConverterTests
{
    private readonly PdfToHtmlConverter _sut = new();

    [Fact]
    public async Task ConvertAsync_TextPdf_ProducesHtmlWithMarkers()
    {
        await using var stream = new MemoryStream(PdfFixtures.CreateTextPdf("Hello ChopDoc", "Page two text"));

        var result = await _sut.ConvertAsync(stream, OutputFormat.Html);

        Assert.Equal(OutputFormat.Html, result.Format);
        Assert.Equal(".html", result.FileExtension);
        Assert.Equal(2, result.PageCount);
        Assert.True(result.Content.Length > 0);

        var html = System.Text.Encoding.UTF8.GetString(result.Content);
        Assert.Contains("data-chopdoc-marker=\"page-1\"", html);
        Assert.Contains("data-chopdoc-marker=\"page-2\"", html);
        Assert.Contains("Hello ChopDoc", html);
        Assert.Contains("Page two text", html);
    }

    [Fact]
    public async Task ConvertAsync_NoTextLayer_ThrowsScannedDocumentException()
    {
        await using var stream = new MemoryStream(PdfFixtures.CreateNoTextPdf());

        var ex = await Assert.ThrowsAsync<ScannedDocumentException>(() =>
            _sut.ConvertAsync(stream, OutputFormat.Html));

        Assert.Equal("SCANNED_DOCUMENT", ex.ErrorCode);
    }

    [Fact]
    public async Task ConvertAsync_CorruptedPdf_ThrowsUnsupportedOrCorrupted()
    {
        await using var stream = new MemoryStream(PdfFixtures.CreateCorruptedPdf());

        var ex = await Assert.ThrowsAsync<UnsupportedOrCorruptedDocumentException>(() =>
            _sut.ConvertAsync(stream, OutputFormat.Html));

        Assert.Equal("UNSUPPORTED_OR_CORRUPTED_INPUT", ex.ErrorCode);
    }
}
