using BriefMe.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BriefMe.Infrastructure.Persistence.Configurations;

public class BriefingTaskConfiguration : IEntityTypeConfiguration<BriefingTask>
{
    public void Configure(EntityTypeBuilder<BriefingTask> builder)
    {
        // Table keeps the backlog name; only the CLR type is prefixed to dodge System.Threading.Tasks.Task.
        builder.ToTable("Tasks");

        builder.HasKey(t => t.Id);

        builder.Property(t => t.BriefingId).IsRequired();

        builder.Property(t => t.Title)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(t => t.DesignerId);

        // Calendar date, not an instant — same reasoning as Briefing.DesiredDeliveryDate.
        builder.Property(t => t.Deadline).HasColumnType("date");

        // Enums persisted as strings for readability in direct database queries (BRF-002).
        builder.Property(t => t.Priority)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(t => t.Status)
            .HasConversion<string>()
            .HasMaxLength(30)
            .IsRequired();

        // Instant, not a date: it records when the monitor detected the breach (RF028), unlike Deadline.
        builder.Property(t => t.OverdueSince);

        // Derived from OverdueSince; a column of its own would be a second axis of the same state.
        builder.Ignore(t => t.IsOverdue);

        builder.Property(t => t.CreatedAt).IsRequired();
        builder.Property(t => t.CreatedBy).HasMaxLength(100);
        builder.Property(t => t.UpdatedBy).HasMaxLength(100);

        // Briefing (RF016): physical FK, no navigation property (aggregate boundary).
        builder.HasOne<Briefing>()
            .WithMany()
            .HasForeignKey(t => t.BriefingId)
            .OnDelete(DeleteBehavior.Cascade);

        // Assigned Designer (RF017): a user holding tasks must not vanish from under them.
        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(t => t.DesignerId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(t => t.Comments)
            .WithOne()
            .HasForeignKey(c => c.TaskId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(t => t.Comments).UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasIndex(t => t.BriefingId).HasDatabaseName("IX_Tasks_BriefingId");
        builder.HasIndex(t => t.DesignerId).HasDatabaseName("IX_Tasks_DesignerId");
        builder.HasIndex(t => t.Status).HasDatabaseName("IX_Tasks_Status");

        // Serves both sides of the overdue flag: IS NULL for the monitor's sweep (RF028) and IS NOT NULL for
        // the overdue list of RF029 (BRF-044).
        builder.HasIndex(t => t.OverdueSince).HasDatabaseName("IX_Tasks_OverdueSince");
    }
}
