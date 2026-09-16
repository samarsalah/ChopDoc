using System.Text;
using System.Text.RegularExpressions;
using ChopDoc.Domain.Enums;
using ChopDoc.Infrastructure.Export;
using DocumentFormat.OpenXml.Packaging;

namespace ChopDoc.Tests;

/// <summary>
/// Images have to survive the whole way into the delivered file, not just into the HTML
/// intermediate. These assert the DOCX package, since that is where the loss was invisible.
/// </summary>
public class HtmlOutputExporterImageTests
{
    private readonly HtmlOutputExporter _sut = new();

    /// <summary>A 1x1 JPEG — the encoding PdfPig hands back as raw bytes rather than a PNG.</summary>
    private const string JpegBase64 =
        "/9j/4AAQSkZJRgABAQEAYABgAAD/2wBDAAgGBgcGBQgHBwcJCQgKDBQNDAsLDBkSEw8UHRofHh0aHBwcJC4nICIsIxwcKDcpLDAxNDQ0Hyc5PTgyPC4zNDL/wAALCABAAEABAREA/8QAHwAAAQUBAQEBAQEAAAAAAAAAAAECAwQFBgcICQoL/8QAtRAAAgEDAwIEAwUFBAQAAAF9AQIDAAQRBRIhMUEGE1FhByJxFDKBkaEII0KxwRVS0fAkM2JyggkKFhcYGRolJicoKSo0NTY3ODk6Q0RFRkdISUpTVFVWV1hZWmNkZWZnaGlqc3R1dnd4eXqDhIWGh4iJipKTlJWWl5iZmqKjpKWmp6ipqrKztLW2t7i5usLDxMXGx8jJytLT1NXW19jZ2uHi4+Tl5ufo6erx8vP09fb3+Pn6/9oACAEBAAA/APn+iiigD//Z";

    [Fact]
    public void Export_Docx_EmbedsImageIntoThePackage()
    {
        var result = _sut.Export(
            Encoding.UTF8.GetBytes(HtmlWithImages(1)),
            "sample",
            1,
            1,
            OutputFormat.Docx);

        using var stream = new MemoryStream(result.Content);
        using var document = WordprocessingDocument.Open(stream, false);

        Assert.Single(document.MainDocumentPart!.ImageParts);
        Assert.Contains("Body text", document.MainDocumentPart.Document.InnerText);
    }

    /// <summary>
    /// Word treats duplicate drawing ids as a corrupt document and refuses to render the images,
    /// so each one must get its own.
    /// </summary>
    [Fact]
    public void Export_Docx_GivesEveryImageAUniqueDrawingId()
    {
        var result = _sut.Export(
            Encoding.UTF8.GetBytes(HtmlWithImages(3)),
            "sample",
            1,
            1,
            OutputFormat.Docx);

        using var stream = new MemoryStream(result.Content);
        using var document = WordprocessingDocument.Open(stream, false);

        Assert.Equal(3, document.MainDocumentPart!.ImageParts.Count());

        var ids = Regex.Matches(document.MainDocumentPart.Document.OuterXml, @"<wp:docPr id=""(?<id>\d+)""")
            .Select(m => m.Groups["id"].Value)
            .ToList();

        Assert.Equal(3, ids.Count);
        Assert.Equal(ids.Count, ids.Distinct().Count());
    }

    /// <summary>
    /// Text and images used to be exported in two passes, which collected every image at the end
    /// of the document regardless of where it appeared in the source.
    /// </summary>
    [Fact]
    public void Export_Docx_KeepsImagesInDocumentOrder()
    {
        var html = "<!DOCTYPE html><html><body><section data-chopdoc-marker=\"page-1\">"
            + "<p>before the image</p>"
            + $"<img src=\"data:image/jpeg;base64,{JpegBase64}\" width=\"64\" height=\"64\" />"
            + "<p>after the image</p>"
            + "</section></body></html>";

        var result = _sut.Export(Encoding.UTF8.GetBytes(html), "sample", 1, 1, OutputFormat.Docx);

        using var stream = new MemoryStream(result.Content);
        using var document = WordprocessingDocument.Open(stream, false);
        var xml = document.MainDocumentPart!.Document.OuterXml;

        var before = xml.IndexOf("before the image", StringComparison.Ordinal);
        var drawing = xml.IndexOf("<w:drawing>", StringComparison.Ordinal);
        var after = xml.IndexOf("after the image", StringComparison.Ordinal);

        Assert.True(before < drawing, "image should follow the paragraph that precedes it");
        Assert.True(drawing < after, "image should precede the paragraph that follows it");
    }

    /// <summary>A portrait image must not be stretched into a fixed landscape box.</summary>
    [Fact]
    public void Export_Docx_KeepsImageAspectRatio()
    {
        var html = "<!DOCTYPE html><html><body>"
            + $"<img src=\"data:image/jpeg;base64,{JpegBase64}\" width=\"1000\" height=\"2000\" />"
            + "</body></html>";

        var result = _sut.Export(Encoding.UTF8.GetBytes(html), "sample", 1, 1, OutputFormat.Docx);

        using var stream = new MemoryStream(result.Content);
        using var document = WordprocessingDocument.Open(stream, false);

        var extent = Regex.Match(
            document.MainDocumentPart!.Document.OuterXml,
            @"<wp:extent cx=""(?<cx>\d+)"" cy=""(?<cy>\d+)""");

        Assert.True(extent.Success);
        var cx = long.Parse(extent.Groups["cx"].Value);
        var cy = long.Parse(extent.Groups["cy"].Value);
        Assert.Equal(2.0, cy / (double)cx, precision: 2);
    }

    [Fact]
    public void Export_PlainText_MarksImagePositionsInsteadOfDroppingThem()
    {
        var result = _sut.Export(
            Encoding.UTF8.GetBytes(HtmlWithImages(2)),
            "sample",
            1,
            1,
            OutputFormat.PlainText);

        var text = Encoding.UTF8.GetString(result.Content);

        Assert.Equal(2, Regex.Matches(text, Regex.Escape("[image]")).Count);
        Assert.Contains("Body text", text);
    }

    [Fact]
    public void Export_Docx_SkipsUndecodableImageButKeepsText()
    {
        var html = "<!DOCTYPE html><html><body>"
            + "<p>Body text 1</p>"
            + "<img src=\"data:image/png;base64,not-valid-base64!!\" width=\"10\" height=\"10\" />"
            + "</body></html>";

        var result = _sut.Export(Encoding.UTF8.GetBytes(html), "sample", 1, 1, OutputFormat.Docx);

        using var stream = new MemoryStream(result.Content);
        using var document = WordprocessingDocument.Open(stream, false);

        Assert.Empty(document.MainDocumentPart!.ImageParts);
        Assert.Contains("Body text 1", document.MainDocumentPart.Document.InnerText);
    }

    private static string HtmlWithImages(int count)
    {
        var body = new StringBuilder("<!DOCTYPE html><html><body><section data-chopdoc-marker=\"page-1\">");
        for (var i = 1; i <= count; i++)
        {
            body.Append($"<p>Body text {i}</p>");
            body.Append($"<img src=\"data:image/jpeg;base64,{JpegBase64}\" width=\"64\" height=\"64\" />");
        }

        return body.Append("</section></body></html>").ToString();
    }
}
