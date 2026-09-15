using ChopDoc.Domain.Enums;
using ChopDoc.Domain.Models;

namespace ChopDoc.Domain.Abstractions;

/// <summary>
/// Exports validated HTML parts into the user-requested output format.
/// </summary>
public interface IOutputExporter
{
    bool Supports(OutputFormat format);

    ExportResult Export(
        byte[] htmlContent,
        string baseFileName,
        int partNumber,
        int totalParts,
        OutputFormat format);
}
