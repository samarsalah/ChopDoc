using System.Text;
using System.Text.RegularExpressions;
using ChopDoc.Domain.Abstractions;
using ChopDoc.Domain.Models;

namespace ChopDoc.Infrastructure.Processing;

public sealed class DocumentOutputValidator : IOutputValidator
{
    private static readonly StringComparer MarkerComparer = StringComparer.OrdinalIgnoreCase;

    private static readonly Regex HtmlMarkerRegex = new(
        @"data-chopdoc-marker\s*=\s*""(?<marker>[^""]+)""",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex TextMarkerRegex = new(
        @"===CHOPDOC:(?<marker>[^=\r\n]+)===",
        RegexOptions.Compiled);

    public ValidationResult ValidateStructure(
        IReadOnlyList<SplitPartContent> parts,
        IReadOnlyCollection<string> expectedMarkers)
    {
        if (parts is null || parts.Count == 0)
            return ValidationResult.Failure("No output parts were produced.");

        var sequence = ValidateSequence(parts.Select(p => (p.PartNumber, p.TotalParts)).ToList());
        if (!sequence.IsValid)
            return sequence;

        var seenMarkers = new HashSet<string>(MarkerComparer);

        foreach (var part in parts.OrderBy(p => p.PartNumber))
        {
            if (part.Content is null || part.Content.Length == 0)
                return ValidationResult.Failure($"Part {part.PartNumber} has empty content.");

            var markersInPart = ExtractMarkers(Encoding.UTF8.GetString(part.Content));

            // The single-part path carries the whole converted document, so it declares no
            // section list to cross-check. The coverage check below still proves nothing was lost.
            if (part.ContentMarker != SplitPartContent.FullDocumentMarker)
            {
                var declaredMismatch = ValidateDeclaredMarkers(part, markersInPart);
                if (!declaredMismatch.IsValid)
                    return declaredMismatch;
            }

            foreach (var marker in markersInPart)
            {
                if (!seenMarkers.Add(marker))
                    return ValidationResult.Failure($"Duplicate content marker '{marker}' across parts.");
            }
        }

        return ValidateCoverage(expectedMarkers, seenMarkers);
    }

    public ValidationResult ValidateExportedParts(
        IReadOnlyList<ExportedPart> parts,
        long sizeLimitBytes)
    {
        if (parts is null || parts.Count == 0)
            return ValidationResult.Failure("No output parts were produced.");

        if (sizeLimitBytes <= 0)
            return ValidationResult.Failure("Size limit must be greater than zero.");

        var sequence = ValidateSequence(parts.Select(p => (p.PartNumber, p.TotalParts)).ToList());
        if (!sequence.IsValid)
            return sequence;

        foreach (var part in parts.OrderBy(p => p.PartNumber))
        {
            if (part.Content is null || part.Content.Length == 0)
                return ValidationResult.Failure($"Part {part.PartNumber} has empty content.");

            if (part.Content.LongLength > sizeLimitBytes)
            {
                return ValidationResult.Failure(
                    $"Part {part.PartNumber} is {part.Content.LongLength} bytes in the requested output format, exceeding limit {sizeLimitBytes}.");
            }
        }

        return ValidationResult.Success();
    }

    private static ValidationResult ValidateSequence(IReadOnlyList<(int PartNumber, int TotalParts)> parts)
    {
        var expectedTotal = parts[0].TotalParts;
        if (parts.Count != expectedTotal)
            return ValidationResult.Failure($"Expected {expectedTotal} parts but found {parts.Count}.");

        if (parts.Any(p => p.TotalParts != expectedTotal))
            return ValidationResult.Failure("Inconsistent TotalParts values across output parts.");

        for (var i = 1; i <= expectedTotal; i++)
        {
            var matches = parts.Count(p => p.PartNumber == i);
            if (matches == 0)
                return ValidationResult.Failure($"Missing part number {i}.");
            if (matches > 1)
                return ValidationResult.Failure($"Duplicate part number {i}.");
        }

        return ValidationResult.Success();
    }

    private static ValidationResult ValidateDeclaredMarkers(SplitPartContent part, List<string> markersInPart)
    {
        var declared = part.ContentMarker
            .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        if (declared.Length == 0)
            return ValidationResult.Failure($"Part {part.PartNumber} has no content markers.");

        if (!markersInPart.SequenceEqual(declared, MarkerComparer)
            && !declared.All(d => markersInPart.Contains(d, MarkerComparer)))
        {
            return ValidationResult.Failure(
                $"Part {part.PartNumber} marker mismatch between metadata and content.");
        }

        return ValidationResult.Success();
    }

    /// <summary>
    /// Proves nothing was lost: every section the converter produced has to appear somewhere in
    /// the output. Without this the validator would only confirm that the parts it was handed
    /// are internally consistent, which stays true even if the splitter dropped a page.
    /// </summary>
    private static ValidationResult ValidateCoverage(
        IReadOnlyCollection<string> expectedMarkers,
        IReadOnlyCollection<string> observedMarkers)
    {
        if (expectedMarkers is null || expectedMarkers.Count == 0)
            return ValidationResult.Success();

        foreach (var expected in expectedMarkers)
        {
            // An oversized page is broken into atoms named "<page>#1", "<page>#2", and so on.
            var covered = observedMarkers.Any(observed =>
                MarkerComparer.Equals(observed, expected) ||
                observed.StartsWith(expected + "#", StringComparison.OrdinalIgnoreCase));

            if (!covered)
                return ValidationResult.Failure($"Source section '{expected}' is missing from the output parts.");
        }

        return ValidationResult.Success();
    }

    private static List<string> ExtractMarkers(string content)
    {
        var html = HtmlMarkerRegex.Matches(content)
            .Select(m => m.Groups["marker"].Value)
            .ToList();

        if (html.Count > 0)
            return html;

        return TextMarkerRegex.Matches(content)
            .Select(m => m.Groups["marker"].Value.Trim())
            .ToList();
    }
}
