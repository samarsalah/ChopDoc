using ChopDoc.Domain.Abstractions;
using ChopDoc.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace ChopDoc.Infrastructure.Persistence;

public sealed class JobRepository : IJobRepository
{
    private readonly ChopDocDbContext _db;

    public JobRepository(ChopDocDbContext db)
    {
        _db = db;
    }

    public async Task AddAsync(DocumentJob job, CancellationToken cancellationToken = default)
    {
        _db.Jobs.Add(job);
        await _db.SaveChangesAsync(cancellationToken);
    }

    public Task<DocumentJob?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        _db.Jobs
            .Include(j => j.Parts)
            .Include(j => j.History)
            .AsSplitQuery()
            .FirstOrDefaultAsync(j => j.Id == id, cancellationToken);

    public async Task<IReadOnlyList<DocumentJob>> ListAsync(CancellationToken cancellationToken = default) =>
        await _db.Jobs
            .Include(j => j.Parts)
            .AsNoTracking()
            .OrderByDescending(j => j.CreatedAtUtc)
            .ToListAsync(cancellationToken);

    public async Task UpdateAsync(DocumentJob job, CancellationToken cancellationToken = default)
    {
        _db.Jobs.Update(job);
        await _db.SaveChangesAsync(cancellationToken);
    }
}
