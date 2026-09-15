using ChopDoc.Application.DTOs;
using ChopDoc.Domain.Entities;

namespace ChopDoc.Application.Mapping;

internal static class JobMapping
{
    public static JobSummaryDto ToSummary(DocumentJob job) =>
        new(
            job.Id,
            job.OriginalFileName,
            job.RequestedOutputFormat.ToString(),
            job.Status.ToString(),
            job.CreatedAtUtc,
            job.UpdatedAtUtc,
            job.ErrorCode,
            job.ErrorMessage,
            job.Parts.Count);

    public static JobDetailDto ToDetail(DocumentJob job) =>
        new(
            job.Id,
            job.OriginalFileName,
            job.RequestedOutputFormat.ToString(),
            job.SizeLimitBytes,
            job.Status.ToString(),
            job.CreatedAtUtc,
            job.UpdatedAtUtc,
            job.CompletedAtUtc,
            job.ErrorCode,
            job.ErrorMessage,
            job.Parts
                .OrderBy(p => p.PartNumber)
                .Select(p => new JobPartDto(
                    p.Id,
                    p.PartNumber,
                    p.TotalParts,
                    p.SequenceLabel,
                    p.FileName,
                    p.SizeBytes))
                .ToList(),
            job.History
                .OrderBy(h => h.OccurredAtUtc)
                .Select(h => new JobHistoryDto(
                    h.Id,
                    h.Status.ToString(),
                    h.Message,
                    h.OccurredAtUtc))
                .ToList());
}
