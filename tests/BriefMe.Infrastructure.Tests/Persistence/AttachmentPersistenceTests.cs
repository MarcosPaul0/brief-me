using BriefMe.Domain.Entities;
using BriefMe.Domain.Enums;
using BriefMe.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace BriefMe.Infrastructure.Tests.Persistence;

/// <summary>
/// <c>SubmitTaskDeliveryCommandHandler</c> (BRF-031) writes a delivery by adding it to the
/// <c>Attachments</c> collection of a tracked briefing and calling <c>SaveChanges</c> — no repository of its
/// own, and without ever loading the collection first (<c>Attachment</c> is a child of the <c>Briefing</c>
/// aggregate, BRF-004). These tests pin that premise: with mocked repositories the handler tests would pass
/// either way. Same shape as <c>CommentPersistenceTests</c> (BRF-030), whose finding on
/// <c>ValueGeneratedNever()</c> was deferred to this slice.
/// </summary>
public class AttachmentPersistenceTests
{
    private static AppDbContext CreateContext(string databaseName)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName)
            .Options;

        return new AppDbContext(options);
    }

    /// <summary>A briefing under execution — the only status that accepts a task delivery (RF022).</summary>
    private static Briefing NewBriefingInExecution()
    {
        var briefing = Briefing.Create("Campanha de fim de ano", Priority.Medium, Guid.NewGuid(), Guid.NewGuid());
        briefing.CreatedAt = DateTime.UtcNow;

        typeof(Briefing)
            .GetProperty(nameof(Briefing.Status))!
            .SetValue(briefing, BriefingStatus.InProgress);

        return briefing;
    }

    [Fact]
    public async Task SaveChanges_WhenADeliveryIsAddedToATrackedBriefing_PersistsTheAttachment()
    {
        // Arrange
        var databaseName = Guid.NewGuid().ToString();
        var taskId = Guid.NewGuid();
        var uploaderId = Guid.NewGuid();
        Guid briefingId;

        await using (var seedContext = CreateContext(databaseName))
        {
            var seeded = NewBriefingInExecution();
            seedContext.Briefings.Add(seeded);
            await seedContext.SaveChangesAsync();
            briefingId = seeded.Id;
        }

        await using var context = CreateContext(databaseName);

        // The attachments collection is deliberately not included — this is how the handler loads the briefing.
        var briefing = await context.Briefings.FirstAsync(b => b.Id == briefingId);

        // Act
        briefing.AddDelivery(taskId, "  key-visual.psd  ", "2026/07/29/abc.psd", 4096, uploaderId);
        await context.SaveChangesAsync();

        // Assert
        await using var readContext = CreateContext(databaseName);
        var persisted = await readContext.Briefings
            .Include(b => b.Attachments)
            .FirstAsync(b => b.Id == briefingId);

        persisted.Attachments.Should().ContainSingle();
        var delivery = persisted.Attachments.Single();
        delivery.BriefingId.Should().Be(briefingId);
        delivery.TaskId.Should().Be(taskId);
        delivery.FileName.Should().Be("key-visual.psd");
        delivery.SizeBytes.Should().Be(4096);
        delivery.UploadedById.Should().Be(uploaderId);
        delivery.UploadedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromMinutes(1));
    }

    [Fact]
    public async Task SaveChanges_WhenTwoDeliveriesAreAddedToTheSameTask_PersistsBoth()
    {
        // Arrange
        var databaseName = Guid.NewGuid().ToString();
        var taskId = Guid.NewGuid();
        await using var context = CreateContext(databaseName);
        var briefing = NewBriefingInExecution();
        context.Briefings.Add(briefing);
        await context.SaveChangesAsync();

        // Act
        briefing.AddDelivery(taskId, "v1.psd", "2026/07/29/a.psd", 4096, Guid.NewGuid());
        briefing.AddDelivery(taskId, "v2.psd", "2026/07/29/b.psd", 8192, Guid.NewGuid());
        await context.SaveChangesAsync();

        // Assert
        await using var readContext = CreateContext(databaseName);
        var persisted = await readContext.Briefings
            .Include(b => b.Attachments)
            .FirstAsync(b => b.Id == briefing.Id);

        persisted.Attachments.Should().HaveCount(2);
    }

    [Fact]
    public async Task SaveChanges_WhenABriefingAttachmentIsAdded_PersistsItWithoutATask()
    {
        // Arrange — RF005 attachments reach the context the same way and inherit the same fix (BRF-006).
        var databaseName = Guid.NewGuid().ToString();
        await using var context = CreateContext(databaseName);
        var briefing = Briefing.Create("Campanha de fim de ano", Priority.Medium, Guid.NewGuid(), Guid.NewGuid());
        briefing.CreatedAt = DateTime.UtcNow;
        context.Briefings.Add(briefing);
        await context.SaveChangesAsync();

        // Act
        briefing.AddAttachment("brief.pdf", "2026/07/29/c.pdf", 1024, Guid.NewGuid());
        await context.SaveChangesAsync();

        // Assert
        await using var readContext = CreateContext(databaseName);
        var persisted = await readContext.Briefings
            .Include(b => b.Attachments)
            .FirstAsync(b => b.Id == briefing.Id);

        persisted.Attachments.Should().ContainSingle().Which.TaskId.Should().BeNull();
    }
}
