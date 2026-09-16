using System.Net;
using System.Text;
using ChopDoc.Domain.Abstractions;
using ChopDoc.Domain.Enums;
using ChopDoc.Domain.Exceptions;
using ChopDoc.Domain.Models;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;

namespace ChopDoc.Infrastructure.Conversion;

/// <summary>
/// Rule-based PDF → HTML (canonical intermediate). No OCR.
/// Text is rebuilt from word positions so columns and line wraps are not smashed together.
/// </summary>
public sealed class PdfDocumentConverter : IDocumentConverter
{
    public const string HtmlMarkerAttribute = "data-chopdoc-marker";

    public Task<ConversionResult> ConvertPdfToHtmlAsync(
        Stream sourcePdf,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var document = PdfDocument.Open(sourcePdf);
            var pages = document.GetPages().ToList();

            if (pages.Count == 0)
                throw new UnsupportedOrCorruptedDocumentException("The PDF contains no pages.");

            var combinedText = string.Concat(pages.Select(p => p.Text ?? string.Empty));
            if (string.IsNullOrWhiteSpace(combinedText))
                throw new ScannedDocumentException();

            var built = BuildHtml(pages);
            return Task.FromResult(new ConversionResult(
                Encoding.UTF8.GetBytes(built.Html),
                "text/html; charset=utf-8",
                ".html",
                OutputFormat.Html,
                pages.Count,
                built.PageMarkers,
                built.Warnings));
        }
        catch (DomainException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new UnsupportedOrCorruptedDocumentException(ex.Message);
        }
    }

    private static HtmlBuildResult BuildHtml(IReadOnlyList<Page> pages)
    {
        var markers = new List<string>(pages.Count);
        var warnings = new List<string>();
        var sb = new StringBuilder();
        sb.AppendLine("<!DOCTYPE html>");
        sb.AppendLine("<html lang=\"en\">");
        sb.AppendLine("<head>");
        sb.AppendLine("  <meta charset=\"utf-8\" />");
        sb.AppendLine("  <title>Converted document</title>");
        sb.AppendLine("  <style>body{font-family:Segoe UI,Arial,sans-serif;line-height:1.4;margin:1.5rem;} section{margin-bottom:2rem;padding-bottom:1rem;border-bottom:1px solid #ddd;} img{max-width:100%;height:auto;}</style>");
        sb.AppendLine("</head>");
        sb.AppendLine("<body>");

        for (var i = 0; i < pages.Count; i++)
        {
            var page = pages[i];
            var marker = $"page-{i + 1}";
            markers.Add(marker);
            sb.AppendLine($"  <section {HtmlMarkerAttribute}=\"{marker}\">");
            sb.AppendLine($"    <h2>Page {i + 1}</h2>");

            var blocks = PdfPageLayout.ExtractBlocks(page);
            if (blocks.Count == 0)
            {
                var text = page.Text?.Trim();
                if (!string.IsNullOrEmpty(text))
                {
                    foreach (var paragraph in SplitParagraphs(text))
                    {
                        sb.Append("    <p>");
                        sb.Append(WebUtility.HtmlEncode(paragraph));
                        sb.AppendLine("</p>");
                    }
                }
            }
            else
            {
                AppendBlocks(sb, blocks);
            }

            AppendImages(sb, page, i + 1, warnings);

            sb.AppendLine("  </section>");
        }

        sb.AppendLine("</body>");
        sb.AppendLine("</html>");
        return new HtmlBuildResult(sb.ToString(), markers, warnings);
    }

    private static void AppendBlocks(StringBuilder sb, IReadOnlyList<LayoutBlock> blocks)
    {
        var inList = false;
        foreach (var block in blocks)
        {
            if (block.Tag == "li")
            {
                if (!inList)
                {
                    sb.AppendLine("    <ul>");
                    inList = true;
                }

                sb.Append("      <li>");
                sb.Append(WebUtility.HtmlEncode(block.Text));
                sb.AppendLine("</li>");
                continue;
            }

            if (inList)
            {
                sb.AppendLine("    </ul>");
                inList = false;
            }

            sb.Append("    <").Append(block.Tag).Append('>');
            sb.Append(WebUtility.HtmlEncode(block.Text));
            sb.Append("</").Append(block.Tag).AppendLine(">");
        }

        if (inList)
            sb.AppendLine("    </ul>");
    }

    private static void AppendImages(StringBuilder sb, Page page, int pageNumber, List<string> warnings)
    {
        var seen = new HashSet<string>();
        foreach (var image in page.GetImages())
        {
            var box = image.BoundingBox;
            if (box.Width < 24 && box.Height < 24)
                continue;

            var key = $"{Math.Round(box.Left)}:{Math.Round(box.Bottom)}:{Math.Round(box.Width)}:{Math.Round(box.Height)}";
            if (!seen.Add(key))
                continue;

            if (!TryGetImageBytes(image, out var bytes, out var mime))
            {
                warnings.Add(
                    $"Page {pageNumber}: an embedded image uses an encoding that cannot be copied across and was left out of the output.");
                continue;
            }

            var base64 = Convert.ToBase64String(bytes);
            sb.AppendLine($"    <img src=\"data:{mime};base64,{base64}\" alt=\"Image from page {pageNumber}\" />");
        }
    }

    private static IEnumerable<string> SplitParagraphs(string text) =>
        text.Split(new[] { "\r\n\r\n", "\n\n" }, StringSplitOptions.RemoveEmptyEntries)
            .Select(p => p.Trim())
            .Where(p => p.Length > 0);

    /// <summary>
    /// Copies the image across untouched. PdfPig can only build a PNG for the encodings it
    /// decodes itself; a JPEG-encoded image (DCTDecode) comes back as raw bytes that are
    /// already a valid JPEG file, so it can be embedded as-is.
    /// </summary>
    private static bool TryGetImageBytes(IPdfImage image, out byte[] bytes, out string mime)
    {
        bytes = Array.Empty<byte>();
        mime = "image/png";

        try
        {
            if (image.TryGetPng(out var png) && png is { Length: > 0 })
            {
                bytes = png;
                mime = "image/png";
                return true;
            }

            var raw = image.RawBytes.ToArray();
            if (IsJpeg(raw))
            {
                bytes = raw;
                mime = "image/jpeg";
                return true;
            }
        }
        catch
        {
            return false;
        }

        return false;
    }

    private static bool IsJpeg(byte[] content) =>
        content.Length > 3 && content[0] == 0xFF && content[1] == 0xD8 && content[2] == 0xFF;

    private sealed record HtmlBuildResult(
        string Html,
        IReadOnlyList<string> PageMarkers,
        IReadOnlyList<string> Warnings);
}
