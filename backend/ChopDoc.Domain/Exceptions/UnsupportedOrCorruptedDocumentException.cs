namespace ChopDoc.Domain.Exceptions;

public sealed class UnsupportedOrCorruptedDocumentException : DomainException
{
    public UnsupportedOrCorruptedDocumentException(string details)
        : base(
            "UNSUPPORTED_OR_CORRUPTED_INPUT",
            $"The source document is unsupported or corrupted: {details}")
    {
    }
}
