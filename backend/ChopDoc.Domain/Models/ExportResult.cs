namespace ChopDoc.Domain.Models;

public sealed record ExportResult(
    byte[] Content,
    string FileName,
    string ContentType,
    string FileExtension);
