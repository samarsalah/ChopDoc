namespace ChopDoc.Domain.Exceptions;

public sealed class UnsplittableContentException : DomainException
{
    public UnsplittableContentException(string details)
        : base(
            "UNSPLITTABLE_CONTENT",
            $"A single content unit exceeds the size limit and cannot be split further: {details}")
    {
    }
}
