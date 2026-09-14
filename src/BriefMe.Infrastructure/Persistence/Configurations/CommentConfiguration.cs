using BriefMe.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BriefMe.Infrastructure.Persistence.Configurations;

public class CommentConfiguration : IEntityTypeConfiguration<Comment>
{
    public void Configure(EntityTypeBuilder<Comment> builder)
    {
        builder.ToTable("Comments");

        builder.HasKey(c => c.Id);

        // The id comes from Comment.Create, never from the store. Saying so is what makes a comment added to
        // an already-loaded task (BRF-030) be tracked as Added: under the Guid convention (ValueGeneratedOnAdd)
        // change detection reads the filled key as "this row exists" and issues an UPDATE that matches nothing.
        // Roots do not hit this — they are added explicitly through the repository — but a child of an
        // aggregate reaches the context only through its parent's collection.
        builder.Property(c => c.Id).ValueGeneratedNever();

        builder.Property(c => c.TaskId).IsRequired();

        builder.Property(c => c.AuthorId).IsRequired();

        builder.Property(c => c.Text)
            .HasMaxLength(Comment.MaxTextLength)
            .IsRequired();

        builder.Property(c => c.CreatedAt).IsRequired();

        builder.HasIndex(c => c.TaskId).HasDatabaseName("IX_Comments_TaskId");
    }
}
