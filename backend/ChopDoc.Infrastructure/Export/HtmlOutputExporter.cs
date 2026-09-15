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
/// Supported: Html, PlainText, Markdown, Docx.
/// </summary>
public sealed class HtmlOutputExporter : IOutputExporter
{
    private static readonly Regex ParagraphRegex = new(
        @"<p[^>]*>(?<text>[\s\S]*?)</p>",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex HeadingRegex = new(
        @"<h2[^>]*>(?<text>[\s\S]*?)</h2>",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex ImageRegex = new(
        @"<img[^>]*src\s*=\s*""(?<src>data:(?<mime>[^;]+);base64,(?<data>[^""]+))""[^>]*>",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex TagRegex = new("<.*?>", RegexOptions.Singleline | RegexOptions.Compiled);

    public bool Supports(OutputFormat format) =>
        format is OutputFormat.Html
            or OutputFormat.PlainText
            or OutputFormat.Markdown
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
            OutputFormat.PlainText => new ExportResult(
                Encoding.UTF8.GetBytes(ToPlainText(Encoding.UTF8.GetString(htmlContent))),
                fileName,
                "text/plain; charset=utf-8",
                ".txt"),
            OutputFormat.Markdown => new ExportResult(
                Encoding.UTF8.GetBytes(ToMarkdown(Encoding.UTF8.GetString(htmlContent))),
                fileName,
                "text/markdown; charset=utf-8",
                ".md"),
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
        OutputFormat.PlainText => ".txt",
        OutputFormat.Markdown => ".md",
        OutputFormat.Docx => ".docx",
        _ => ".bin"
    };

    private static string ToPlainText(string html)
    {
        var sb = new StringBuilder();
        foreach (Match h in HeadingRegex.Matches(html))
            sb.AppendLine(Decode(h.Groups["text"].Value));

        foreach (Match p in ParagraphRegex.Matches(html))
            sb.AppendLine(Decode(p.Groups["text"].Value));

        return sb.ToString().Trim() + Environment.NewLine;
    }

    private static string ToMarkdown(string html)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# Converted document");
        sb.AppendLine();

        foreach (Match h in HeadingRegex.Matches(html))
        {
            sb.Append("## ").AppendLine(Decode(h.Groups["text"].Value));
            sb.AppendLine();
        }

        foreach (Match p in ParagraphRegex.Matches(html))
        {
            sb.AppendLine(Decode(p.Groups["text"].Value));
            sb.AppendLine();
        }

        foreach (Match img in ImageRegex.Matches(html))
        {
            sb.Append("![embedded image](").Append(img.Groups["src"].Value).AppendLine(")");
            sb.AppendLine();
        }

        return sb.ToString();
    }

    private static byte[] ToDocx(string html)
    {
        using var stream = new MemoryStream();
        using (var word = WordprocessingDocument.Create(stream, WordprocessingDocumentType.Document, true))
        {
            var mainPart = word.AddMainDocumentPart();
            mainPart.Document = new Document(new Body());
            var body = mainPart.Document.Body!;

            foreach (Match h in HeadingRegex.Matches(html))
            {
                body.AppendChild(CreateParagraph(Decode(h.Groups["text"].Value), bold: true, fontSize: "28"));
            }

            foreach (Match p in ParagraphRegex.Matches(html))
            {
                body.AppendChild(CreateParagraph(Decode(p.Groups["text"].Value)));
            }

            var imageIndex = 0;
            foreach (Match img in ImageRegex.Matches(html))
            {
                try
                {
                    var bytes = Convert.FromBase64String(img.Groups["data"].Value);
                    var mime = img.Groups["mime"].Value.ToLowerInvariant();
                    var imagePartType = mime.Contains("jpeg") || mime.Contains("jpg")
                        ? ImagePartType.Jpeg
                        : ImagePartType.Png;

                    var imagePart = mainPart.AddImagePart(imagePartType);
                    using (var ms = new MemoryStream(bytes))
                        imagePart.FeedData(ms);

                    var relationshipId = mainPart.GetIdOfPart(imagePart);
                    body.AppendChild(CreateImageParagraph(relationshipId, $"image{imageIndex++}"));
                }
                catch
                {
                    // Skip undecodable images; text content still exported.
                }
            }

            mainPart.Document.Save();
        }

        return stream.ToArray();
    }

    private static Paragraph CreateParagraph(string text, bool bold = false, string fontSize = "22")
    {
        var runProps = new RunProperties(new FontSize { Val = fontSize });
        if (bold)
            runProps.Append(new Bold());

        return new Paragraph(
            new Run(runProps, new Text(text)));
    }

    private static Paragraph CreateImageParagraph(string relationshipId, string name)
    {
        long cx = 4572000;
        long cy = 2571750;

        var element =
            new Drawing(
                new DW.Inline(
                    new DW.Extent { Cx = cx, Cy = cy },
                    new DW.EffectExtent { LeftEdge = 0L, TopEdge = 0L, RightEdge = 0L, BottomEdge = 0L },
                    new DW.DocProperties { Id = (UInt32Value)1U, Name = name },
                    new DW.NonVisualGraphicFrameDrawingProperties(
                        new A.GraphicFrameLocks { NoChangeAspect = true }),
                    new A.Graphic(
                        new A.GraphicData(
                            new PIC.Picture(
                                new PIC.NonVisualPictureProperties(
                                    new PIC.NonVisualDrawingProperties { Id = 0U, Name = name },
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
