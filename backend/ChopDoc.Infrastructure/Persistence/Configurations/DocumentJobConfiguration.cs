using ChopDoc.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ChopDoc.Infrastructure.Persistence.Configurations;

public sealed class DocumentJobConfiguration : IEntityTypeConfiguration<DocumentJob>
{
    public void Configure(EntityTypeBuilder<DocumentJob> builder)
    {
        builder.ToTable("Jobs");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.OriginalFileName).HasMaxLength(260).IsRequired();
        builder.Property(x => x.StoredSourcePath).HasMaxLength(500).IsRequired();
        builder.Property(x => x.ErrorCode).HasMaxLength(100);
        builder.Property(x => x.ErrorMessage).HasMaxLength(2000);
        builder.Property(x => x.RequestedOutputFormat).HasConversion<string>().HasMaxLength(50);
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(50);

        builder.HasMany(x => x.Parts)
            .WithOne()
            .HasForeignKey(x => x.JobId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(x => x.History)
            .WithOne()
            .HasForeignKey(x => x.JobId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(x => x.Parts)
            .HasField("_parts")
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.Navigation(x => x.History)
            .HasField("_history")
            .UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
