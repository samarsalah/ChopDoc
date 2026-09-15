namespace ChopDoc.Domain.Models;

public sealed record ValidationResult(bool IsValid, string? FailureReason)
{
    public static ValidationResult Success() => new(true, null);

    public static ValidationResult Failure(string reason) => new(false, reason);
}
