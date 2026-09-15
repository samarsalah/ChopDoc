using ChopDoc.Application.DTOs;

namespace ChopDoc.Application.Services;

public interface IDocumentJobService
{
    Task<JobDetailDto> SubmitAsync(SubmitJobRequest request, CancellationToken cancellationToken = default);

    Task<JobDetailDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<JobSummaryDto>> ListAsync(CancellationToken cancellationToken = default);

    Task<(Stream Content, string FileName, string ContentType)?> OpenPartAsync(
        Guid jobId,
        Guid partId,
        CancellationToken cancellationToken = default);
}
