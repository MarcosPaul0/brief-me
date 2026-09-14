using BriefMe.Application.Abstractions.Notifications;
using BriefMe.Application.UseCases.Notifications.NotifyBriefingDecision;
using BriefMe.Domain.Entities;
using BriefMe.Domain.Enums;
using BriefMe.Domain.Events;
using BriefMe.Domain.Repositories;
using FluentAssertions;
using Moq;

namespace BriefMe.Application.Tests.UseCases.Notifications.NotifyBriefingDecision;

public class NotifyRequesterOnBriefingApprovedHandlerTests
{
    private const string BriefingTitle = "Campanha de fim de ano";

    private readonly Mock<IBriefingRepository> _briefingRepositoryMock = new();
    private readonly Mock<INotificationService> _notificationServiceMock = new();
    private readonly Guid _requesterId = Guid.NewGuid();
    private readonly Guid _managerId = Guid.NewGuid();
    private readonly NotifyRequesterOnBriefingApprovedHandler _handler;

    public NotifyRequesterOnBriefingApprovedHandlerTests()
    {
        _handler = new NotifyRequesterOnBriefingApprovedHandler(
            _briefingRepositoryMock.Object,
            _notificationServiceMock.Object);
    }

    /// <summary>Briefing in Approved, reached through the real transitions (no reflection).</summary>
    private Briefing ArrangeApprovedBriefing(string title = BriefingTitle)
    {
        var briefing = Briefing.Create(
            title,
            Priority.High,
            Guid.NewGuid(),
            _requesterId,
            description: "Peças para redes sociais",
            objective: "Divulgar a campanha",
            requestingArea: "Marketing",
            desiredDeliveryDate: DateOnly.FromDateTime(DateTime.UtcNow).AddDays(10));

        briefing.SubmitForApproval();
        briefing.Approve(_managerId);

        _briefingRepositoryMock
            .Setup(r => r.GetByIdAsync(briefing.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(briefing);

        return briefing;
    }

    private BriefingApproved EventFor(Briefing briefing) =>
        new(briefing.Id, _requesterId, _managerId);

    [Fact]
    public async Task Handle_WhenBriefingIsApproved_NotifiesTheRequester()
    {
        // Arrange
        var briefing = ArrangeApprovedBriefing();

        // Act
        await _handler.Handle(EventFor(briefing), default);

        // Assert
        _notificationServiceMock.Verify(
            s => s.NotifyAsync(
                It.Is<NotificationRequest>(r => r.RecipientId == _requesterId),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_WhenBriefingIsApproved_UsesTheBriefingApprovedEventType()
    {
        // Arrange
        var briefing = ArrangeApprovedBriefing();

        // Act
        await _handler.Handle(EventFor(briefing), default);

        // Assert
        _notificationServiceMock.Verify(
            s => s.NotifyAsync(
                It.Is<NotificationRequest>(r => r.EventType == NotificationEventType.BriefingApproved),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_WhenBriefingIsApproved_MessageNamesTheBriefing()
    {
        // Arrange
        var briefing = ArrangeApprovedBriefing();

        // Act
        await _handler.Handle(EventFor(briefing), default);

        // Assert
        _notificationServiceMock.Verify(
            s => s.NotifyAsync(
                It.Is<NotificationRequest>(r => r.Message.Contains(BriefingTitle)),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_WhenBriefingIsApproved_ComposesATextNotificationAccepts()
    {
        // Arrange
        var briefing = ArrangeApprovedBriefing(new string('a', 200));
        NotificationRequest? captured = null;
        _notificationServiceMock
            .Setup(s => s.NotifyAsync(It.IsAny<NotificationRequest>(), It.IsAny<CancellationToken>()))
            .Callback<NotificationRequest, CancellationToken>((request, _) => captured = request)
            .Returns(Task.CompletedTask);

        // Act
        await _handler.Handle(EventFor(briefing), default);

        // Assert
        var creating = () => Notification.Create(
            captured!.RecipientId,
            captured.EventType,
            captured.Title,
            captured.Message);
        creating.Should().NotThrow();
    }

    [Fact]
    public async Task Handle_WhenBriefingNotFound_DoesNotNotify()
    {
        // Arrange
        _briefingRepositoryMock
            .Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Briefing?)null);

        // Act
        await _handler.Handle(new BriefingApproved(Guid.NewGuid(), _requesterId, _managerId), default);

        // Assert
        _notificationServiceMock.Verify(
            s => s.NotifyAsync(It.IsAny<NotificationRequest>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_WhenBriefingNotFound_DoesNotThrow()
    {
        // Arrange
        _briefingRepositoryMock
            .Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Briefing?)null);

        // Act
        var handling = async () =>
            await _handler.Handle(new BriefingApproved(Guid.NewGuid(), _requesterId, _managerId), default);

        // Assert
        await handling.Should().NotThrowAsync();
    }
}
