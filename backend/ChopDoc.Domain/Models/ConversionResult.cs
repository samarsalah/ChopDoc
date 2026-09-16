using ChopDoc.Domain.Enums;

namespace ChopDoc.Domain.Models;

public sealed record ConversionResult(
    byte[] Content,
    string ContentType,
    string FileExtension,
    OutputFormat Format,
    int PageCount,
    IReadOnlyList<string> PageMarkers,
    IReadOnlyList<string> Warnings);
