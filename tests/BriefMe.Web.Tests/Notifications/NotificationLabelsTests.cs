using BriefMe.Domain.Enums;
using BriefMe.Web.Components.Notifications;
using FluentAssertions;

namespace BriefMe.Web.Tests.Notifications;

/// <summary>
/// Covers the wording and treatment the bell renders (BRF-054). Pure statics, so the acceptance criteria
/// about the badge and the relative time are testable without rendering a component.
/// </summary>
public class NotificationLabelsTests
{
    private static readonly DateTime Now = new(2026, 9, 8, 12, 0, 0, DateTimeKind.Utc);

    [Theory]
    [InlineData(NotificationEventType.BriefingApproved, "Aprovação")]
    [InlineData(NotificationEventType.BriefingRejected, "Reprovação")]
    [InlineData(NotificationEventType.TaskAssigned, "Atribuição")]
    [InlineData(NotificationEventType.EntityChanged, "Alteração")]
    [InlineData(NotificationEventType.DeadlineOverdue, "Atraso")]
    public void EventType_WhenTheReasonIsKnown_NamesIt(NotificationEventType eventType, string expected)
    {
        // Arrange
        // (none needed)

        // Act
        var label = NotificationLabels.EventType(eventType);

        // Assert
        label.Should().Be(expected);
    }

    [Fact]
    public void EventType_WhenTheReasonIsUnmapped_FallsBackInsteadOfThrowing()
    {
        // Arrange
        var unmapped = (NotificationEventType)99;

        // Act
        var label = NotificationLabels.EventType(unmapped);

        // Assert
        // A bell that throws over a new enum member would hide every other entry with it.
        label.Should().Be("Notificação");
    }

    [Fact]
    public void EventTypeColorClass_WhenTheReasonIsUnmapped_UsesTheMutedTint()
    {
        // Arrange
        var unmapped = (NotificationEventType)99;

        // Act
        var cssClass = NotificationLabels.EventTypeColorClass(unmapped);

        // Assert
        cssClass.Should().Be("text-[#999999]");
    }

    [Fact]
    public void EventTypeDotClass_WhenTheReasonIsARejection_UsesTheRedTint()
    {
        // Arrange
        // (none needed)

        // Act
        var cssClass = NotificationLabels.EventTypeDotClass(NotificationEventType.BriefingRejected);

        // Assert
        cssClass.Should().Be("bg-[#ef4444]");
    }

    [Fact]
    public void RelativeTime_WhenUnderAMinute_ReadsAsNow()
    {
        // Arrange
        var createdAt = Now.AddSeconds(-30);

        // Act
        var label = NotificationLabels.RelativeTime(createdAt, Now);

        // Assert
        label.Should().Be("agora");
    }

    [Theory]
    [InlineData(1, "há 1 minuto")]
    [InlineData(2, "há 2 minutos")]
    [InlineData(59, "há 59 minutos")]
    public void RelativeTime_WhenUnderAnHour_CountsMinutes(int minutes, string expected)
    {
        // Arrange
        var createdAt = Now.AddMinutes(-minutes);

        // Act
        var label = NotificationLabels.RelativeTime(createdAt, Now);

        // Assert
        label.Should().Be(expected);
    }

    [Theory]
    [InlineData(1, "há 1 hora")]
    [InlineData(5, "há 5 horas")]
    [InlineData(23, "há 23 horas")]
    public void RelativeTime_WhenUnderADay_CountsHours(int hours, string expected)
    {
        // Arrange
        var createdAt = Now.AddHours(-hours);

        // Act
        var label = NotificationLabels.RelativeTime(createdAt, Now);

        // Assert
        label.Should().Be(expected);
    }

    [Theory]
    [InlineData(1, "há 1 dia")]
    [InlineData(3, "há 3 dias")]
    [InlineData(7, "há 7 dias")]
    public void RelativeTime_WhenWithinAWeek_CountsDays(int days, string expected)
    {
        // Arrange
        var createdAt = Now.AddDays(-days);

        // Act
        var label = NotificationLabels.RelativeTime(createdAt, Now);

        // Assert
        label.Should().Be(expected);
    }

    [Fact]
    public void RelativeTime_WhenOlderThanAWeek_ShowsTheDate()
    {
        // Arrange
        var createdAt = new DateTime(2026, 7, 1, 9, 30, 0, DateTimeKind.Utc);

        // Act
        var label = NotificationLabels.RelativeTime(createdAt, Now);

        // Assert
        // "há 69 dias" is a number the reader has to convert back into a date.
        label.Should().Be("01/07/2026");
    }

    [Fact]
    public void RelativeTime_WhenTheInstantIsInTheFuture_ReadsAsNowInsteadOfNegative()
    {
        // Arrange
        var createdAt = Now.AddMinutes(5);

        // Act
        var label = NotificationLabels.RelativeTime(createdAt, Now);

        // Assert
        // Clock skew between the browser and the server that stamped it: the entry exists, so it happened.
        label.Should().Be("agora");
    }

    [Theory]
    [InlineData(1, "1")]
    [InlineData(42, "42")]
    [InlineData(99, "99")]
    public void UnreadBadge_WhenTheCountFits_ShowsTheNumber(int count, string expected)
    {
        // Arrange
        // (none needed)

        // Act
        var badge = NotificationLabels.UnreadBadge(count);

        // Assert
        badge.Should().Be(expected);
    }

    [Theory]
    [InlineData(100)]
    [InlineData(1000)]
    public void UnreadBadge_WhenTheCountExceedsTheCap_ShowsTheCap(int count)
    {
        // Arrange
        // (none needed)

        // Act
        var badge = NotificationLabels.UnreadBadge(count);

        // Assert
        // An inbox nobody has opened in a month must not be able to widen the sidebar chrome.
        badge.Should().Be("99+");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void UnreadBadge_WhenThereIsNothingToShow_ReturnsEmpty(int count)
    {
        // Arrange
        // (none needed)

        // Act
        var badge = NotificationLabels.UnreadBadge(count);

        // Assert
        badge.Should().BeEmpty();
    }
}
