namespace ChopDoc.Domain.Models;

/// <summary>
/// In-memory split unit before persistence. Content carries a stable marker used by validation.
/// </summary>
public sealed record SplitPartContent(
    int PartNumber,
    int TotalParts,
    byte[] Content,
    string FileName,
    string ContentMarker)
{
    /// <summary>Marker used when the whole converted document fits in one part.</summary>
    public const string FullDocumentMarker = "full-document";
}
