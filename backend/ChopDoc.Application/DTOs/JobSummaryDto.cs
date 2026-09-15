namespace ChopDoc.Application.DTOs;

public sealed record JobSummaryDto(
    Guid Id,
    string OriginalFileName,
    string OutputFormat,
    string Status,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc,
    string? ErrorCode,
    string? ErrorMessage,
    int PartCount);
