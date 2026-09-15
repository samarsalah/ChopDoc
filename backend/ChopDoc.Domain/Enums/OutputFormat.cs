namespace ChopDoc.Domain.Enums;

/// <summary>
/// Supported conversion targets. Assessment converts PDF → HTML.
/// Extra values can be added later without changing the job pipeline (OCP).
/// </summary>
public enum OutputFormat
{
    /// <summary>Used when the client requested an unsupported/unknown format (job still persisted).</summary>
    Unspecified = 0,
    Html = 1
}
