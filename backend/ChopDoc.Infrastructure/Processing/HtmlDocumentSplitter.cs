using System.Text;
using System.Text.RegularExpressions;
using ChopDoc.Domain.Abstractions;
using ChopDoc.Domain.Exceptions;
using ChopDoc.Domain.Models;

namespace ChopDoc.Infrastructure.Processing;

/// <summary>
/// Splits converted HTML by marked page sections when over the size limit.
/// </summary>
public sealed class HtmlDocumentSplitter : IDocumentSplitter
{
    private static readonly Regex SectionRegex = new(
        @"<section\s+[^>]*data-chopdoc-marker\s*=\s*""(?<marker>[^""]+)""[^>]*>(?<body>[\s\S]*?)</section>",
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

        var extension = string.IsNullOrWhiteSpace(fileExtension) ? ".html" : fileExtension;
        if (!extension.StartsWith('.'))
            extension = "." + extension;

        var safeBase = string.IsNullOrWhiteSpace(baseFileName) ? "document" : baseFileName;

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

        var html = Encoding.UTF8.GetString(convertedContent);
        var units = ExtractUnits(html);

        if (units.Count == 0)
        {
            // Whole document is one unsplittable blob over the limit.
            throw new UnsplittableContentException(
                $"Converted output is {convertedContent.LongLength} bytes and has no splittable page sections.");
        }

        foreach (var unit in units)
        {
            var unitBytes = Encoding.UTF8.GetByteCount(WrapHtml(unit.SectionHtml));
            if (unitBytes > sizeLimitBytes)
            {
                throw new UnsplittableContentException(
                    $"Section '{unit.Marker}' is {unitBytes} bytes, which exceeds the limit of {sizeLimitBytes} bytes.");
            }
        }

        var packs = PackUnits(units, sizeLimitBytes);
        var total = packs.Count;
        var parts = new List<SplitPartContent>(total);

        for (var i = 0; i < packs.Count; i++)
        {
            var pack = packs[i];
            var partHtml = WrapHtml(string.Concat(pack.Select(u => u.SectionHtml)));
            var bytes = Encoding.UTF8.GetBytes(partHtml);
            var markers = string.Join(';', pack.Select(u => u.Marker));
            var fileName = total == 1
                ? $"{safeBase}{extension}"
                : $"{safeBase}.part{i + 1}-of-{total}{extension}";

            parts.Add(new SplitPartContent(i + 1, total, bytes, fileName, markers));
        }

        return parts;
    }

    private static List<ContentUnit> ExtractUnits(string html)
    {
        var units = new List<ContentUnit>();
        foreach (Match match in SectionRegex.Matches(html))
        {
            var marker = match.Groups["marker"].Value;
            var sectionHtml = match.Value;
            units.Add(new ContentUnit(marker, sectionHtml));
        }

        return units;
    }

    private static List<List<ContentUnit>> PackUnits(IReadOnlyList<ContentUnit> units, long sizeLimitBytes)
    {
        var packs = new List<List<ContentUnit>>();
        var current = new List<ContentUnit>();
        long currentSize = OverheadBytes();

        foreach (var unit in units)
        {
            var unitSize = Encoding.UTF8.GetByteCount(unit.SectionHtml);

            if (current.Count > 0 && currentSize + unitSize > sizeLimitBytes)
            {
                packs.Add(current);
                current = new List<ContentUnit>();
                currentSize = OverheadBytes();
            }

            current.Add(unit);
            currentSize += unitSize;
        }

        if (current.Count > 0)
            packs.Add(current);

        return packs;
    }

    private static long OverheadBytes() =>
        Encoding.UTF8.GetByteCount(WrapHtml(string.Empty));

    private static string WrapHtml(string bodyInnerSections) =>
        "<!DOCTYPE html><html lang=\"en\"><head><meta charset=\"utf-8\" /><title>Converted document part</title></head><body>"
        + bodyInnerSections
        + "</body></html>";

    private sealed record ContentUnit(string Marker, string SectionHtml);
}
