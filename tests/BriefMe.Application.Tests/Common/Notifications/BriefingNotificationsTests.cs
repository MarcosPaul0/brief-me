using BriefMe.Application.Common.Notifications;
using BriefMe.Domain.Entities;
using BriefMe.Domain.ValueObjects;
using FluentAssertions;

namespace BriefMe.Application.Tests.Common.Notifications;

/// <summary>
/// Only the derivation is tested: the trimming that keeps a composed body inside
/// <see cref="Notification.MaxMessageLength"/>. The wording itself is asserted through the handlers.
/// </summary>
public class BriefingNotificationsTests
{
    [Fact]
    public void Rejected_WhenTitleAndJustificationAreAtTheirLimits_FitsTheNotificationLimit()
    {
        // Arrange
        var title = new string('a', 200);
        var justification = new string('b', Justification.MaxLength);

        // Act
        var message = BriefingNotifications.Rejected(title, justification);

        // Assert
        message.Length.Should().Be(Notification.MaxMessageLength);
    }

    [Fact]
    public void Rejected_WhenTextOverflows_MarksTheCut()
    {
        // Arrange
        var justification = new string('b', Justification.MaxLength);

        // Act
        var message = BriefingNotifications.Rejected("Campanha", justification);

        // Assert
        message.Should().EndWith("…");
    }

    [Fact]
    public void Rejected_WhenTextFits_KeepsItWhole()
    {
        // Act
        var message = BriefingNotifications.Rejected("Campanha", "Faltou o público-alvo.");

        // Assert
        message.Should().Contain("Campanha").And.EndWith("Faltou o público-alvo.");
    }

    [Fact]
    public void Approved_WhenTitleIsAtItsLimit_FitsTheNotificationLimit()
    {
        // Act
        var message = BriefingNotifications.Approved(new string('a', 200));

        // Assert
        message.Length.Should().BeLessThanOrEqualTo(Notification.MaxMessageLength);
    }

    [Fact]
    public void Overdue_WhenTheDeadlineFellDue_NamesTheDueDateInBrazilianFormat()
    {
        // Arrange
        var dueAtUtc = new DateTime(2026, 9, 5, 14, 30, 0, DateTimeKind.Utc);

        // Act
        var message = BriefingNotifications.Overdue("Campanha", dueAtUtc);

        // Assert
        message.Should().Contain("05/09/2026");
    }

    [Fact]
    public void Overdue_WhenTheDeadlineFellDue_OmitsTheTimeOfDay()
    {
        // Arrange
        var dueAtUtc = new DateTime(2026, 9, 5, 14, 30, 0, DateTimeKind.Utc);

        // Act
        var message = BriefingNotifications.Overdue("Campanha", dueAtUtc);

        // Assert
        message.Should().NotContain("14:30");
    }

    [Fact]
    public void Overdue_WhenTitleIsAtItsLimit_FitsTheNotificationLimit()
    {
        // Act
        var message = BriefingNotifications.Overdue(new string('a', 200), DateTime.UtcNow);

        // Assert
        message.Length.Should().BeLessThanOrEqualTo(Notification.MaxMessageLength);
    }
}
