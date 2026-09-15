using ChopDoc.Domain.Entities;

namespace ChopDoc.Domain.Abstractions;

public interface IJobRepository
{
    Task AddAsync(DocumentJob job, CancellationToken cancellationToken = default);

    Task<DocumentJob?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<DocumentJob>> ListAsync(CancellationToken cancellationToken = default);

    Task UpdateAsync(DocumentJob job, CancellationToken cancellationToken = default);
}
