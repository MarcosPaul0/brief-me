using BriefMe.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BriefMe.Infrastructure.Persistence.Configurations;

public class BriefingAttachmentConfiguration : IEntityTypeConfiguration<BriefingAttachment>
{
    public void Configure(EntityTypeBuilder<BriefingAttachment> builder)
    {
        builder.ToTable("BriefingAttachments");

        builder.HasKey(a => a.Id);

        // The domain assigns the id. Without this, EF treats a set Guid key as "already persisted" and an
        // attachment added to a tracked briefing would be saved as an UPDATE of a row that does not exist.
        builder.Property(a => a.Id).ValueGeneratedNever();

        builder.Property(a => a.BriefingId).IsRequired();

        builder.Property(a => a.FileName)
            .HasMaxLength(BriefingAttachment.FileNameMaxLength)
            .IsRequired();

        builder.Property(a => a.StorageKey)
            .HasMaxLength(300)
            .IsRequired();

        builder.Property(a => a.ContentType)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(a => a.Kind)
            .HasConversion<string>()
            .HasMaxLength(10)
            .IsRequired();

        builder.Property(a => a.SizeBytes).IsRequired();
        builder.Property(a => a.UploadedAt).IsRequired();

        builder.HasIndex(a => a.BriefingId).HasDatabaseName("IX_BriefingAttachments_BriefingId");
        builder.HasIndex(a => a.StorageKey).IsUnique().HasDatabaseName("UX_BriefingAttachments_StorageKey");
    }
}
