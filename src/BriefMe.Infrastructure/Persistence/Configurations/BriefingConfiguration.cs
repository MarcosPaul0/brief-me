using BriefMe.Domain.Entities;
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

        builder.HasIndex(b => b.RequesterId).HasDatabaseName("IX_Briefings_RequesterId");
        builder.HasIndex(b => b.Status).HasDatabaseName("IX_Briefings_Status");
        builder.HasIndex(b => b.CategoryId).HasDatabaseName("IX_Briefings_CategoryId");

    }
}
