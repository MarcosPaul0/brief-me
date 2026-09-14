using BriefMe.Domain.Entities;
using BriefMe.Domain.Enums;
using BriefMe.Infrastructure.Persistence;
using BriefMe.Infrastructure.Persistence.Repositories;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace BriefMe.Infrastructure.Tests.Persistence;

/// <summary>
/// Covers the four reads the bell of BRF-054 added to the inbox repository: recipient scoping, order, paging
/// and the unread set. Scoping is the one worth testing hardest — the inbox carries rejection justifications
/// (RF013/RF032), so a query that crosses recipients is not a display bug.
/// </summary>
public class NotificationRepositoryTests
{
    private static AppDbContext CreateContext(string databaseName)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName)
            .Options;

        return new AppDbContext(options);
    }

    /// <summary>
    /// Builds an entry with a chosen <c>CreatedAt</c>: the entity stamps <c>DateTime.UtcNow</c> itself
    /// (BRF-047), and ordering cannot be tested against instants a few ticks apart.
    /// </summary>
    private static Notification NewNotification(Guid recipientId, DateTime createdAt, string title = "Briefing aprovado")
    {
        var notification = Notification.Create(
            recipientId,
            NotificationEventType.BriefingApproved,
            title,
            "Seu briefing \"Campanha de Natal\" foi aprovado.");

        typeof(Notification)
            .GetProperty(nameof(Notification.CreatedAt))!
            .SetValue(notification, createdAt);

        return notification;
    }

    private static async Task<(AppDbContext Context, NotificationRepository Repository)> SeedAsync(
        params Notification[] notifications)
    {
        var context = CreateContext(Guid.NewGuid().ToString());

        context.Notifications.AddRange(notifications);
        await context.SaveChangesAsync();

        return (context, new NotificationRepository(context));
    }

    [Fact]
    public async Task GetByRecipientAsync_WhenAnotherUserHasNotifications_ReturnsOnlyTheRecipientsOwn()
    {
        // Arrange
        var recipientId = Guid.NewGuid();
        var otherId = Guid.NewGuid();
        var createdAt = new DateTime(2026, 9, 1, 10, 0, 0, DateTimeKind.Utc);

        var (context, repository) = await SeedAsync(
            NewNotification(recipientId, createdAt, "Meu aviso"),
            NewNotification(otherId, createdAt, "Aviso alheio"));

        await using var _ = context;

        // Act
        var page = await repository.GetByRecipientAsync(recipientId, 1, 10);

        // Assert
        page.Should().ContainSingle().Which.Title.Should().Be("Meu aviso");
    }

    [Fact]
    public async Task GetByRecipientAsync_WhenTheInboxHasSeveralEntries_ReturnsTheNewestFirst()
    {
        // Arrange
        var recipientId = Guid.NewGuid();

        var (context, repository) = await SeedAsync(
            NewNotification(recipientId, new DateTime(2026, 9, 1, 10, 0, 0, DateTimeKind.Utc), "Mais antigo"),
            NewNotification(recipientId, new DateTime(2026, 9, 3, 10, 0, 0, DateTimeKind.Utc), "Mais recente"),
            NewNotification(recipientId, new DateTime(2026, 9, 2, 10, 0, 0, DateTimeKind.Utc), "Intermediário"));

        await using var _ = context;

        // Act
        var page = await repository.GetByRecipientAsync(recipientId, 1, 10);

        // Assert
        page.Select(n => n.Title).Should().ContainInOrder("Mais recente", "Intermediário", "Mais antigo");
    }

    [Fact]
    public async Task GetByRecipientAsync_WhenAPageIsRequested_ReturnsThatSlice()
    {
        // Arrange
        var recipientId = Guid.NewGuid();

        var (context, repository) = await SeedAsync(
            NewNotification(recipientId, new DateTime(2026, 9, 3, 10, 0, 0, DateTimeKind.Utc), "Primeiro"),
            NewNotification(recipientId, new DateTime(2026, 9, 2, 10, 0, 0, DateTimeKind.Utc), "Segundo"),
            NewNotification(recipientId, new DateTime(2026, 9, 1, 10, 0, 0, DateTimeKind.Utc), "Terceiro"));

        await using var _ = context;

        // Act
        var page = await repository.GetByRecipientAsync(recipientId, 2, 2);

        // Assert
        page.Should().ContainSingle().Which.Title.Should().Be("Terceiro");
    }

    [Fact]
    public async Task GetByRecipientAsync_WhenTheEntryWasRead_StillReturnsIt()
    {
        // Arrange
        var recipientId = Guid.NewGuid();
        var read = NewNotification(recipientId, new DateTime(2026, 9, 1, 10, 0, 0, DateTimeKind.Utc));
        read.MarkAsRead();

        var (context, repository) = await SeedAsync(read);

        await using var _ = context;

        // Act
        var page = await repository.GetByRecipientAsync(recipientId, 1, 10);

        // Assert
        // The bell shows what happened, not only what is new — the read mark is a property of a row, not a
        // filter on the list.
        page.Should().ContainSingle().Which.IsRead.Should().BeTrue();
    }

    [Fact]
    public async Task CountByRecipientAsync_WhenAnotherUserHasNotifications_CountsOnlyTheRecipientsOwn()
    {
        // Arrange
        var recipientId = Guid.NewGuid();
        var createdAt = new DateTime(2026, 9, 1, 10, 0, 0, DateTimeKind.Utc);

        var (context, repository) = await SeedAsync(
            NewNotification(recipientId, createdAt),
            NewNotification(recipientId, createdAt),
            NewNotification(Guid.NewGuid(), createdAt));

        await using var _ = context;

        // Act
        var count = await repository.CountByRecipientAsync(recipientId);

        // Assert
        count.Should().Be(2);
    }

    [Fact]
    public async Task CountUnreadByRecipientAsync_WhenSomeWereRead_CountsOnlyTheUnreadOnes()
    {
        // Arrange
        var recipientId = Guid.NewGuid();
        var createdAt = new DateTime(2026, 9, 1, 10, 0, 0, DateTimeKind.Utc);

        var read = NewNotification(recipientId, createdAt);
        read.MarkAsRead();

        var (context, repository) = await SeedAsync(
            read,
            NewNotification(recipientId, createdAt),
            NewNotification(recipientId, createdAt));

        await using var _ = context;

        // Act
        var count = await repository.CountUnreadByRecipientAsync(recipientId);

        // Assert
        count.Should().Be(2);
    }

    [Fact]
    public async Task CountUnreadByRecipientAsync_WhenAnotherUserHasUnreadEntries_DoesNotCountThem()
    {
        // Arrange
        var recipientId = Guid.NewGuid();
        var createdAt = new DateTime(2026, 9, 1, 10, 0, 0, DateTimeKind.Utc);

        var (context, repository) = await SeedAsync(
            NewNotification(recipientId, createdAt),
            NewNotification(Guid.NewGuid(), createdAt),
            NewNotification(Guid.NewGuid(), createdAt));

        await using var _ = context;

        // Act
        var count = await repository.CountUnreadByRecipientAsync(recipientId);

        // Assert
        count.Should().Be(1);
    }

    [Fact]
    public async Task GetUnreadByRecipientAsync_WhenSomeWereRead_ReturnsOnlyTheUnreadOnes()
    {
        // Arrange
        var recipientId = Guid.NewGuid();
        var createdAt = new DateTime(2026, 9, 1, 10, 0, 0, DateTimeKind.Utc);

        var read = NewNotification(recipientId, createdAt, "Já lida");
        read.MarkAsRead();

        var (context, repository) = await SeedAsync(read, NewNotification(recipientId, createdAt, "Nova"));

        await using var _ = context;

        // Act
        var unread = await repository.GetUnreadByRecipientAsync(recipientId);

        // Assert
        unread.Should().ContainSingle().Which.Title.Should().Be("Nova");
    }

    [Fact]
    public async Task GetUnreadByRecipientAsync_WhenAnotherUserHasUnreadEntries_NeverReturnsThem()
    {
        // Arrange
        var recipientId = Guid.NewGuid();
        var createdAt = new DateTime(2026, 9, 1, 10, 0, 0, DateTimeKind.Utc);

        var (context, repository) = await SeedAsync(
            NewNotification(recipientId, createdAt, "Minha"),
            NewNotification(Guid.NewGuid(), createdAt, "Alheia"));

        await using var _ = context;

        // Act
        var unread = await repository.GetUnreadByRecipientAsync(recipientId);

        // Assert
        // What this set returns is what the bulk read mark writes to — crossing recipients here would mark
        // someone else's inbox as read.
        unread.Should().ContainSingle().Which.RecipientId.Should().Be(recipientId);
    }

    [Fact]
    public async Task GetUnreadByRecipientAsync_WhenNothingIsUnread_ReturnsAnEmptySet()
    {
        // Arrange
        var recipientId = Guid.NewGuid();
        var read = NewNotification(recipientId, new DateTime(2026, 9, 1, 10, 0, 0, DateTimeKind.Utc));
        read.MarkAsRead();

        var (context, repository) = await SeedAsync(read);

        await using var _ = context;

        // Act
        var unread = await repository.GetUnreadByRecipientAsync(recipientId);

        // Assert
        unread.Should().BeEmpty();
    }
}
