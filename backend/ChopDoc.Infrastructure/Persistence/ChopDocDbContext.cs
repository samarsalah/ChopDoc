using ChopDoc.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace ChopDoc.Infrastructure.Persistence;

public sealed class ChopDocDbContext : DbContext
{
    public ChopDocDbContext(DbContextOptions<ChopDocDbContext> options)
        : base(options)
    {
    }

    public DbSet<DocumentJob> Jobs => Set<DocumentJob>();
    public DbSet<DocumentPart> Parts => Set<DocumentPart>();
    public DbSet<JobHistoryEntry> History => Set<JobHistoryEntry>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ChopDocDbContext).Assembly);
    }
}
