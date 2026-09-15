namespace ChopDoc.Domain.Exceptions;

public sealed class UnsupportedOutputFormatException : DomainException
{
    public UnsupportedOutputFormatException(string format)
        : base(
            "UNSUPPORTED_OUTPUT_FORMAT",
            $"The requested output format '{format}' is not supported.")
    {
    }
}
