using BriefMe.Domain.Entities;
using BriefMe.Domain.Enums;
using BriefMe.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace BriefMe.Infrastructure.Tests.Persistence;

/// <summary>
/// <c>RegisterCommentCommandHandler</c> (BRF-030) writes a comment by adding it to the <c>Comments</c>
/// collection of a tracked task and calling <c>SaveChanges</c> — no repository of its own, and without ever
/// loading the collection first (<c>Comment</c> is a child of the <c>BriefingTask</c> aggregate, BRF-024).
/// These tests pin that premise: with mocked repositories the handler tests would pass either way.
/// </summary>
public class CommentPersistenceTests
{
    private static AppDbContext CreateContext(string databaseName)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName)
            .Options;

        return new AppDbContext(options);
    }

    private static BriefingTask NewTask()
    {
        var task = BriefingTask.Create(Guid.NewGuid(), "Key visual", Priority.Medium);
        task.CreatedAt = DateTime.UtcNow;

        return task;
    }

    [Fact]
    public async Task SaveChanges_WhenCommentIsAddedToATrackedTask_PersistsTheComment()
    {
        // Arrange
        var databaseName = Guid.NewGuid().ToString();
        var authorId = Guid.NewGuid();
        Guid taskId;

        await using (var seedContext = CreateContext(databaseName))
        {
            var seeded = NewTask();
            seedContext.Tasks.Add(seeded);
            await seedContext.SaveChangesAsync();
            taskId = seeded.Id;
        }

        await using var context = CreateContext(databaseName);

        // The comments collection is deliberately not included — this is how the handler loads the task.
        var task = await context.Tasks.FirstAsync(t => t.Id == taskId);

        // Act
        task.AddComment(authorId, "  Primeira versão amanhã.  ");
        await context.SaveChangesAsync();

        // Assert
        await using var readContext = CreateContext(databaseName);
        var persisted = await readContext.Tasks
            .Include(t => t.Comments)
            .FirstAsync(t => t.Id == taskId);

        persisted.Comments.Should().ContainSingle();
        persisted.Comments.Single().TaskId.Should().Be(taskId);
        persisted.Comments.Single().AuthorId.Should().Be(authorId);
        persisted.Comments.Single().Text.Should().Be("Primeira versão amanhã.");
    }

    [Fact]
    public async Task SaveChanges_WhenTwoCommentsAreAdded_PersistsBoth()
    {
        // Arrange
        var databaseName = Guid.NewGuid().ToString();
        await using var context = CreateContext(databaseName);
        var task = NewTask();
        context.Tasks.Add(task);
        await context.SaveChangesAsync();

        // Act
        task.AddComment(Guid.NewGuid(), "Primeiro.");
        task.AddComment(Guid.NewGuid(), "Segundo.");
        await context.SaveChangesAsync();

        // Assert
        await using var readContext = CreateContext(databaseName);
        var persisted = await readContext.Tasks
            .Include(t => t.Comments)
            .FirstAsync(t => t.Id == task.Id);

        persisted.Comments.Should().HaveCount(2);
    }
}
