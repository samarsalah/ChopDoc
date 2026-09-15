using ChopDoc.Domain.Enums;

namespace ChopDoc.Domain.Entities;

public class JobHistoryEntry
{
    private JobHistoryEntry()
    {
        // EF Core
    }

    public JobHistoryEntry(Guid jobId, JobStatus status, string message)
    {
        Id = Guid.NewGuid();
        JobId = jobId;
        Status = status;
        Message = string.IsNullOrWhiteSpace(message) ? status.ToString() : message.Trim();
        OccurredAtUtc = DateTime.UtcNow;
    }

    public Guid Id { get; private set; }
    public Guid JobId { get; private set; }
    public JobStatus Status { get; private set; }
    public string Message { get; private set; } = string.Empty;
    public DateTime OccurredAtUtc { get; private set; }
}
