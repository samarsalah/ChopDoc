using System.Text;
using System.Text.RegularExpressions;
using ChopDoc.Domain.Abstractions;
using ChopDoc.Domain.Exceptions;
using ChopDoc.Domain.Models;

namespace ChopDoc.Infrastructure.Processing;

/// <summary>
/// Splits converted content by page markers, then by smaller HTML atoms when a page exceeds the
/// limit. Every size decision is measured in the requested output format, not in the HTML
/// intermediate, because that is the artifact the limit applies to.
/// </summary>
public sealed class MarkedDocumentSplitter : IDocumentSplitter
{
    private const string MarkerAttribute = "data-chopdoc-marker";

    private static readonly Regex HtmlSectionRegex = new(
        @"<section\s+[^>]*data-chopdoc-marker\s*=\s*""(?<marker>[^""]+)""[^>]*>(?<body>[\s\S]*?)</section>",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex TextSectionRegex = new(
        @"===CHOPDOC:(?<marker>[^=\r\n]+)===\r?\n(?<body>[\s\S]*?)(?======CHOPDOC:|\z)",
        RegexOptions.Compiled);

    /// <summary>Headings, paragraphs, and images as atomic split units.</summary>
    private static readonly Regex HtmlAtomRegex = new(
        @"<img\b[^>]*>|<(?<tag>h1|h2|h3|p|li)\b[^>]*>[\s\S]*?</\k<tag>>",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex TagRegex = new("<.*?>", RegexOptions.Singleline | RegexOptions.Compiled);

    public IReadOnlyList<SplitPartContent> SplitIfNeeded(
        byte[] convertedContent,
        string baseFileName,
        string fileExtension,
        long sizeLimitBytes,
        ExportedSizeProbe measureExportedSize)
    {
        if (convertedContent is null)
            throw new ArgumentNullException(nameof(convertedContent));

        if (measureExportedSize is null)
            throw new ArgumentNullException(nameof(measureExportedSize));

        if (sizeLimitBytes <= 0)
            throw new ArgumentOutOfRangeException(nameof(sizeLimitBytes));

        if (convertedContent.Length == 0)
            throw new UnsupportedOrCorruptedDocumentException("Converted output is empty.");

        var extension = NormalizeExtension(fileExtension);
        var safeBase = string.IsNullOrWhiteSpace(baseFileName) ? "document" : baseFileName;
        var isHtml = extension.Equals(".html", StringComparison.OrdinalIgnoreCase);
        var measure = new PartSizeProbe(isHtml, measureExportedSize);

        // Measured in the delivered format: plain text and DOCX are usually much smaller than
        // the HTML they came from, so judging by the intermediate would split documents that
        // would have fit into a single part.
        if (measure.SizeOfWholeDocument(convertedContent) <= sizeLimitBytes)
        {
            return new[]
            {
                new SplitPartContent(
                    1,
                    1,
                    convertedContent,
                    $"{safeBase}{extension}",
                    SplitPartContent.FullDocumentMarker)
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
            units = ExpandOversizedHtmlUnits(units, sizeLimitBytes, measure);

        foreach (var unit in units)
        {
            var unitBytes = measure.SizeOf(unit.Content);
            if (unitBytes > sizeLimitBytes)
            {
                throw new UnsplittableContentException(
                    $"Section '{unit.Marker}' is {unitBytes} bytes once exported, which exceeds the limit of {sizeLimitBytes} bytes.");
            }
        }

        var packs = PackUnits(units, sizeLimitBytes, measure);
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
        long sizeLimitBytes,
        PartSizeProbe measure)
    {
        var expanded = new List<ContentUnit>();

        foreach (var unit in units)
        {
            if (measure.SizeOf(unit.Content) <= sizeLimitBytes)
            {
                expanded.Add(unit);
                continue;
            }

            var bodyMatch = HtmlSectionRegex.Match(unit.Content);
            var body = bodyMatch.Success ? bodyMatch.Groups["body"].Value : unit.Content;
            var atoms = SplitIntoAtoms(body);

            if (atoms.Count <= 1)
            {
                // Cannot break further — the caller raises unsplittable if it is still over.
                expanded.Add(unit);
                continue;
            }

            for (var i = 0; i < atoms.Count; i++)
            {
                var marker = $"{unit.Marker}#{i + 1}";
                expanded.Add(new ContentUnit(
                    marker,
                    $"<section {MarkerAttribute}=\"{marker}\">{atoms[i]}</section>"));
            }
        }

        return expanded;
    }

    /// <summary>
    /// Breaks a page body into atoms without losing content: each block the atom regex matches
    /// becomes an atom, and anything between matches is kept as an atom of its own. Only gaps
    /// that carry no text once tags are stripped — list wrappers, whitespace — are discarded.
    /// </summary>
    private static List<string> SplitIntoAtoms(string body)
    {
        var atoms = new List<string>();
        var cursor = 0;

        foreach (Match atom in HtmlAtomRegex.Matches(body))
        {
            AddIfMeaningful(atoms, body[cursor..atom.Index]);
            atoms.Add(atom.Value);
            cursor = atom.Index + atom.Length;
        }

        AddIfMeaningful(atoms, body[cursor..]);
        return atoms;
    }

    private static void AddIfMeaningful(List<string> atoms, string residue)
    {
        if (TagRegex.Replace(residue, string.Empty).Trim().Length == 0)
            return;

        // Wrapped so the exporters, which only read block elements, still carry the text across.
        atoms.Add($"<p>{residue.Trim()}</p>");
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

    /// <summary>
    /// Greedy packing verified against the exported size: add a unit, and if the pack no longer
    /// fits once exported, close it and start the next pack with that unit. Units are already
    /// known to fit individually, so every pack is guaranteed to fit.
    /// </summary>
    private static List<List<ContentUnit>> PackUnits(
        IReadOnlyList<ContentUnit> units,
        long sizeLimitBytes,
        PartSizeProbe measure)
    {
        var packs = new List<List<ContentUnit>>();
        var current = new List<ContentUnit>();

        foreach (var unit in units)
        {
            current.Add(unit);

            if (current.Count == 1 || measure.SizeOfPack(current) <= sizeLimitBytes)
                continue;

            current.RemoveAt(current.Count - 1);
            packs.Add(current);
            current = new List<ContentUnit> { unit };
        }

        if (current.Count > 0)
            packs.Add(current);

        return packs;
    }

    private static string WrapUnitContent(string content, bool isHtml) =>
        isHtml ? WrapHtml(content) : content;

    private static string WrapHtml(string bodyInnerSections) =>
        "<!DOCTYPE html><html lang=\"en\"><head><meta charset=\"utf-8\" /><title>Converted document part</title></head><body>"
        + bodyInnerSections
        + "</body></html>";

    /// <summary>
    /// Sizes a candidate part in the delivered format. Each call is a real export, so packing
    /// measures whole packs rather than re-measuring after every byte.
    /// </summary>
    private sealed class PartSizeProbe
    {
        private readonly bool _isHtml;
        private readonly ExportedSizeProbe _probe;

        public PartSizeProbe(bool isHtml, ExportedSizeProbe probe)
        {
            _isHtml = isHtml;
            _probe = probe;
        }

        /// <summary>The converted document is already a complete part, so it needs no wrapping.</summary>
        public long SizeOfWholeDocument(byte[] convertedContent) => _probe(convertedContent);

        public long SizeOf(string body) =>
            _probe(Encoding.UTF8.GetBytes(WrapUnitContent(body, _isHtml)));

        public long SizeOfPack(IEnumerable<ContentUnit> pack) =>
            SizeOf(string.Concat(pack.Select(u => u.Content)));
    }

    private sealed record ContentUnit(string Marker, string Content);
}
