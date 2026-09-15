using ChopDoc.Domain.Enums;
using ChopDoc.Domain.Exceptions;

namespace ChopDoc.Domain.Entities;

public class DocumentJob
{
    private readonly List<DocumentPart> _parts = new();
    private readonly List<JobHistoryEntry> _history = new();

    private DocumentJob()
    {
        // EF Core
    }

    public DocumentJob(
        string originalFileName,
        string storedSourcePath,
        OutputFormat requestedOutputFormat,
        long sizeLimitBytes)
    {
        if (string.IsNullOrWhiteSpace(originalFileName))
            throw new ArgumentException("Original file name is required.", nameof(originalFileName));

        if (string.IsNullOrWhiteSpace(storedSourcePath))
            throw new ArgumentException("Stored source path is required.", nameof(storedSourcePath));

        if (sizeLimitBytes <= 0)
            throw new ArgumentOutOfRangeException(nameof(sizeLimitBytes), "Size limit must be greater than zero.");

        Id = Guid.NewGuid();
        OriginalFileName = originalFileName.Trim();
        StoredSourcePath = storedSourcePath;
        RequestedOutputFormat = requestedOutputFormat;
        SizeLimitBytes = sizeLimitBytes;
        Status = JobStatus.Queued;
        CreatedAtUtc = DateTime.UtcNow;
        UpdatedAtUtc = CreatedAtUtc;

        AddHistory(JobStatus.Queued, "Job accepted and queued.");
    }

    public Guid Id { get; private set; }
    public string OriginalFileName { get; private set; } = string.Empty;
    public string StoredSourcePath { get; private set; } = string.Empty;
    public OutputFormat RequestedOutputFormat { get; private set; }
    public long SizeLimitBytes { get; private set; }
    public JobStatus Status { get; private set; }
    public string? ErrorCode { get; private set; }
    public string? ErrorMessage { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }
    public DateTime? CompletedAtUtc { get; private set; }

    public IReadOnlyCollection<DocumentPart> Parts => _parts.AsReadOnly();
    public IReadOnlyCollection<JobHistoryEntry> History => _history.AsReadOnly();

    public void MarkConverting() => TransitionTo(JobStatus.Converting, "Conversion started.");

    public void MarkSplitting() => TransitionTo(JobStatus.Splitting, "Size-based splitting started.");

    public void MarkValidating() => TransitionTo(JobStatus.Validating, "Output validation started.");

    public void MarkCompleted()
    {
        TransitionTo(JobStatus.Completed, "Job completed successfully.");
        CompletedAtUtc = DateTime.UtcNow;
        ErrorCode = null;
        ErrorMessage = null;
    }

    public void MarkFailed(DomainException exception)
    {
        ErrorCode = exception.ErrorCode;
        ErrorMessage = exception.Message;
        TransitionTo(JobStatus.Failed, exception.Message);
        CompletedAtUtc = DateTime.UtcNow;
    }

    public void MarkFailed(string errorCode, string message)
    {
        ErrorCode = errorCode;
        ErrorMessage = message;
        TransitionTo(JobStatus.Failed, message);
        CompletedAtUtc = DateTime.UtcNow;
    }

    public void MarkNeedsReview(DomainException exception)
    {
        ErrorCode = exception.ErrorCode;
        ErrorMessage = exception.Message;
        TransitionTo(JobStatus.NeedsReview, exception.Message);
        CompletedAtUtc = DateTime.UtcNow;
    }

    public void ReplaceParts(IEnumerable<DocumentPart> parts)
    {
        _parts.Clear();
        foreach (var part in parts)
            _parts.Add(part);
        Touch();
    }

    private void TransitionTo(JobStatus next, string message)
    {
        Status = next;
        Touch();
        AddHistory(next, message);
    }

    private void AddHistory(JobStatus status, string message)
    {
        _history.Add(new JobHistoryEntry(Id, status, message));
    }

    private void Touch() => UpdatedAtUtc = DateTime.UtcNow;
}
