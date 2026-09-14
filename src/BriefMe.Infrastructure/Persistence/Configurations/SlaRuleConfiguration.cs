using BriefMe.Domain.Entities;
using BriefMe.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BriefMe.Infrastructure.Persistence.Configurations;

public class SlaRuleConfiguration : IEntityTypeConfiguration<SlaRule>
{
    public void Configure(EntityTypeBuilder<SlaRule> builder)
    {
        builder.ToTable("SlaRules");

        builder.HasKey(r => r.Id);

        // Category (RF040): logical FK, null meaning "any category". The physical constraint arrives with
        // BRF-059 (Categories table), same as Briefings.CategoryId.
        builder.Property(r => r.CategoryId);

        // Enum persisted as string for readability in direct database queries (BRF-002); nullable here,
        // meaning "any priority".
        builder.Property(r => r.Priority)
            .HasConversion<string>()
            .HasMaxLength(20);

        // The table stores hours; the domain handles the value object (BRF-041).
        builder.Property(r => r.Deadline)
            .HasConversion(
                deadline => deadline.Hours,
                hours => SlaDeadline.FromPersistedValue(hours))
            .HasColumnName("DeadlineHours")
            .IsRequired();

        builder.Property(r => r.CreatedAt).IsRequired();
        builder.Property(r => r.CreatedBy).HasMaxLength(100);
        builder.Property(r => r.UpdatedBy).HasMaxLength(100);

        // One scope, one rule. AreNullsDistinct(false) emits UNIQUE NULLS NOT DISTINCT (PostgreSQL 15+):
        // under the default, every NULL counts as its own value and any number of global rules — the very
        // rows that must be unique — would slip through.
        builder.HasIndex(r => new { r.CategoryId, r.Priority })
            .IsUnique()
            .AreNullsDistinct(false)
            .HasDatabaseName("UX_SlaRules_CategoryId_Priority");
    }
}
