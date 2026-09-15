using ChopDoc.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ChopDoc.Infrastructure.Persistence.Configurations;

public sealed class JobHistoryEntryConfiguration : IEntityTypeConfiguration<JobHistoryEntry>
{
    public void Configure(EntityTypeBuilder<JobHistoryEntry> builder)
    {
        builder.ToTable("JobHistory");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(50);
        builder.Property(x => x.Message).HasMaxLength(2000).IsRequired();
    }
}
