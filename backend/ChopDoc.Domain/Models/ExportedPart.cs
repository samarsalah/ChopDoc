namespace ChopDoc.Domain.Models;

/// <summary>
/// An output part in its final format, held in memory so it can be validated before anything
/// is written to storage.
/// </summary>
public sealed record ExportedPart(
    int PartNumber,
    int TotalParts,
    byte[] Content,
    string FileName);
