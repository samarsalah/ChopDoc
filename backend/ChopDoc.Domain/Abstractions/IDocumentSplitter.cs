using ChopDoc.Domain.Models;

namespace ChopDoc.Domain.Abstractions;

public interface IDocumentSplitter
{
    /// <summary>
    /// Returns a single part when under the limit; otherwise ordered parts.
    /// Throws UnsplittableContentException when a unit cannot be split below the limit.
    /// </summary>
    IReadOnlyList<SplitPartContent> SplitIfNeeded(
        byte[] convertedContent,
        string baseFileName,
        string fileExtension,
        long sizeLimitBytes);
}
