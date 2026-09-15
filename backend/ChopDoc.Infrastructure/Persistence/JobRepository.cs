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
        var existingHistoryIds = await _db.Set<JobHistoryEntry>()
            .AsNoTracking()
            .Where(h => h.JobId == job.Id)
            .Select(h => h.Id)
            .ToListAsync(cancellationToken);

        var existingPartIds = await _db.Set<DocumentPart>()
            .AsNoTracking()
            .Where(p => p.JobId == job.Id)
            .Select(p => p.Id)
            .ToListAsync(cancellationToken);

        if (_db.Entry(job).State == EntityState.Detached)
            _db.Jobs.Attach(job);

        // Mark scalar job fields dirty (status, errors, timestamps).
        var jobEntry = _db.Entry(job);
        jobEntry.Property(j => j.Status).IsModified = true;
        jobEntry.Property(j => j.ErrorCode).IsModified = true;
        jobEntry.Property(j => j.ErrorMessage).IsModified = true;
        jobEntry.Property(j => j.UpdatedAtUtc).IsModified = true;
        jobEntry.Property(j => j.CompletedAtUtc).IsModified = true;

        foreach (var history in job.History)
        {
            if (existingHistoryIds.Contains(history.Id))
                continue;

            // Force INSERT for new history — never UPDATE (avoids concurrency 0-row errors).
            var historyEntry = _db.Entry(history);
            if (historyEntry.State == EntityState.Detached)
                _db.Set<JobHistoryEntry>().Add(history);
            else
                historyEntry.State = EntityState.Added;
        }

        var currentPartIds = job.Parts.Select(p => p.Id).ToHashSet();
        foreach (var oldPartId in existingPartIds.Where(id => !currentPartIds.Contains(id)))
        {
            var oldPart = await _db.Set<DocumentPart>().FindAsync(new object[] { oldPartId }, cancellationToken);
            if (oldPart is not null)
                _db.Set<DocumentPart>().Remove(oldPart);
        }

        foreach (var part in job.Parts)
        {
            if (existingPartIds.Contains(part.Id))
                continue;

            var partEntry = _db.Entry(part);
            if (partEntry.State == EntityState.Detached)
                _db.Set<DocumentPart>().Add(part);
            else
                partEntry.State = EntityState.Added;
        }

        await _db.SaveChangesAsync(cancellationToken);
    }
}
