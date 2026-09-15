namespace ChopDoc.Application.DTOs;

public sealed record JobPartDto(
    Guid Id,
    int PartNumber,
    int TotalParts,
    string SequenceLabel,
    string FileName,
    long SizeBytes);
