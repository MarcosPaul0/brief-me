using BriefMe.Domain.Entities;
using BriefMe.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BriefMe.Infrastructure.Persistence.Configurations;

public class BriefingConfiguration : IEntityTypeConfiguration<Briefing>
{
    public void Configure(EntityTypeBuilder<Briefing> builder)
    {
        builder.ToTable("Briefings");

        builder.HasKey(b => b.Id);

        builder.Property(b => b.Title)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(b => b.Description)
            .HasMaxLength(4000);

        builder.Property(b => b.Objective)
            .HasMaxLength(4000);

        builder.Property(b => b.RequestingArea)
            .HasMaxLength(200);

        // Calendar date, not an instant: 'date' avoids the timezone conversion that 'timestamptz' demands.
        builder.Property(b => b.DesiredDeliveryDate).HasColumnType("date");

        // Enums persisted as strings for readability in direct database queries (BRF-002).
        builder.Property(b => b.Priority)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(b => b.Status)
            .HasConversion<string>()
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(b => b.CategoryId).IsRequired();
        builder.Property(b => b.RequesterId).IsRequired();

        // Approval decision (BRF-015). The converter only runs for non-null values, hence the suppression.
        builder.Property(b => b.DecisionJustification)
            .HasConversion(
                justification => justification!.Value,
                value => Justification.FromPersistedValue(value))
            .HasMaxLength(Justification.MaxLength);

        builder.Property(b => b.ReviewedById);
        builder.Property(b => b.ReviewedAt);

        // Instant, not a date: it records when the monitor detected the breach (RF028), unlike
        // DesiredDeliveryDate.
        builder.Property(b => b.OverdueSince);

        // Derived from OverdueSince; a column of its own would be a second axis of the same state.
        builder.Ignore(b => b.IsOverdue);

        builder.Property(b => b.CreatedAt).IsRequired();
        builder.Property(b => b.CreatedBy).HasMaxLength(100);
        builder.Property(b => b.UpdatedBy).HasMaxLength(100);

        // Requester: physical FK to Users, no navigation property (aggregate boundary).
        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(b => b.RequesterId)
            .OnDelete(DeleteBehavior.Restrict);

        // Category (RF040): logical FK. The physical constraint arrives with BRF-059 (Categories table).
        // For now just the index, without HasOne, to keep the migration self-contained.

        builder.HasMany(b => b.Attachments)
            .WithOne()
            .HasForeignKey(a => a.BriefingId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(b => b.Attachments).UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasIndex(b => b.RequesterId).HasDatabaseName("IX_Briefings_RequesterId");
        builder.HasIndex(b => b.Status).HasDatabaseName("IX_Briefings_Status");
        builder.HasIndex(b => b.CategoryId).HasDatabaseName("IX_Briefings_CategoryId");

        // Serves both sides of the overdue flag: IS NULL for the monitor's sweep (RF028) and IS NOT NULL for
        // the overdue list of RF030 (BRF-045).
        builder.HasIndex(b => b.OverdueSince).HasDatabaseName("IX_Briefings_OverdueSince");
    }
}
