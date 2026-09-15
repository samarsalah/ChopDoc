namespace ChopDoc.Domain.Exceptions;

public sealed class ScannedDocumentException : DomainException
{
    public ScannedDocumentException()
        : base(
            "SCANNED_DOCUMENT",
            "The PDF has no extractable text layer (scanned/image-only). Rule-based conversion cannot proceed.")
    {
    }
}
