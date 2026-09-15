using ChopDoc.Domain.Enums;
using ChopDoc.Domain.Models;

namespace ChopDoc.Domain.Abstractions;

public interface IDocumentConverter
{
    Task<ConversionResult> ConvertAsync(
        Stream sourcePdf,
        OutputFormat outputFormat,
        CancellationToken cancellationToken = default);
}
