using BriefMe.Application.Common.Notifications;
using BriefMe.Domain.Enums;
using FluentAssertions;

namespace BriefMe.Application.Tests.Common.Notifications;

/// <summary>
/// Only the derivations are tested: the Portuguese vocabulary for status and priority, and the date format.
/// The sentences that merely interpolate a title are asserted through the handlers
/// (<c>.claude/rules/unit-tests.md</c>).
/// </summary>
public class BriefingTaskNotificationsTests
{
    private const string TaskTitle = "Banner";
    private const string BriefingTitle = "Campanha";

    [Theory]
    [InlineData(BriefingTaskStatus.NotStarted, "Não iniciada")]
    [InlineData(BriefingTaskStatus.InProgress, "Em execução")]
    [InlineData(BriefingTaskStatus.AwaitingTechnicalApproval, "Aguardando aprovação técnica")]
    [InlineData(BriefingTaskStatus.AwaitingRequesterApproval, "Aguardando aprovação do solicitante")]
    [InlineData(BriefingTaskStatus.Approved, "Aprovada")]
    [InlineData(BriefingTaskStatus.Rejected, "Rejeitada")]
    [InlineData(BriefingTaskStatus.Blocked, "Bloqueada")]
    public void StatusChanged_WhenStatusIsNamed_UsesItsPortugueseName(
        BriefingTaskStatus status,
        string expectedName)
    {
        // Act
        var message = BriefingTaskNotifications.StatusChanged(TaskTitle, BriefingTitle, status);

        // Assert
        message.Should().Contain($"\"{expectedName}\"");
    }

    [Theory]
    [InlineData(Priority.Low, "Baixa")]
    [InlineData(Priority.Medium, "Média")]
    [InlineData(Priority.High, "Alta")]
    [InlineData(Priority.Urgent, "Urgente")]
    public void Replanned_WhenPriorityIsNamed_UsesItsPortugueseName(Priority priority, string expectedName)
    {
        // Arrange
        var deadline = new DateOnly(2026, 12, 24);

        // Act
        var message = BriefingTaskNotifications.Replanned(TaskTitle, BriefingTitle, deadline, priority);

        // Assert
        message.Should().Contain($"prioridade {expectedName}");
    }

    [Fact]
    public void Replanned_WhenDeadlineIsSet_FormatsItAsBrazilianDate()
    {
        // Arrange
        var deadline = new DateOnly(2026, 12, 24);

        // Act
        var message = BriefingTaskNotifications.Replanned(
            TaskTitle,
            BriefingTitle,
            deadline,
            Priority.High);

        // Assert
        message.Should().Contain("entrega em 24/12/2026");
    }

    [Fact]
    public void Replanned_WhenDeadlineIsMissing_DropsTheDeadlineClause()
    {
        // Act
        var message = BriefingTaskNotifications.Replanned(
            TaskTitle,
            BriefingTitle,
            deadline: null,
            Priority.High);

        // Assert
        message.Should().NotContain("entrega em").And.Contain("prioridade Alta");
    }

    [Fact]
    public void Replanned_WhenTitlesAreAtTheirLimits_FitsTheNotificationLimit()
    {
        // Arrange
        var deadline = DateOnly.FromDateTime(DateTime.UtcNow);

        // Act
        var message = BriefingTaskNotifications.Replanned(
            new string('t', 200),
            new string('b', 200),
            deadline,
            Priority.Urgent);

        // Assert
        message.Length.Should().BeLessThanOrEqualTo(Domain.Entities.Notification.MaxMessageLength);
    }

    [Fact]
    public void Overdue_WhenTheDeadlineFellDue_NamesTheDueDateInBrazilianFormat()
    {
        // Arrange — a UTC instant whose day is unambiguous under either day/month order.
        var dueAtUtc = new DateTime(2026, 9, 5, 14, 30, 0, DateTimeKind.Utc);

        // Act
        var message = BriefingTaskNotifications.Overdue(TaskTitle, BriefingTitle, dueAtUtc);

        // Assert
        message.Should().Contain("05/09/2026");
    }

    [Fact]
    public void Overdue_WhenTheDeadlineFellDue_OmitsTheTimeOfDay()
    {
        // Arrange — the instant is UTC and the reader is in UTC-3: an unqualified hour would be wrong.
        var dueAtUtc = new DateTime(2026, 9, 5, 14, 30, 0, DateTimeKind.Utc);

        // Act
        var message = BriefingTaskNotifications.Overdue(TaskTitle, BriefingTitle, dueAtUtc);

        // Assert
        message.Should().NotContain("14:30");
    }

    [Fact]
    public void Overdue_WhenTitlesAreAtTheirLimits_FitsTheNotificationLimit()
    {
        // Act
        var message = BriefingTaskNotifications.Overdue(
            new string('t', 200),
            new string('b', 200),
            DateTime.UtcNow);

        // Assert
        message.Length.Should().BeLessThanOrEqualTo(Domain.Entities.Notification.MaxMessageLength);
    }
}
