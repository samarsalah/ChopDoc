namespace ChopDoc.Domain.Exceptions;

public sealed class OutputValidationException : DomainException
{
    public OutputValidationException(string details)
        : base(
            "OUTPUT_VALIDATION_FAILED",
            $"Output validation failed: {details}")
    {
    }
}
