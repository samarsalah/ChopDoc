using ChopDoc.Domain.Models;

namespace ChopDoc.Domain.Abstractions;

/// <summary>
/// Always converts PDF → HTML (canonical intermediate for split/validate).
/// </summary>
public interface IDocumentConverter
{
    Task<ConversionResult> ConvertPdfToHtmlAsync(
        Stream sourcePdf,
        CancellationToken cancellationToken = default);
}
