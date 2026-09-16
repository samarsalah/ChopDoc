using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using ChopDoc.Domain.Abstractions;
using ChopDoc.Domain.Enums;
using ChopDoc.Domain.Exceptions;
using ChopDoc.Domain.Models;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using A = DocumentFormat.OpenXml.Drawing;
using DW = DocumentFormat.OpenXml.Drawing.Wordprocessing;
using PIC = DocumentFormat.OpenXml.Drawing.Pictures;

namespace ChopDoc.Infrastructure.Export;

/// <summary>
/// Exports HTML parts (after split/validate) to the requested user format.
/// Supported: Html, Docx.
/// </summary>
public sealed class HtmlOutputExporter : IOutputExporter
{
    /// <summary>
    /// Block elements and images in a single pattern so they are exported in document order,
    /// instead of carrying the text across first and appending every image at the end.
    /// </summary>
    private static readonly Regex ContentRegex = new(
        @"<img\b[^>]*src\s*=\s*""data:(?<mime>[^;]+);base64,(?<data>[^""]+)""[^>]*>"
        + @"|<(?<tag>h1|h2|h3|p|li)\b[^>]*>(?<text>[\s\S]*?)</\k<tag>>",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex ImageWidthRegex = new(
        @"\bwidth\s*=\s*""(?<value>\d+)""",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex ImageHeightRegex = new(
        @"\bheight\s*=\s*""(?<value>\d+)""",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex TagRegex = new("<.*?>", RegexOptions.Singleline | RegexOptions.Compiled);

    /// <summary>Rendered image width: 6 inches at 914400 EMU per inch.</summary>
    private const long RenderedImageWidthEmu = 5_486_400;

    public bool Supports(OutputFormat format) =>
        format is OutputFormat.Html
            or OutputFormat.Docx;

    public ExportResult Export(
        byte[] htmlContent,
        string baseFileName,
        int partNumber,
        int totalParts,
        OutputFormat format)
    {
        if (!Supports(format))
            throw new UnsupportedOutputFormatException(format.ToString());

        var safeBase = string.IsNullOrWhiteSpace(baseFileName) ? "document" : baseFileName;
        var fileName = totalParts <= 1
            ? $"{safeBase}{ExtensionFor(format)}"
            : $"{safeBase}.part{partNumber}-of-{totalParts}{ExtensionFor(format)}";

        return format switch
        {
            OutputFormat.Html => new ExportResult(
                htmlContent,
                fileName,
                "text/html; charset=utf-8",
                ".html"),
            OutputFormat.Docx => new ExportResult(
                ToDocx(Encoding.UTF8.GetString(htmlContent)),
                fileName,
                "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
                ".docx"),
            _ => throw new UnsupportedOutputFormatException(format.ToString())
        };
    }

    private static string ExtensionFor(OutputFormat format) => format switch
    {
        OutputFormat.Html => ".html",
        OutputFormat.Docx => ".docx",
        _ => ".bin"
    };

    private static byte[] ToDocx(string html)
    {
        using var stream = new MemoryStream();
        using (var word = WordprocessingDocument.Create(stream, WordprocessingDocumentType.Document, true))
        {
            var mainPart = word.AddMainDocumentPart();
            mainPart.Document = new Document(new Body());
            var body = mainPart.Document.Body!;
            var imageCount = 0u;

            foreach (Match match in ContentRegex.Matches(html))
            {
                if (match.Groups["data"].Success)
                {
                    if (TryAppendImage(mainPart, body, match, imageCount + 1))
                        imageCount++;

                    continue;
                }

                var text = Decode(match.Groups["text"].Value);
                if (text.Length == 0)
                    continue;

                body.AppendChild(match.Groups["tag"].Value.ToLowerInvariant() switch
                {
                    "h1" or "h2" => CreateParagraph(text, bold: true, fontSize: "28"),
                    "h3" => CreateParagraph(text, bold: true, fontSize: "24"),
                    "li" => CreateParagraph("• " + text),
                    _ => CreateParagraph(text)
                });
            }

            mainPart.Document.Save();
        }

        return stream.ToArray();
    }

    private static bool TryAppendImage(MainDocumentPart mainPart, Body body, Match image, uint imageId)
    {
        try
        {
            var bytes = Convert.FromBase64String(image.Groups["data"].Value);
            var mime = image.Groups["mime"].Value.ToLowerInvariant();
            var imagePartType = mime.Contains("jpeg") || mime.Contains("jpg")
                ? ImagePartType.Jpeg
                : ImagePartType.Png;

            var imagePart = mainPart.AddImagePart(imagePartType);
            using (var content = new MemoryStream(bytes))
                imagePart.FeedData(content);

            body.AppendChild(CreateImageParagraph(
                mainPart.GetIdOfPart(imagePart),
                imageId,
                RenderedExtent(image.Value)));

            return true;
        }
        catch
        {
            // Skip undecodable images; text content still exported.
            return false;
        }
    }

    /// <summary>
    /// Renders at a fixed width and derives the height from the source dimensions the converter
    /// recorded, so a portrait image is not stretched into a landscape box.
    /// </summary>
    private static (long Cx, long Cy) RenderedExtent(string imageTag)
    {
        var width = ImageWidthRegex.Match(imageTag).Groups["value"].Value;
        var height = ImageHeightRegex.Match(imageTag).Groups["value"].Value;

        if (!int.TryParse(width, out var samplesWide) || samplesWide <= 0 ||
            !int.TryParse(height, out var samplesHigh) || samplesHigh <= 0)
        {
            return (RenderedImageWidthEmu, RenderedImageWidthEmu * 3 / 4);
        }

        return (RenderedImageWidthEmu, (long)(RenderedImageWidthEmu * (samplesHigh / (double)samplesWide)));
    }

    private static Paragraph CreateParagraph(string text, bool bold = false, string fontSize = "22")
    {
        var runProps = new RunProperties(new FontSize { Val = fontSize });
        if (bold)
            runProps.Append(new Bold());

        return new Paragraph(
            new Run(runProps, new Text(text)));
    }

    private static Paragraph CreateImageParagraph(string relationshipId, uint imageId, (long Cx, long Cy) extent)
    {
        // Word treats a document where two drawings share a docPr id as corrupt, so every
        // image needs its own.
        var name = $"Image {imageId}";
        var (cx, cy) = extent;

        var element =
            new Drawing(
                new DW.Inline(
                    new DW.Extent { Cx = cx, Cy = cy },
                    new DW.EffectExtent { LeftEdge = 0L, TopEdge = 0L, RightEdge = 0L, BottomEdge = 0L },
                    new DW.DocProperties { Id = imageId, Name = name },
                    new DW.NonVisualGraphicFrameDrawingProperties(
                        new A.GraphicFrameLocks { NoChangeAspect = true }),
                    new A.Graphic(
                        new A.GraphicData(
                            new PIC.Picture(
                                new PIC.NonVisualPictureProperties(
                                    new PIC.NonVisualDrawingProperties { Id = imageId, Name = name },
                                    new PIC.NonVisualPictureDrawingProperties()),
                                new PIC.BlipFill(
                                    new A.Blip { Embed = relationshipId },
                                    new A.Stretch(new A.FillRectangle())),
                                new PIC.ShapeProperties(
                                    new A.Transform2D(
                                        new A.Offset { X = 0L, Y = 0L },
                                        new A.Extents { Cx = cx, Cy = cy }),
                                    new A.PresetGeometry(new A.AdjustValueList())
                                    { Preset = A.ShapeTypeValues.Rectangle }))
                        )
                        { Uri = "http://schemas.openxmlformats.org/drawingml/2006/picture" })
                )
                {
                    DistanceFromTop = 0U,
                    DistanceFromBottom = 0U,
                    DistanceFromLeft = 0U,
                    DistanceFromRight = 0U
                });

        return new Paragraph(new Run(element));
    }

    private static string Decode(string value) =>
        WebUtility.HtmlDecode(TagRegex.Replace(value, string.Empty)).Trim();
}
