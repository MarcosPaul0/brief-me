using BriefMe.Application.Abstractions.Notifications;
using BriefMe.Domain.Entities;
using BriefMe.Domain.Enums;
using BriefMe.Domain.Exceptions;
using BriefMe.Domain.Repositories;
using BriefMe.Infrastructure.Notifications;
using BriefMe.Infrastructure.Persistence;
using BriefMe.Infrastructure.Persistence.Repositories;
using BriefMe.Infrastructure.Tests.TestDoubles;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace BriefMe.Infrastructure.Tests.Notifications;

/// <summary>
/// Covers the in-app delivery of BRF-048: one entry per request, one commit per call, and a batch that
/// either lands whole or not at all.
/// </summary>
public class InAppNotificationServiceTests
{
    private readonly AppDbContext _context;
    private readonly CountingUnitOfWork _unitOfWork;
    private readonly InAppNotificationService _service;

    public InAppNotificationServiceTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _context = new AppDbContext(options);
        _unitOfWork = new CountingUnitOfWork(new UnitOfWork(_context, new RecordingDomainEventDispatcher()));
        _service = new InAppNotificationService(new NotificationRepository(_context), _unitOfWork);
    }

    private static NotificationRequest Request(Guid recipientId) =>
        new(recipientId, NotificationEventType.TaskAssigned, "Nova tarefa", "A tarefa \"Key visual\" é sua.");

    [Fact]
    public async Task NotifyAsync_WhenSingleRequest_WritesOneInboxEntryForTheRecipient()
    {
        // Arrange
        var recipientId = Guid.NewGuid();

        // Act
        await _service.NotifyAsync(Request(recipientId));

        // Assert
        var persisted = await _context.Notifications.SingleAsync();
        persisted.RecipientId.Should().Be(recipientId);
        persisted.EventType.Should().Be(NotificationEventType.TaskAssigned);
        persisted.Title.Should().Be("Nova tarefa");
        persisted.Message.Should().Be("A tarefa \"Key visual\" é sua.");
        persisted.IsRead.Should().BeFalse();
    }

    [Fact]
    public async Task NotifyAsync_WhenBatchOfThree_WritesOneEntryPerRecipient()
    {
        // Arrange — RF034: the same change concerns the requester, the Designer and the Manager.
        var requester = Guid.NewGuid();
        var designer = Guid.NewGuid();
        var manager = Guid.NewGuid();

        // Act
        await _service.NotifyAsync([Request(requester), Request(designer), Request(manager)]);

        // Assert
        var recipients = await _context.Notifications.Select(n => n.RecipientId).ToListAsync();
        recipients.Should().BeEquivalentTo([requester, designer, manager]);
    }

    [Fact]
    public async Task NotifyAsync_WhenBatchOfThree_CommitsOnce()
    {
        // Arrange
        var requests = new[] { Request(Guid.NewGuid()), Request(Guid.NewGuid()), Request(Guid.NewGuid()) };

        // Act
        await _service.NotifyAsync(requests);

        // Assert
        _unitOfWork.CommitCount.Should().Be(1);
    }

    [Fact]
    public async Task NotifyAsync_WhenBatchIsEmpty_WritesNothingAndDoesNotCommit()
    {
        // Arrange
        IReadOnlyCollection<NotificationRequest> requests = [];

        // Act
        await _service.NotifyAsync(requests);

        // Assert
        _unitOfWork.CommitCount.Should().Be(0);
        (await _context.Notifications.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task NotifyAsync_WhenBatchIsNull_ThrowsArgumentNullException()
    {
        // Arrange
        IReadOnlyCollection<NotificationRequest> requests = null!;

        // Act
        var act = () => _service.NotifyAsync(requests);

        // Assert — a null batch is a caller bug, not user input.
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task NotifyAsync_WhenOneRequestViolatesAnInvariant_ThrowsDomainException()
    {
        // Arrange
        var invalid = Request(Guid.NewGuid()) with { Message = "   " };

        // Act
        var act = () => _service.NotifyAsync([Request(Guid.NewGuid()), invalid]);

        // Assert
        await act.Should().ThrowAsync<DomainException>();
    }

    [Fact]
    public async Task NotifyAsync_WhenOneRequestViolatesAnInvariant_WritesNoneOfTheBatch()
    {
        // Arrange
        var invalid = Request(Guid.NewGuid()) with { Title = new string('a', Notification.MaxTitleLength + 1) };

        // Act
        try
        {
            await _service.NotifyAsync([Request(Guid.NewGuid()), invalid, Request(Guid.NewGuid())]);
        }
        catch (DomainException)
        {
            // The throw is asserted by the test above; here the point is what did not reach the database.
        }

        // Assert — half of an RF034 notification is worse than none.
        _unitOfWork.CommitCount.Should().Be(0);
        (await _context.Notifications.CountAsync()).Should().Be(0);
    }

    /// <summary>
    /// Wraps the real unit of work to count commits: "one write, one commit" is behaviour the persisted rows
    /// alone cannot distinguish from three separate transactions.
    /// </summary>
    private sealed class CountingUnitOfWork(IUnitOfWork inner) : IUnitOfWork
    {
        public int CommitCount { get; private set; }

        public Task CommitAsync(CancellationToken cancellationToken = default)
        {
            CommitCount++;
            return inner.CommitAsync(cancellationToken);
        }
    }
}
