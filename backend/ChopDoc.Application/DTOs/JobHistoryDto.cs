namespace ChopDoc.Application.DTOs;

public sealed record JobHistoryDto(
    Guid Id,
    string Status,
    string Message,
    DateTime OccurredAtUtc);
