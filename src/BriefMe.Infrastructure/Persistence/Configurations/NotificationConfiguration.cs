using BriefMe.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BriefMe.Infrastructure.Persistence.Configurations;

public class NotificationConfiguration : IEntityTypeConfiguration<Notification>
{
    public void Configure(EntityTypeBuilder<Notification> builder)
    {
        builder.ToTable("Notifications");

        builder.HasKey(n => n.Id);

        // Logical FK to Users, deliberately without HasOne (BRF-048). A physical constraint would force a
        // choice between Restrict — making a user undeletable for having once been told something — and
        // Cascade, which erases the record of what the system said. The inbox is history, like Comment.
        builder.Property(n => n.RecipientId).IsRequired();

        // Enum persisted as string for readability in direct database queries (BRF-002).
        builder.Property(n => n.EventType)
            .HasConversion<string>()
            .HasMaxLength(30)
            .IsRequired();

        // Lengths come from the entity's own constants: with the number in one place, a limit that moves in
        // the domain produces a different column in the next migration instead of a DbUpdateException in
        // production. Same device as CommentConfiguration and Comment.MaxTextLength.
        builder.Property(n => n.Title)
            .HasMaxLength(Notification.MaxTitleLength)
            .IsRequired();

        builder.Property(n => n.Message)
            .HasMaxLength(Notification.MaxMessageLength)
            .IsRequired();

        // Stamped by Notification.Create, not by the audit interceptor: the entity is not IAuditableEntity,
        // because the author of an alert is the system reacting to an event — sometimes outside any request,
        // as in the SLA job (BRF-053), where CreatedBy would be structurally null.
        builder.Property(n => n.CreatedAt).IsRequired();

        // Null while unread; the instant of the first read once marked (BRF-047).
        builder.Property(n => n.ReadAt);

        // Derived from ReadAt; a column of its own would be free to diverge from it.
        builder.Ignore(n => n.IsRead);

        // What the bell asks for: "this person's unread ones" (BRF-054). With RecipientId leftmost, the same
        // index also serves any filter by recipient alone — the minimum BRF-048 asks for.
        builder.HasIndex(n => new { n.RecipientId, n.ReadAt })
            .HasDatabaseName("IX_Notifications_RecipientId_ReadAt");
    }
}
