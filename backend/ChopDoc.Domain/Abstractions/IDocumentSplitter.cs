using ChopDoc.Domain.Models;

namespace ChopDoc.Domain.Abstractions;

public interface IDocumentSplitter
{
    /// <summary>
    /// Returns a single part when the exported output is under the limit; otherwise ordered parts.
    /// Boundaries are chosen by measuring each candidate part with <paramref name="measureExportedSize"/>.
    /// Throws UnsplittableContentException when a unit cannot be split below the limit.
    /// </summary>
    IReadOnlyList<SplitPartContent> SplitIfNeeded(
        byte[] convertedContent,
        string baseFileName,
        string fileExtension,
        long sizeLimitBytes,
        ExportedSizeProbe measureExportedSize);
}
