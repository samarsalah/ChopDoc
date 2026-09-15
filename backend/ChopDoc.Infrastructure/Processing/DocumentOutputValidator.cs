using System.Text;
using System.Text.RegularExpressions;
using ChopDoc.Domain.Abstractions;
using ChopDoc.Domain.Models;

namespace ChopDoc.Infrastructure.Processing;

public sealed class DocumentOutputValidator : IOutputValidator
{
    private static readonly Regex MarkerRegex = new(
        @"data-chopdoc-marker\s*=\s*""(?<marker>[^""]+)""",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public ValidationResult Validate(IReadOnlyList<SplitPartContent> parts, long sizeLimitBytes)
    {
        if (parts is null || parts.Count == 0)
            return ValidationResult.Failure("No output parts were produced.");

        if (sizeLimitBytes <= 0)
            return ValidationResult.Failure("Size limit must be greater than zero.");

        var expectedTotal = parts[0].TotalParts;
        if (parts.Count != expectedTotal)
            return ValidationResult.Failure($"Expected {expectedTotal} parts but found {parts.Count}.");

        if (parts.Any(p => p.TotalParts != expectedTotal))
            return ValidationResult.Failure("Inconsistent TotalParts values across output parts.");

        for (var i = 1; i <= expectedTotal; i++)
        {
            var matches = parts.Where(p => p.PartNumber == i).ToList();
            if (matches.Count == 0)
                return ValidationResult.Failure($"Missing part number {i}.");
            if (matches.Count > 1)
                return ValidationResult.Failure($"Duplicate part number {i}.");
        }

        var ordered = parts.OrderBy(p => p.PartNumber).ToList();
        var seenMarkers = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var part in ordered)
        {
            if (part.Content is null || part.Content.Length == 0)
                return ValidationResult.Failure($"Part {part.PartNumber} has empty content.");

            if (part.Content.LongLength > sizeLimitBytes)
                return ValidationResult.Failure(
                    $"Part {part.PartNumber} is {part.Content.LongLength} bytes, exceeding limit {sizeLimitBytes}.");

            var html = Encoding.UTF8.GetString(part.Content);
            var markersInPart = MarkerRegex.Matches(html)
                .Select(m => m.Groups["marker"].Value)
                .ToList();

            // Single-part under-limit path uses marker "full-document" without sections.
            if (part.ContentMarker == "full-document")
                continue;

            var declared = part.ContentMarker
                .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            if (declared.Length == 0)
                return ValidationResult.Failure($"Part {part.PartNumber} has no content markers.");

            if (!markersInPart.SequenceEqual(declared, StringComparer.OrdinalIgnoreCase)
                && !declared.All(d => markersInPart.Contains(d, StringComparer.OrdinalIgnoreCase)))
            {
                return ValidationResult.Failure(
                    $"Part {part.PartNumber} marker mismatch between metadata and HTML content.");
            }

            foreach (var marker in declared)
            {
                if (!seenMarkers.Add(marker))
                    return ValidationResult.Failure($"Duplicate content marker '{marker}' across parts.");
            }
        }

        return ValidationResult.Success();
    }
}
