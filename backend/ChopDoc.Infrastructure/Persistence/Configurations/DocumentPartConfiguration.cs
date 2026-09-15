using ChopDoc.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ChopDoc.Infrastructure.Persistence.Configurations;

public sealed class DocumentPartConfiguration : IEntityTypeConfiguration<DocumentPart>
{
    public void Configure(EntityTypeBuilder<DocumentPart> builder)
    {
        builder.ToTable("Parts");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.SequenceLabel).HasMaxLength(50).IsRequired();
        builder.Property(x => x.StoredPath).HasMaxLength(500).IsRequired();
        builder.Property(x => x.FileName).HasMaxLength(260).IsRequired();

        builder.HasIndex(x => new { x.JobId, x.PartNumber }).IsUnique();
    }
}
