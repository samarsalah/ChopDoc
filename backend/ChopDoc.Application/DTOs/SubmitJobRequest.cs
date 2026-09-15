namespace ChopDoc.Application.DTOs;

public sealed class SubmitJobRequest
{
    public required Stream FileStream { get; init; }
    public required string FileName { get; init; }
    public required string OutputFormat { get; init; }
    public double? SizeLimitMb { get; init; }
}
