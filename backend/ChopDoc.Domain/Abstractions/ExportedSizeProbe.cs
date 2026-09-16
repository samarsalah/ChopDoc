namespace ChopDoc.Domain.Abstractions;

/// <summary>
/// Measures how large a candidate HTML payload becomes in the format that gets handed off.
/// The size limit applies to the exported artifact, not to the HTML intermediate, so the
/// splitter needs to measure rather than assume the two are the same size.
/// </summary>
public delegate long ExportedSizeProbe(byte[] htmlContent);
