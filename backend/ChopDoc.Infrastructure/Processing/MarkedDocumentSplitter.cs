using System.Text;
using System.Text.RegularExpressions;
using ChopDoc.Domain.Abstractions;
using ChopDoc.Domain.Exceptions;
using ChopDoc.Domain.Models;

namespace ChopDoc.Infrastructure.Processing;

/// <summary>
/// Splits converted content by page markers, then by smaller HTML atoms when a page exceeds the limit.
/// </summary>
public sealed class MarkedDocumentSplitter : IDocumentSplitter
{
    private static readonly Regex HtmlSectionRegex = new(
        @"<section\s+[^>]*data-chopdoc-marker\s*=\s*""(?<marker>[^""]+)""[^>]*>(?<body>[\s\S]*?)</section>",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex TextSectionRegex = new(
        @"===CHOPDOC:(?<marker>[^=\r\n]+)===\r?\n(?<body>[\s\S]*?)(?======CHOPDOC:|\z)",
        RegexOptions.Compiled);

    /// <summary>Headings, paragraphs, and images as atomic split units.</summary>
    private static readonly Regex HtmlAtomRegex = new(
        @"<img\b[^>]*>|<(?<tag>h1|h2|h3|p)\b[^>]*>[\s\S]*?</\k<tag>>",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public IReadOnlyList<SplitPartContent> SplitIfNeeded(
        byte[] convertedContent,
        string baseFileName,
        string fileExtension,
        long sizeLimitBytes)
    {
        if (convertedContent is null)
            throw new ArgumentNullException(nameof(convertedContent));

        if (sizeLimitBytes <= 0)
            throw new ArgumentOutOfRangeException(nameof(sizeLimitBytes));

        if (convertedContent.Length == 0)
            throw new UnsupportedOrCorruptedDocumentException("Converted output is empty.");

        var extension = NormalizeExtension(fileExtension);
        var safeBase = string.IsNullOrWhiteSpace(baseFileName) ? "document" : baseFileName;
        var isHtml = extension.Equals(".html", StringComparison.OrdinalIgnoreCase);

        if (convertedContent.LongLength <= sizeLimitBytes)
        {
            return new[]
            {
                new SplitPartContent(
                    1,
                    1,
                    convertedContent,
                    $"{safeBase}{extension}",
                    "full-document")
            };
        }

        var text = Encoding.UTF8.GetString(convertedContent);
        var units = isHtml ? ExtractHtmlUnits(text) : ExtractTextUnits(text);

        if (units.Count == 0)
        {
            throw new UnsplittableContentException(
                $"Converted output is {convertedContent.LongLength} bytes and has no splittable page sections.");
        }

        if (isHtml)
            units = ExpandOversizedHtmlUnits(units, sizeLimitBytes);

        foreach (var unit in units)
        {
            var wrapped = WrapUnitContent(unit.Content, isHtml);
            var unitBytes = Encoding.UTF8.GetByteCount(wrapped);
            if (unitBytes > sizeLimitBytes)
            {
                throw new UnsplittableContentException(
                    $"Section '{unit.Marker}' is {unitBytes} bytes, which exceeds the limit of {sizeLimitBytes} bytes.");
            }
        }

        var packs = PackUnits(units, sizeLimitBytes, isHtml);
        var total = packs.Count;
        var parts = new List<SplitPartContent>(total);

        for (var i = 0; i < packs.Count; i++)
        {
            var pack = packs[i];
            var body = string.Concat(pack.Select(u => u.Content));
            var partText = WrapUnitContent(body, isHtml);
            var bytes = Encoding.UTF8.GetBytes(partText);
            var markers = string.Join(';', pack.Select(u => u.Marker));
            var fileName = total == 1
                ? $"{safeBase}{extension}"
                : $"{safeBase}.part{i + 1}-of-{total}{extension}";

            parts.Add(new SplitPartContent(i + 1, total, bytes, fileName, markers));
        }

        return parts;
    }

    private static string NormalizeExtension(string fileExtension)
    {
        var extension = string.IsNullOrWhiteSpace(fileExtension) ? ".bin" : fileExtension;
        return extension.StartsWith('.') ? extension : "." + extension;
    }

    private static List<ContentUnit> ExtractHtmlUnits(string html)
    {
        var units = new List<ContentUnit>();
        foreach (Match match in HtmlSectionRegex.Matches(html))
            units.Add(new ContentUnit(match.Groups["marker"].Value, match.Value));
        return units;
    }

    private static List<ContentUnit> ExpandOversizedHtmlUnits(
        IReadOnlyList<ContentUnit> units,
        long sizeLimitBytes)
    {
        var expanded = new List<ContentUnit>();

        foreach (var unit in units)
        {
            var unitBytes = Encoding.UTF8.GetByteCount(WrapHtml(unit.Content));
            if (unitBytes <= sizeLimitBytes)
            {
                expanded.Add(unit);
                continue;
            }

            var bodyMatch = HtmlSectionRegex.Match(unit.Content);
            var body = bodyMatch.Success ? bodyMatch.Groups["body"].Value : unit.Content;
            var atoms = HtmlAtomRegex.Matches(body);

            if (atoms.Count == 0)
            {
                // Cannot break further — keep as-is; caller will raise unsplittable if still over limit.
                expanded.Add(unit);
                continue;
            }

            var index = 1;
            foreach (Match atom in atoms)
            {
                var marker = $"{unit.Marker}#{index}";
                var section =
                    $"<section data-chopdoc-marker=\"{marker}\">{atom.Value}</section>";
                expanded.Add(new ContentUnit(marker, section));
                index++;
            }
        }

        return expanded;
    }

    private static List<ContentUnit> ExtractTextUnits(string text)
    {
        var units = new List<ContentUnit>();
        foreach (Match match in TextSectionRegex.Matches(text))
        {
            var marker = match.Groups["marker"].Value.Trim();
            var content = $"===CHOPDOC:{marker}===\n{match.Groups["body"].Value}";
            units.Add(new ContentUnit(marker, content));
        }

        return units;
    }

    private static List<List<ContentUnit>> PackUnits(
        IReadOnlyList<ContentUnit> units,
        long sizeLimitBytes,
        bool isHtml)
    {
        var packs = new List<List<ContentUnit>>();
        var current = new List<ContentUnit>();
        long currentSize = OverheadBytes(isHtml);

        foreach (var unit in units)
        {
            var unitSize = Encoding.UTF8.GetByteCount(unit.Content);

            if (current.Count > 0 && currentSize + unitSize > sizeLimitBytes)
            {
                packs.Add(current);
                current = new List<ContentUnit>();
                currentSize = OverheadBytes(isHtml);
            }

            current.Add(unit);
            currentSize += unitSize;
        }

        if (current.Count > 0)
            packs.Add(current);

        return packs;
    }

    private static long OverheadBytes(bool isHtml) =>
        isHtml ? Encoding.UTF8.GetByteCount(WrapHtml(string.Empty)) : 0;

    private static string WrapUnitContent(string content, bool isHtml) =>
        isHtml ? WrapHtml(content) : content;

    private static string WrapHtml(string bodyInnerSections) =>
        "<!DOCTYPE html><html lang=\"en\"><head><meta charset=\"utf-8\" /><title>Converted document part</title></head><body>"
        + bodyInnerSections
        + "</body></html>";

    private sealed record ContentUnit(string Marker, string Content);
}
