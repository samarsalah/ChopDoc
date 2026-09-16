using ChopDoc.Domain.Enums;
using ChopDoc.Domain.Exceptions;
using ChopDoc.Infrastructure.Conversion;
using ChopDoc.Tests.Helpers;
using DocumentFormat.OpenXml.Packaging;

namespace ChopDoc.Tests;

public class PdfDocumentConverterTests
{
    private readonly PdfDocumentConverter _sut = new();

    [Fact]
    public async Task ConvertPdfToHtmlAsync_TextPdf_ProducesHtmlWithMarkers()
    {
        await using var stream = new MemoryStream(PdfFixtures.CreateTextPdf("Hello ChopDoc", "Page two text"));

        var result = await _sut.ConvertPdfToHtmlAsync(stream);

        Assert.Equal(OutputFormat.Html, result.Format);
        Assert.Equal(".html", result.FileExtension);
        Assert.Equal(2, result.PageCount);

        // Validation uses these to prove every source page reached the output.
        Assert.Equal(new[] { "page-1", "page-2" }, result.PageMarkers);
        Assert.Empty(result.Warnings);

        var html = System.Text.Encoding.UTF8.GetString(result.Content);
        Assert.Contains("data-chopdoc-marker=\"page-1\"", html);
        Assert.Contains("data-chopdoc-marker=\"page-2\"", html);
        Assert.Contains("Hello ChopDoc", html);
    }

    /// <summary>
    /// Images used to be appended after all of a page's text, so a document laid out as
    /// text/image/text came out as text/text/image.
    /// </summary>
    [Fact]
    public async Task ConvertPdfToHtmlAsync_PlacesImagesBetweenTheTextTheySitBetween()
    {
        const string top = "TEXT ABOVE THE PICTURE";
        const string bottom = "TEXT BELOW THE PICTURE";

        await using var stream = new MemoryStream(
            PdfFixtures.CreateTextPdfWithImageBetweenParagraphs(top, bottom));

        var result = await _sut.ConvertPdfToHtmlAsync(stream);
        var html = System.Text.Encoding.UTF8.GetString(result.Content);

        Assert.Empty(result.Warnings);

        var topIndex = html.IndexOf(top, StringComparison.Ordinal);
        var imageIndex = html.IndexOf("<img", StringComparison.Ordinal);
        var bottomIndex = html.IndexOf(bottom, StringComparison.Ordinal);

        Assert.True(topIndex >= 0 && imageIndex >= 0 && bottomIndex >= 0);
        Assert.True(topIndex < imageIndex, "image should follow the text above it");
        Assert.True(imageIndex < bottomIndex, "image should precede the text below it");
    }

    [Fact]
    public async Task ConvertPdfToHtmlAsync_NoTextLayer_ThrowsScannedDocumentException()
    {
        await using var stream = new MemoryStream(PdfFixtures.CreateNoTextPdf());

        var ex = await Assert.ThrowsAsync<ScannedDocumentException>(() =>
            _sut.ConvertPdfToHtmlAsync(stream));

        Assert.Equal("SCANNED_DOCUMENT", ex.ErrorCode);
    }

    [Fact]
    public async Task ConvertPdfToHtmlAsync_CorruptedPdf_ThrowsUnsupportedOrCorrupted()
    {
        await using var stream = new MemoryStream(PdfFixtures.CreateCorruptedPdf());

        var ex = await Assert.ThrowsAsync<UnsupportedOrCorruptedDocumentException>(() =>
            _sut.ConvertPdfToHtmlAsync(stream));

        Assert.Equal("UNSUPPORTED_OR_CORRUPTED_INPUT", ex.ErrorCode);
    }
}

public class HtmlOutputExporterTests
{
    private readonly ChopDoc.Infrastructure.Export.HtmlOutputExporter _sut = new();

    [Fact]
    public void Supports_HtmlTextDocx()
    {
        Assert.True(_sut.Supports(OutputFormat.Html));
        Assert.True(_sut.Supports(OutputFormat.Docx));
        Assert.False(_sut.Supports(OutputFormat.PlainText));
        Assert.False(_sut.Supports(OutputFormat.Markdown));
        Assert.False(_sut.Supports(OutputFormat.Rtf));
    }

    [Fact]
    public void Export_Docx_CreatesValidOpenXmlPackage()
    {
        var html = """
            <!DOCTYPE html><html><body>
            <section data-chopdoc-marker="page-1">
              <h2>Page 1</h2>
              <p>Hello from ChopDoc</p>
            </section>
            </body></html>
            """;

        var result = _sut.Export(
            System.Text.Encoding.UTF8.GetBytes(html),
            "sample",
            1,
            1,
            OutputFormat.Docx);

        Assert.Equal(".docx", result.FileExtension);
        Assert.EndsWith(".docx", result.FileName);
        Assert.True(result.Content.Length > 0);

        using var ms = new MemoryStream(result.Content);
        using var doc = WordprocessingDocument.Open(ms, false);
        Assert.NotNull(doc.MainDocumentPart);
        var text = doc.MainDocumentPart!.Document.InnerText;
        Assert.Contains("Hello from ChopDoc", text);
    }
}
