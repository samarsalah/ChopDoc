namespace ChopDoc.Api.Contracts;

public sealed class SubmitJobForm
{
    public IFormFile? File { get; set; }

    public string OutputFormat { get; set; } = "Html";

    public double? SizeLimitMb { get; set; }
}
