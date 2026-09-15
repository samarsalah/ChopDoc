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

            var html = BuildHtml(pages);
            return Task.FromResult(new ConversionResult(
                Encoding.UTF8.GetBytes(html),
                "text/html; charset=utf-8",
                ".html",
                OutputFormat.Html,
                pages.Count));
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

    private static string BuildHtml(IReadOnlyList<Page> pages)
    {
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
            sb.AppendLine($"  <section {HtmlMarkerAttribute}=\"{marker}\">");
            sb.AppendLine($"    <h2>Page {i + 1}</h2>");

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

            foreach (var image in page.GetImages())
            {
                if (!TryGetImageBytes(image, out var bytes, out var mime))
                    continue;

                var base64 = Convert.ToBase64String(bytes);
                sb.AppendLine($"    <img src=\"data:{mime};base64,{base64}\" alt=\"Embedded image from page {i + 1}\" />");
            }

            sb.AppendLine("  </section>");
        }

        sb.AppendLine("</body>");
        sb.AppendLine("</html>");
        return sb.ToString();
    }

    private static IEnumerable<string> SplitParagraphs(string text) =>
        text.Split(new[] { "\r\n\r\n", "\n\n" }, StringSplitOptions.RemoveEmptyEntries)
            .Select(p => p.Trim())
            .Where(p => p.Length > 0);

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

            if (!image.RawBytes.IsEmpty)
            {
                bytes = image.RawBytes.ToArray();
                mime = "application/octet-stream";
                return true;
            }
        }
        catch
        {
            return false;
        }

        return false;
    }
}
