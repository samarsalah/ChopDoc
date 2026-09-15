namespace ChopDoc.Domain.Enums;

public enum JobStatus
{
    Queued = 0,
    Converting = 1,
    Splitting = 2,
    Validating = 3,
    Completed = 4,
    Failed = 5,
    NeedsReview = 6
}
