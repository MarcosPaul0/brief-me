using BriefMe.Domain.Entities;
using BriefMe.Domain.Enums;
using BriefMe.Domain.Exceptions;
using FluentAssertions;

namespace BriefMe.Domain.Tests.Entities;

public class NotificationTests
{
    private static readonly Guid RequesterId = Guid.NewGuid();

    [Fact]
    public void Create_WhenDataIsValid_ReturnsUnreadNotificationForTheRecipient()
    {
        // Arrange
        const string title = "Briefing aprovado";
        const string message = "O briefing \"Campanha de fim de ano\" foi aprovado.";

        // Act
        var notification = Notification.Create(
            RequesterId,
            NotificationEventType.BriefingApproved,
            title,
            message);

        // Assert
        notification.RecipientId.Should().Be(RequesterId);
        notification.EventType.Should().Be(NotificationEventType.BriefingApproved);
        notification.Title.Should().Be(title);
        notification.Message.Should().Be(message);
        notification.IsRead.Should().BeFalse();
        notification.ReadAt.Should().BeNull();
    }

    [Fact]
    public void Create_WhenRecipientIsEmpty_ThrowsDomainException()
    {
        // Arrange
        // (none)

        // Act
        var act = () => Notification.Create(
            Guid.Empty,
            NotificationEventType.TaskAssigned,
            "Nova tarefa",
            "Uma tarefa foi atribuída a você.");

        // Assert
        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void Create_WhenEventTypeIsOutsideTheEnum_ThrowsDomainException()
    {
        // Arrange
        const NotificationEventType unknownEventType = (NotificationEventType)99;

        // Act
        var act = () => Notification.Create(RequesterId, unknownEventType, "Título", "Mensagem");

        // Assert
        act.Should().Throw<DomainException>();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Create_WhenTitleIsNullOrWhitespace_ThrowsDomainException(string? title)
    {
        // Arrange
        // (none)

        // Act
        var act = () => Notification.Create(
            RequesterId,
            NotificationEventType.BriefingRejected,
            title,
            "Mensagem");

        // Assert
        act.Should().Throw<DomainException>();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Create_WhenMessageIsNullOrWhitespace_ThrowsDomainException(string? message)
    {
        // Arrange
        // (none)

        // Act
        var act = () => Notification.Create(
            RequesterId,
            NotificationEventType.BriefingRejected,
            "Título",
            message);

        // Assert
        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void Create_WhenTitleExceedsMaxLength_ThrowsDomainException()
    {
        // Arrange
        var title = new string('t', Notification.MaxTitleLength + 1);

        // Act
        var act = () => Notification.Create(
            RequesterId,
            NotificationEventType.EntityChanged,
            title,
            "Mensagem");

        // Assert
        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void Create_WhenMessageExceedsMaxLength_ThrowsDomainException()
    {
        // Arrange
        var message = new string('m', Notification.MaxMessageLength + 1);

        // Act
        var act = () => Notification.Create(
            RequesterId,
            NotificationEventType.EntityChanged,
            "Título",
            message);

        // Assert
        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void Create_WhenTextHasSurroundingWhitespace_TrimsIt()
    {
        // Arrange
        const string title = "  Prazo estourado  ";
        const string message = "\tA tarefa passou do prazo.\n";

        // Act
        var notification = Notification.Create(
            RequesterId,
            NotificationEventType.DeadlineOverdue,
            title,
            message);

        // Assert
        notification.Title.Should().Be("Prazo estourado");
        notification.Message.Should().Be("A tarefa passou do prazo.");
    }

    [Fact]
    public void MarkAsRead_WhenNotificationIsUnread_RecordsTheInstant()
    {
        // Arrange
        var notification = Notification.Create(
            RequesterId,
            NotificationEventType.BriefingApproved,
            "Título",
            "Mensagem");

        // Act
        notification.MarkAsRead();

        // Assert
        notification.IsRead.Should().BeTrue();
        notification.ReadAt.Should().NotBeNull();
    }

    [Fact]
    public void MarkAsRead_WhenNotificationIsAlreadyRead_KeepsTheFirstInstant()
    {
        // Arrange
        var notification = Notification.Create(
            RequesterId,
            NotificationEventType.BriefingApproved,
            "Título",
            "Mensagem");
        notification.MarkAsRead();
        var firstReadAt = notification.ReadAt;

        // Act
        notification.MarkAsRead();

        // Assert
        notification.ReadAt.Should().Be(firstReadAt);
    }

    [Fact]
    public void Create_WhenCalledTwiceForTheSameRecipient_ProducesDistinctEntries()
    {
        // Arrange
        const string title = "Briefing reprovado";

        // Act
        var first = Notification.Create(RequesterId, NotificationEventType.BriefingRejected, title, "Motivo A");
        var second = Notification.Create(RequesterId, NotificationEventType.BriefingRejected, title, "Motivo B");

        // Assert
        first.Id.Should().NotBe(second.Id);
    }
}
