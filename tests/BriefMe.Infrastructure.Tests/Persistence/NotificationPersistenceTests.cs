using BriefMe.Domain.Entities;
using BriefMe.Domain.Enums;
using BriefMe.Infrastructure.Persistence;
using BriefMe.Infrastructure.Persistence.Interceptors;
using BriefMe.Infrastructure.Tests.TestDoubles;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace BriefMe.Infrastructure.Tests.Persistence;

/// <summary>
/// Pins the parts of the inbox mapping (BRF-048) the domain tests cannot see: the text and the read mark
/// surviving a round trip, the derived flag staying out of the schema, and the index the bell of BRF-054
/// will read through.
/// </summary>
public class NotificationPersistenceTests
{
    private static AppDbContext CreateContext(string databaseName, string? currentUserId = null)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName)
            .AddInterceptors(new AuditableEntitySaveChangesInterceptor(new FakeCurrentUser(currentUserId)))
            .Options;

        return new AppDbContext(options);
    }

    private static Notification CreateNotification(Guid recipientId) =>
        Notification.Create(
            recipientId,
            NotificationEventType.BriefingApproved,
            "Briefing aprovado",
            "O briefing \"Campanha de Natal\" foi aprovado pelo Gestor de Design.");

    [Fact]
    public async Task SaveChanges_WhenNotificationIsAdded_RoundTripsEveryMember()
    {
        // Arrange
        var databaseName = Guid.NewGuid().ToString();
        var recipientId = Guid.NewGuid();
        var notification = CreateNotification(recipientId);

        await using (var context = CreateContext(databaseName))
        {
            context.Notifications.Add(notification);
            await context.SaveChangesAsync();
        }

        // Act
        await using var readContext = CreateContext(databaseName);
        var persisted = await readContext.Notifications.FirstAsync(n => n.Id == notification.Id);

        // Assert
        persisted.RecipientId.Should().Be(recipientId);
        persisted.EventType.Should().Be(NotificationEventType.BriefingApproved);
        persisted.Title.Should().Be(notification.Title);
        persisted.Message.Should().Be(notification.Message);
        persisted.CreatedAt.Should().Be(notification.CreatedAt);
    }

    [Fact]
    public async Task SaveChanges_WhenNotificationIsAdded_PersistsItUnread()
    {
        // Arrange
        var databaseName = Guid.NewGuid().ToString();
        var notification = CreateNotification(Guid.NewGuid());

        await using (var context = CreateContext(databaseName))
        {
            context.Notifications.Add(notification);
            await context.SaveChangesAsync();
        }

        // Act
        await using var readContext = CreateContext(databaseName);
        var persisted = await readContext.Notifications.FirstAsync(n => n.Id == notification.Id);

        // Assert
        persisted.ReadAt.Should().BeNull();
        persisted.IsRead.Should().BeFalse();
    }

    [Fact]
    public async Task SaveChanges_WhenNotificationIsMarkedAsRead_PersistsTheReadInstant()
    {
        // Arrange
        var databaseName = Guid.NewGuid().ToString();
        var notification = CreateNotification(Guid.NewGuid());

        await using (var context = CreateContext(databaseName))
        {
            context.Notifications.Add(notification);
            await context.SaveChangesAsync();
        }

        // Act
        await using (var updateContext = CreateContext(databaseName))
        {
            var tracked = await updateContext.Notifications.FirstAsync(n => n.Id == notification.Id);
            tracked.MarkAsRead();
            await updateContext.SaveChangesAsync();
        }

        // Assert
        await using var readContext = CreateContext(databaseName);
        var persisted = await readContext.Notifications.FirstAsync(n => n.Id == notification.Id);
        persisted.ReadAt.Should().NotBeNull();
        persisted.IsRead.Should().BeTrue();
    }

    [Fact]
    public async Task SaveChanges_WhenNotificationIsAdded_DoesNotStampAuditColumns()
    {
        // Arrange — Notification is not IAuditableEntity: the alert's author is the system reacting to an
        // event, sometimes with no authenticated user at all (the SLA job of BRF-053).
        await using var context = CreateContext(Guid.NewGuid().ToString(), "admin@briefme.com");
        var notification = CreateNotification(Guid.NewGuid());
        context.Notifications.Add(notification);

        // Act
        await context.SaveChangesAsync();

        // Assert
        var columns = context.Model.FindEntityType(typeof(Notification))!
            .GetProperties()
            .Select(property => property.Name);

        columns.Should().NotContain(["CreatedBy", "UpdatedAt", "UpdatedBy"]);
    }

    [Fact]
    public void Model_WhenBuilt_MapsTheEventTypeAsText()
    {
        // Arrange
        using var context = CreateContext(Guid.NewGuid().ToString());

        // Act
        var eventType = context.Model
            .FindEntityType(typeof(Notification))!
            .FindProperty(nameof(Notification.EventType))!;

        // Assert
        eventType.GetProviderClrType().Should().Be<string>();
        eventType.GetMaxLength().Should().Be(30);
    }

    [Fact]
    public void Model_WhenBuilt_DoesNotMapTheDerivedIsReadFlag()
    {
        // Arrange
        using var context = CreateContext(Guid.NewGuid().ToString());

        // Act
        var isRead = context.Model
            .FindEntityType(typeof(Notification))!
            .FindProperty(nameof(Notification.IsRead));

        // Assert
        isRead.Should().BeNull();
    }

    [Fact]
    public void Model_WhenBuilt_TakesTextLengthsFromTheEntityConstants()
    {
        // Arrange
        using var context = CreateContext(Guid.NewGuid().ToString());
        var entityType = context.Model.FindEntityType(typeof(Notification))!;

        // Act
        var title = entityType.FindProperty(nameof(Notification.Title))!;
        var message = entityType.FindProperty(nameof(Notification.Message))!;

        // Assert
        title.GetMaxLength().Should().Be(Notification.MaxTitleLength);
        title.IsNullable.Should().BeFalse();
        message.GetMaxLength().Should().Be(Notification.MaxMessageLength);
        message.IsNullable.Should().BeFalse();
    }

    [Fact]
    public void Model_WhenBuilt_IndexesRecipientAndReadMarkInThatOrder()
    {
        // Arrange
        using var context = CreateContext(Guid.NewGuid().ToString());

        // Act
        var index = context.Model
            .FindEntityType(typeof(Notification))!
            .GetIndexes()
            .Single();

        // Assert — RecipientId leftmost is what lets the same index serve both the bell's "my unread ones"
        // (BRF-054) and any filter by recipient alone.
        index.GetDatabaseName().Should().Be("IX_Notifications_RecipientId_ReadAt");
        index.Properties.Select(property => property.Name).Should()
            .Equal(nameof(Notification.RecipientId), nameof(Notification.ReadAt));
        index.IsUnique.Should().BeFalse();
    }

    [Fact]
    public void Model_WhenBuilt_DeclaresNoForeignKeyToUsers()
    {
        // Arrange
        using var context = CreateContext(Guid.NewGuid().ToString());

        // Act
        var foreignKeys = context.Model
            .FindEntityType(typeof(Notification))!
            .GetForeignKeys();

        // Assert — RecipientId is a logical FK: a physical one would either block deleting a user who was
        // once notified, or erase the record of what the system said.
        foreignKeys.Should().BeEmpty();
    }
}
