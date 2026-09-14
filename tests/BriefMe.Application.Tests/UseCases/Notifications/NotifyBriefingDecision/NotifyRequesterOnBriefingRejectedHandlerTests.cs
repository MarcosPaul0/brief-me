using BriefMe.Application.Abstractions.Notifications;
using BriefMe.Application.UseCases.Notifications.NotifyBriefingDecision;
using BriefMe.Domain.Entities;
using BriefMe.Domain.Enums;
using BriefMe.Domain.Events;
using BriefMe.Domain.Repositories;
using BriefMe.Domain.ValueObjects;
using FluentAssertions;
using Moq;

namespace BriefMe.Application.Tests.UseCases.Notifications.NotifyBriefingDecision;

public class NotifyRequesterOnBriefingRejectedHandlerTests
{
    private const string BriefingTitle = "Campanha de fim de ano";
    private const string RejectionReason = "Faltou detalhar o público-alvo.";

    private readonly Mock<IBriefingRepository> _briefingRepositoryMock = new();
    private readonly Mock<INotificationService> _notificationServiceMock = new();
    private readonly Guid _requesterId = Guid.NewGuid();
    private readonly Guid _managerId = Guid.NewGuid();
    private readonly NotifyRequesterOnBriefingRejectedHandler _handler;

    public NotifyRequesterOnBriefingRejectedHandlerTests()
    {
        _handler = new NotifyRequesterOnBriefingRejectedHandler(
            _briefingRepositoryMock.Object,
            _notificationServiceMock.Object);
    }

    /// <summary>Briefing in Rejected, reached through the real transitions (no reflection).</summary>
    private Briefing ArrangeRejectedBriefing(string title = BriefingTitle)
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
        briefing.Reject(RejectionReason, _managerId);

        _briefingRepositoryMock
            .Setup(r => r.GetByIdAsync(briefing.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(briefing);

        return briefing;
    }

    private BriefingRejected EventFor(Briefing briefing, string justification = RejectionReason) =>
        new(briefing.Id, _requesterId, _managerId, justification);

    [Fact]
    public async Task Handle_WhenBriefingIsRejected_NotifiesTheRequester()
    {
        // Arrange
        var briefing = ArrangeRejectedBriefing();

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
    public async Task Handle_WhenBriefingIsRejected_UsesTheBriefingRejectedEventType()
    {
        // Arrange
        var briefing = ArrangeRejectedBriefing();

        // Act
        await _handler.Handle(EventFor(briefing), default);

        // Assert
        _notificationServiceMock.Verify(
            s => s.NotifyAsync(
                It.Is<NotificationRequest>(r => r.EventType == NotificationEventType.BriefingRejected),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_WhenBriefingIsRejected_MessageNamesTheBriefing()
    {
        // Arrange
        var briefing = ArrangeRejectedBriefing();

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
    public async Task Handle_WhenBriefingIsRejected_MessageCarriesTheJustification()
    {
        // Arrange
        var briefing = ArrangeRejectedBriefing();

        // Act
        await _handler.Handle(EventFor(briefing), default);

        // Assert
        _notificationServiceMock.Verify(
            s => s.NotifyAsync(
                It.Is<NotificationRequest>(r => r.Message.Contains(RejectionReason)),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    /// <summary>
    /// Longest title (200) plus longest justification (2000) overflow the message limit — the case the
    /// catalog trims so the entry can still be written.
    /// </summary>
    [Fact]
    public async Task Handle_WhenTitleAndJustificationAreAtTheirLimits_ComposesATextNotificationAccepts()
    {
        // Arrange
        var briefing = ArrangeRejectedBriefing(new string('a', 200));
        NotificationRequest? captured = null;
        _notificationServiceMock
            .Setup(s => s.NotifyAsync(It.IsAny<NotificationRequest>(), It.IsAny<CancellationToken>()))
            .Callback<NotificationRequest, CancellationToken>((request, _) => captured = request)
            .Returns(Task.CompletedTask);

        // Act
        await _handler.Handle(EventFor(briefing, new string('b', Justification.MaxLength)), default);

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
        await _handler.Handle(
            new BriefingRejected(Guid.NewGuid(), _requesterId, _managerId, RejectionReason),
            default);

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
        var handling = async () => await _handler.Handle(
            new BriefingRejected(Guid.NewGuid(), _requesterId, _managerId, RejectionReason),
            default);

        // Assert
        await handling.Should().NotThrowAsync();
    }
}
