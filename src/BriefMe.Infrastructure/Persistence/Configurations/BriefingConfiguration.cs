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
        builder.Property(b => b.DesignerId);

        builder.Property(b => b.DeliveryNote).HasMaxLength(Briefing.DeliveryNoteMaxLength);
        builder.Property(b => b.DeliveredAt);

        // The converter only runs for non-null values, hence the suppression.
        builder.Property(b => b.ChangeRequestReason)
            .HasConversion(
                justification => justification!.Value,
                value => Justification.FromPersistedValue(value))
            .HasMaxLength(Justification.MaxLength);

        builder.Ignore(b => b.IsOpenForRequester);

        // Optimistic concurrency on Postgres' xmin system column: two designers claiming the same briefing
        // load the same version, and only the first commit matches it (Npgsql maps a uint row version to xmin,
        // so no column is added).
        builder.Property<uint>("xmin").IsRowVersion();

        builder.Property(b => b.CreatedAt).IsRequired();
        builder.Property(b => b.CreatedBy).HasMaxLength(100);
        builder.Property(b => b.UpdatedBy).HasMaxLength(100);

        // Requester: physical FK to Users, no navigation property (aggregate boundary).
        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(b => b.RequesterId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(b => b.DesignerId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Category>()
            .WithMany()
            .HasForeignKey(b => b.CategoryId)
            .OnDelete(DeleteBehavior.Restrict);

        // Attachments are a composition: no navigation back, deleted with the briefing, and a child dropped
        // from the collection is deleted as an orphan.
        builder.HasMany(b => b.Attachments)
            .WithOne()
            .HasForeignKey(a => a.BriefingId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(b => b.Attachments).UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasIndex(b => b.RequesterId).HasDatabaseName("IX_Briefings_RequesterId");
        builder.HasIndex(b => b.Status).HasDatabaseName("IX_Briefings_Status");
        builder.HasIndex(b => b.CategoryId).HasDatabaseName("IX_Briefings_CategoryId");

        // Serves both the designer queue (Submitted, no designer) and "my work" (by designer).
        builder.HasIndex(b => new { b.Status, b.DesignerId }).HasDatabaseName("IX_Briefings_Status_DesignerId");
    }
}
