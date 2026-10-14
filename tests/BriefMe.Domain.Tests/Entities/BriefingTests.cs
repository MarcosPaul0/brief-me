using BriefMe.Domain.Entities;
using BriefMe.Domain.Enums;
using BriefMe.Domain.Exceptions;
using FluentAssertions;

namespace BriefMe.Domain.Tests.Entities;

public class BriefingTests
{
    private static Briefing NewDraft() =>
        Briefing.Create("Launch campaign", Priority.Medium, Guid.NewGuid(), Guid.NewGuid());

    private static Briefing CompleteDraft() =>
        Briefing.Create(
            "Launch campaign",
            Priority.High,
            Guid.NewGuid(),
            Guid.NewGuid(),
            description: "Social media assets",
            objective: "Promote the new product",
            requestingArea: "Marketing",
            desiredDeliveryDate: DateOnly.FromDateTime(DateTime.UtcNow).AddDays(10));

    private static void ForceStatus(Briefing briefing, BriefingStatus status)
    {
        typeof(Briefing)
            .GetProperty(nameof(Briefing.Status))!
            .SetValue(briefing, status);
    }

    [Fact]
    public void Create_WhenDataIsValid_StartsAsDraft()
    {
        // Arrange
        var categoryId = Guid.NewGuid();
        var requesterId = Guid.NewGuid();

        // Act
        var briefing = Briefing.Create("New banner", Priority.Low, categoryId, requesterId);

        // Assert
        briefing.Status.Should().Be(BriefingStatus.Draft);
        briefing.CategoryId.Should().Be(categoryId);
        briefing.RequesterId.Should().Be(requesterId);
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
        var act = () => Briefing.Create(title!, Priority.Medium, Guid.NewGuid(), Guid.NewGuid());

        // Assert
        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void Create_WhenCategoryIdIsEmpty_ThrowsDomainException()
    {
        // Arrange
        // (none)

        // Act
        var act = () => Briefing.Create("Banner", Priority.Medium, Guid.Empty, Guid.NewGuid());

        // Assert
        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void Create_WhenRequesterIdIsEmpty_ThrowsDomainException()
    {
        // Arrange
        // (none)

        // Act
        var act = () => Briefing.Create("Banner", Priority.Medium, Guid.NewGuid(), Guid.Empty);

        // Assert
        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void Edit_WhenStatusIsDraft_UpdatesContent()
    {
        // Arrange
        var briefing = NewDraft();

        // Act
        briefing.Edit("Edited title", Priority.Urgent, "New description", "New objective", "Design", null);

        // Assert
        briefing.Title.Should().Be("Edited title");
        briefing.Priority.Should().Be(Priority.Urgent);
        briefing.Objective.Should().Be("New objective");
    }

    [Fact]
    public void Edit_WhenStatusIsApproved_ThrowsDomainException()
    {
        // Arrange
        var briefing = NewDraft();
        ForceStatus(briefing, BriefingStatus.Completed);

        // Act
        var act = () => briefing.Edit("x", Priority.Medium, null, null, null, null);

        // Assert
        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void SubmitForApproval_WhenAllRequiredFieldsPresent_ChangesStatusToAwaitingApproval()
    {
        // Arrange
        var briefing = CompleteDraft();

        // Act
        var result = briefing.SubmitForApproval();

        // Assert
        result.IsSuccess.Should().BeTrue();
        briefing.Status.Should().Be(BriefingStatus.AwaitingApproval);
    }

    [Fact]
    public void SubmitForApproval_WhenRequiredFieldsMissing_ReturnsFailureAndKeepsStatus()
    {
        // Arrange
        var briefing = NewDraft();

        // Act
        var result = briefing.SubmitForApproval();

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Errors.Should().NotBeEmpty();
        briefing.Status.Should().Be(BriefingStatus.Draft);
    }

    [Fact]
    public void SubmitForApproval_WhenStatusIsApproved_ThrowsDomainException()
    {
        // Arrange
        var briefing = CompleteDraft();
        ForceStatus(briefing, BriefingStatus.Completed);

        // Act
        var act = () => briefing.SubmitForApproval();

        // Assert
        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void Cancel_WhenStatusIsDraft_ChangesStatusToCancelled()
    {
        // Arrange
        var briefing = NewDraft();

        // Act
        briefing.Cancel();

        // Assert
        briefing.Status.Should().Be(BriefingStatus.Cancelled);
    }

    [Theory]
    [InlineData(BriefingStatus.InProgress)]
    [InlineData(BriefingStatus.Completed)]
    public void Cancel_WhenExecutionStartedOrCompleted_ThrowsDomainException(BriefingStatus status)
    {
        // Arrange
        var briefing = NewDraft();
        ForceStatus(briefing, status);

        // Act
        var act = () => briefing.Cancel();

        // Assert
        act.Should().Throw<DomainException>();
    }
}
