namespace ChopDoc.Application.DTOs;

public sealed record JobDetailDto(
    Guid Id,
    string OriginalFileName,
    string OutputFormat,
    long SizeLimitBytes,
    string Status,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc,
    DateTime? CompletedAtUtc,
    string? ErrorCode,
    string? ErrorMessage,
    IReadOnlyList<JobPartDto> Parts,
    IReadOnlyList<JobHistoryDto> History);
