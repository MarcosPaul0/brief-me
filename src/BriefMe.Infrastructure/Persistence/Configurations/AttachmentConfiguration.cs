using BriefMe.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BriefMe.Infrastructure.Persistence.Configurations;

public class AttachmentConfiguration : IEntityTypeConfiguration<Attachment>
{
    public void Configure(EntityTypeBuilder<Attachment> builder)
    {
        builder.ToTable("Attachments");

        builder.HasKey(a => a.Id);

        // The id comes from Attachment.Create/CreateDelivery, never from the store. Saying so is what makes an
        // attachment added to an already-loaded briefing (BRF-031) be tracked as Added: under the Guid
        // convention (ValueGeneratedOnAdd) change detection reads the filled key as "this row exists" and
        // issues an UPDATE that matches nothing. Roots do not hit this — they are added explicitly through the
        // repository — but a child of an aggregate reaches the context only through its parent's collection.
        // Same finding as CommentConfiguration (BRF-030); no schema change, so no migration.
        builder.Property(a => a.Id).ValueGeneratedNever();

        builder.Property(a => a.BriefingId).IsRequired();

        builder.Property(a => a.FileName)
            .HasMaxLength(Attachment.MaxFileNameLength)
            .IsRequired();

        builder.Property(a => a.StoragePath)
            .HasMaxLength(1024)
            .IsRequired();

        builder.Property(a => a.SizeBytes).IsRequired();

        builder.Property(a => a.UploadedById).IsRequired();

        builder.Property(a => a.UploadedAt).IsRequired();

        // Task delivery link (RF022, BRF-021): null on briefing attachments. SetNull, not Cascade — the file
        // belongs to the briefing, so losing the task must not erase the delivery.
        builder.Property(a => a.TaskId);

        builder.HasOne<BriefingTask>()
            .WithMany()
            .HasForeignKey(a => a.TaskId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(a => a.BriefingId).HasDatabaseName("IX_Attachments_BriefingId");
        builder.HasIndex(a => a.TaskId).HasDatabaseName("IX_Attachments_TaskId");
    }
}
