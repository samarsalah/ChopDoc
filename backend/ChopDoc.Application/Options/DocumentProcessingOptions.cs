namespace ChopDoc.Application.Options;

public sealed class DocumentProcessingOptions
{
    public const string SectionName = "DocumentProcessing";

    /// <summary>Default max size per output part in megabytes.</summary>
    public double DefaultSizeLimitMb { get; set; } = 2;
}
