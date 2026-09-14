using BriefMe.Domain.Entities;
using BriefMe.Domain.Enums;
using BriefMe.Domain.Events;
using BriefMe.Domain.Exceptions;
using BriefMe.Domain.ValueObjects;
using FluentAssertions;

namespace BriefMe.Domain.Tests.Entities;

public class BriefingTests
{
    private static Briefing NewDraft() =>
        Briefing.Create("Launch campaign", Priority.Medium, Guid.NewGuid(), Guid.NewGuid());

    private static Briefing CompleteDraft()
    {
        var briefing = Briefing.Create(
            "Launch campaign",
            Priority.High,
            Guid.NewGuid(),
            Guid.NewGuid(),
            description: "Social media assets",
            objective: "Promote the new product",
            requestingArea: "Marketing",
            desiredDeliveryDate: DateOnly.FromDateTime(DateTime.UtcNow).AddDays(10));

        // No attachment on purpose: the RF005 fields alone make a draft submittable (BRF-001 models
        // attachments as "zero or more"), and every submission test here proves that.
        return briefing;
    }

    private static Briefing UnderReview()
    {
        var briefing = CompleteDraft();
        briefing.SubmitForApproval();
        briefing.ClearDomainEvents();
        return briefing;
    }

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
        briefing.Attachments.Should().BeEmpty();
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
        ForceStatus(briefing, BriefingStatus.Approved);

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
    public void SubmitForApproval_WhenSuccessful_RaisesBriefingSubmittedForApprovalEvent()
    {
        // Arrange
        var briefing = CompleteDraft();

        // Act
        briefing.SubmitForApproval();

        // Assert
        briefing.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<BriefingSubmittedForApproval>()
            .Which.BriefingId.Should().Be(briefing.Id);
    }

    [Fact]
    public void SubmitForApproval_WhenRequiredFieldsMissing_DoesNotRaiseEvent()
    {
        // Arrange
        var briefing = NewDraft();

        // Act
        briefing.SubmitForApproval();

        // Assert
        briefing.DomainEvents.Should().BeEmpty();
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
    public void SubmitForApproval_WhenNoAttachment_Succeeds()
    {
        // Arrange
        var briefing = Briefing.Create(
            "Campaign",
            Priority.Medium,
            Guid.NewGuid(),
            Guid.NewGuid(),
            description: "d",
            objective: "o",
            requestingArea: "a",
            desiredDeliveryDate: DateOnly.FromDateTime(DateTime.UtcNow).AddDays(5));

        // Act
        var result = briefing.SubmitForApproval();

        // Assert
        result.IsSuccess.Should().BeTrue();
        briefing.Status.Should().Be(BriefingStatus.AwaitingApproval);
    }

    [Fact]
    public void SubmitForApproval_WhenAttachmentIsPresent_SucceedsToo()
    {
        // Arrange
        var briefing = CompleteDraft();
        briefing.AddAttachment("brief.pdf", "/storage/brief.pdf", 1024, Guid.NewGuid());

        // Act
        var result = briefing.SubmitForApproval();

        // Assert
        result.IsSuccess.Should().BeTrue();
        briefing.Status.Should().Be(BriefingStatus.AwaitingApproval);
    }

    [Fact]
    public void SubmitForApproval_WhenStatusIsApproved_ThrowsDomainException()
    {
        // Arrange
        var briefing = CompleteDraft();
        ForceStatus(briefing, BriefingStatus.Approved);

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

    [Fact]
    public void Edit_WhenStatusIsChangesRequested_UpdatesContent()
    {
        // Arrange
        var briefing = UnderReview();
        briefing.RequestChanges("Objective is too vague.", Guid.NewGuid());

        // Act
        briefing.Edit("Sharper title", Priority.High, "d", "Clear objective", "Marketing", DateOnly.FromDateTime(DateTime.UtcNow).AddDays(7));

        // Assert
        briefing.Objective.Should().Be("Clear objective");
    }

    [Fact]
    public void Approve_WhenStatusIsAwaitingApproval_ChangesStatusToApproved()
    {
        // Arrange
        var briefing = UnderReview();

        // Act
        briefing.Approve(Guid.NewGuid());

        // Assert
        briefing.Status.Should().Be(BriefingStatus.Approved);
    }

    [Fact]
    public void Approve_WhenStatusIsAwaitingApproval_RecordsReviewer()
    {
        // Arrange
        var briefing = UnderReview();
        var managerId = Guid.NewGuid();

        // Act
        briefing.Approve(managerId);

        // Assert
        briefing.ReviewedById.Should().Be(managerId);
        briefing.ReviewedAt.Should().NotBeNull();
    }

    [Fact]
    public void Approve_WhenSuccessful_LeavesDecisionJustificationNull()
    {
        // Arrange
        var briefing = UnderReview();

        // Act
        briefing.Approve(Guid.NewGuid());

        // Assert
        briefing.DecisionJustification.Should().BeNull();
    }

    [Fact]
    public void Approve_WhenSuccessful_RaisesBriefingApprovedEvent()
    {
        // Arrange
        var briefing = UnderReview();
        var managerId = Guid.NewGuid();

        // Act
        briefing.Approve(managerId);

        // Assert
        briefing.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<BriefingApproved>()
            .Which.ApprovedById.Should().Be(managerId);
    }

    [Theory]
    [InlineData(BriefingStatus.Draft)]
    [InlineData(BriefingStatus.ChangesRequested)]
    [InlineData(BriefingStatus.Approved)]
    [InlineData(BriefingStatus.Rejected)]
    [InlineData(BriefingStatus.InProgress)]
    [InlineData(BriefingStatus.Completed)]
    [InlineData(BriefingStatus.Cancelled)]
    public void Approve_WhenStatusIsNotAwaitingApproval_ThrowsDomainException(BriefingStatus status)
    {
        // Arrange
        var briefing = UnderReview();
        ForceStatus(briefing, status);

        // Act
        var act = () => briefing.Approve(Guid.NewGuid());

        // Assert
        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void Approve_WhenApproverIsEmpty_ThrowsDomainException()
    {
        // Arrange
        var briefing = UnderReview();

        // Act
        var act = () => briefing.Approve(Guid.Empty);

        // Assert
        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void Reject_WhenJustificationIsProvided_ChangesStatusToRejected()
    {
        // Arrange
        var briefing = UnderReview();

        // Act
        briefing.Reject("Budget is not approved for this quarter.", Guid.NewGuid());

        // Assert
        briefing.Status.Should().Be(BriefingStatus.Rejected);
    }

    [Fact]
    public void Reject_WhenJustificationIsProvided_StoresJustification()
    {
        // Arrange
        var briefing = UnderReview();

        // Act
        briefing.Reject("Budget is not approved for this quarter.", Guid.NewGuid());

        // Assert
        briefing.DecisionJustification!.Value.Should().Be("Budget is not approved for this quarter.");
    }

    [Fact]
    public void Reject_WhenSuccessful_RaisesBriefingRejectedEventWithJustification()
    {
        // Arrange
        var briefing = UnderReview();

        // Act
        briefing.Reject("Out of scope.", Guid.NewGuid());

        // Assert
        briefing.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<BriefingRejected>()
            .Which.Justification.Should().Be("Out of scope.");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Reject_WhenJustificationIsNullOrWhitespace_ThrowsDomainException(string? justification)
    {
        // Arrange
        var briefing = UnderReview();

        // Act
        var act = () => briefing.Reject(justification, Guid.NewGuid());

        // Assert
        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void Reject_WhenJustificationIsMissing_KeepsStatusAwaitingApproval()
    {
        // Arrange
        var briefing = UnderReview();

        // Act
        var act = () => briefing.Reject("", Guid.NewGuid());

        // Assert
        act.Should().Throw<DomainException>();
        briefing.Status.Should().Be(BriefingStatus.AwaitingApproval);
    }

    [Fact]
    public void Reject_WhenJustificationIsMissing_DoesNotRaiseEvent()
    {
        // Arrange
        var briefing = UnderReview();

        // Act
        var act = () => briefing.Reject("", Guid.NewGuid());

        // Assert
        act.Should().Throw<DomainException>();
        briefing.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void Reject_WhenJustificationExceedsMaxLength_ThrowsDomainException()
    {
        // Arrange
        var briefing = UnderReview();
        var justification = new string('a', Justification.MaxLength + 1);

        // Act
        var act = () => briefing.Reject(justification, Guid.NewGuid());

        // Assert
        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void Reject_WhenStatusIsDraft_ThrowsDomainException()
    {
        // Arrange
        var briefing = NewDraft();

        // Act
        var act = () => briefing.Reject("Not reviewable yet.", Guid.NewGuid());

        // Assert
        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void Reject_WhenRejecterIsEmpty_ThrowsDomainException()
    {
        // Arrange
        var briefing = UnderReview();

        // Act
        var act = () => briefing.Reject("Out of scope.", Guid.Empty);

        // Assert
        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void RequestChanges_WhenJustificationIsProvided_ChangesStatusToChangesRequested()
    {
        // Arrange
        var briefing = UnderReview();

        // Act
        briefing.RequestChanges("Add the target audience.", Guid.NewGuid());

        // Assert
        briefing.Status.Should().Be(BriefingStatus.ChangesRequested);
    }

    [Fact]
    public void RequestChanges_WhenJustificationIsProvided_StoresJustification()
    {
        // Arrange
        var briefing = UnderReview();

        // Act
        briefing.RequestChanges("Add the target audience.", Guid.NewGuid());

        // Assert
        briefing.DecisionJustification!.Value.Should().Be("Add the target audience.");
    }

    [Fact]
    public void RequestChanges_WhenSuccessful_RaisesChangesRequestedEvent()
    {
        // Arrange
        var briefing = UnderReview();
        var managerId = Guid.NewGuid();

        // Act
        briefing.RequestChanges("Add the target audience.", managerId);

        // Assert
        briefing.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<ChangesRequested>()
            .Which.RequestedById.Should().Be(managerId);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void RequestChanges_WhenJustificationIsNullOrWhitespace_ThrowsDomainException(string? justification)
    {
        // Arrange
        var briefing = UnderReview();

        // Act
        var act = () => briefing.RequestChanges(justification, Guid.NewGuid());

        // Assert
        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void RequestChanges_WhenStatusIsApproved_ThrowsDomainException()
    {
        // Arrange
        var briefing = UnderReview();
        ForceStatus(briefing, BriefingStatus.Approved);

        // Act
        var act = () => briefing.RequestChanges("Add the target audience.", Guid.NewGuid());

        // Assert
        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void RequestChanges_WhenRequesterOfChangesIsEmpty_ThrowsDomainException()
    {
        // Arrange
        var briefing = UnderReview();

        // Act
        var act = () => briefing.RequestChanges("Add the target audience.", Guid.Empty);

        // Assert
        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void SubmitForApproval_WhenResubmittedAfterChangesRequested_ReturnsToAwaitingApproval()
    {
        // Arrange
        var briefing = UnderReview();
        briefing.RequestChanges("Add the target audience.", Guid.NewGuid());
        briefing.Edit("Campaign", Priority.High, "d", "o", "Marketing", DateOnly.FromDateTime(DateTime.UtcNow).AddDays(7));

        // Act
        var result = briefing.SubmitForApproval();

        // Assert
        result.IsSuccess.Should().BeTrue();
        briefing.Status.Should().Be(BriefingStatus.AwaitingApproval);
    }

    [Fact]
    public void SubmitForApproval_WhenResubmittedAfterChangesRequested_ClearsPreviousDecision()
    {
        // Arrange
        var briefing = UnderReview();
        briefing.RequestChanges("Add the target audience.", Guid.NewGuid());

        // Act
        briefing.SubmitForApproval();

        // Assert
        briefing.DecisionJustification.Should().BeNull();
        briefing.ReviewedById.Should().BeNull();
        briefing.ReviewedAt.Should().BeNull();
    }

    private static Briefing Approved()
    {
        var briefing = UnderReview();
        briefing.Approve(Guid.NewGuid());
        briefing.ClearDomainEvents();
        return briefing;
    }

    [Fact]
    public void StartExecution_WhenStatusIsApproved_ChangesStatusToInProgress()
    {
        // Arrange
        var briefing = Approved();

        // Act
        briefing.StartExecution();

        // Assert
        briefing.Status.Should().Be(BriefingStatus.InProgress);
    }

    [Fact]
    public void StartExecution_WhenStatusIsAlreadyInProgress_KeepsStatusAndDoesNotThrow()
    {
        // Arrange
        var briefing = Approved();
        briefing.StartExecution();

        // Act
        var act = () => briefing.StartExecution();

        // Assert
        act.Should().NotThrow();
        briefing.Status.Should().Be(BriefingStatus.InProgress);
    }

    [Theory]
    [InlineData(BriefingStatus.Draft)]
    [InlineData(BriefingStatus.AwaitingApproval)]
    [InlineData(BriefingStatus.ChangesRequested)]
    [InlineData(BriefingStatus.Rejected)]
    [InlineData(BriefingStatus.Completed)]
    [InlineData(BriefingStatus.Cancelled)]
    public void StartExecution_WhenStatusIsNotApproved_ThrowsDomainException(BriefingStatus status)
    {
        // Arrange
        var briefing = NewDraft();
        ForceStatus(briefing, status);

        // Act
        var act = () => briefing.StartExecution();

        // Assert
        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void StartExecution_WhenExecutionStarted_BlocksCancellation()
    {
        // Arrange
        var briefing = Approved();
        briefing.StartExecution();

        // Act
        var act = () => briefing.Cancel();

        // Assert
        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void Complete_WhenStatusIsInProgress_ChangesStatusToCompleted()
    {
        // Arrange
        var briefing = Approved();
        briefing.StartExecution();

        // Act
        briefing.Complete();

        // Assert
        briefing.Status.Should().Be(BriefingStatus.Completed);
    }

    [Fact]
    public void Complete_WhenStatusIsAlreadyCompleted_KeepsStatusAndDoesNotThrow()
    {
        // Arrange
        var briefing = Approved();
        briefing.StartExecution();
        briefing.Complete();

        // Act
        var act = () => briefing.Complete();

        // Assert
        act.Should().NotThrow();
        briefing.Status.Should().Be(BriefingStatus.Completed);
    }

    [Theory]
    [InlineData(BriefingStatus.Draft)]
    [InlineData(BriefingStatus.AwaitingApproval)]
    [InlineData(BriefingStatus.ChangesRequested)]
    [InlineData(BriefingStatus.Approved)]
    [InlineData(BriefingStatus.Rejected)]
    [InlineData(BriefingStatus.Cancelled)]
    public void Complete_WhenExecutionHasNotStarted_ThrowsDomainException(BriefingStatus status)
    {
        // Arrange
        var briefing = NewDraft();
        ForceStatus(briefing, status);

        // Act
        var act = () => briefing.Complete();

        // Assert
        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void Complete_WhenExecutionHasNotStarted_KeepsStatus()
    {
        // Arrange
        var briefing = Approved();

        // Act
        var act = () => briefing.Complete();

        // Assert
        act.Should().Throw<DomainException>();
        briefing.Status.Should().Be(BriefingStatus.Approved);
    }

    [Fact]
    public void Reopen_WhenStatusIsCompleted_ChangesStatusToInProgress()
    {
        // Arrange
        var briefing = Approved();
        briefing.StartExecution();
        briefing.Complete();

        // Act
        briefing.Reopen();

        // Assert
        briefing.Status.Should().Be(BriefingStatus.InProgress);
    }

    [Fact]
    public void Reopen_WhenStatusIsAlreadyInProgress_KeepsStatusAndDoesNotThrow()
    {
        // Arrange
        var briefing = Approved();
        briefing.StartExecution();

        // Act
        var act = () => briefing.Reopen();

        // Assert
        act.Should().NotThrow();
        briefing.Status.Should().Be(BriefingStatus.InProgress);
    }

    [Theory]
    [InlineData(BriefingStatus.Draft)]
    [InlineData(BriefingStatus.AwaitingApproval)]
    [InlineData(BriefingStatus.ChangesRequested)]
    [InlineData(BriefingStatus.Approved)]
    [InlineData(BriefingStatus.Rejected)]
    [InlineData(BriefingStatus.Cancelled)]
    public void Reopen_WhenBriefingNeverCompleted_ThrowsDomainException(BriefingStatus status)
    {
        // Arrange
        var briefing = NewDraft();
        ForceStatus(briefing, status);

        // Act
        var act = () => briefing.Reopen();

        // Assert
        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void Reopen_WhenBriefingNeverCompleted_KeepsStatus()
    {
        // Arrange
        var briefing = Approved();

        // Act
        var act = () => briefing.Reopen();

        // Assert
        act.Should().Throw<DomainException>();
        briefing.Status.Should().Be(BriefingStatus.Approved);
    }

    /// <summary>
    /// The point of RF025 bringing the briefing back: a reopened task must be able to receive the corrected
    /// file, and <c>AddDelivery</c> only accepts one while the briefing is in execution (RF022).
    /// </summary>
    [Fact]
    public void Reopen_WhenBriefingWasCompleted_AllowsDeliveriesAgain()
    {
        // Arrange
        var briefing = Approved();
        briefing.StartExecution();
        briefing.Complete();
        briefing.Reopen();

        // Act
        var delivery = briefing.AddDelivery(Guid.NewGuid(), "arte-v2.png", "/storage/arte-v2.png", 4096, Guid.NewGuid());

        // Assert
        briefing.Attachments.Should().Contain(delivery);
    }

    [Fact]
    public void AddAttachment_WhenStatusIsDraft_AddsToCollection()
    {
        // Arrange
        var briefing = NewDraft();

        // Act
        var attachment = briefing.AddAttachment("logo.png", "/storage/logo.png", 2048, Guid.NewGuid());

        // Assert
        briefing.Attachments.Should().ContainSingle().Which.Should().Be(attachment);
    }

    [Fact]
    public void AddAttachment_WhenStatusIsApproved_ThrowsDomainException()
    {
        // Arrange
        var briefing = NewDraft();
        ForceStatus(briefing, BriefingStatus.Approved);

        // Act
        var act = () => briefing.AddAttachment("logo.png", "/storage/logo.png", 2048, Guid.NewGuid());

        // Assert
        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void AddDelivery_WhenBriefingIsInProgress_AddsToCollection()
    {
        // Arrange
        var briefing = NewDraft();
        ForceStatus(briefing, BriefingStatus.InProgress);
        var taskId = Guid.NewGuid();

        // Act
        var delivery = briefing.AddDelivery(taskId, "key-visual.psd", "2026/07/29/abc.psd", 4096, Guid.NewGuid());

        // Assert
        briefing.Attachments.Should().ContainSingle().Which.Should().Be(delivery);
    }

    [Fact]
    public void AddDelivery_WhenBriefingIsInProgress_LinksBothTaskAndBriefing()
    {
        // Arrange
        var briefing = NewDraft();
        ForceStatus(briefing, BriefingStatus.InProgress);
        var taskId = Guid.NewGuid();
        var uploaderId = Guid.NewGuid();

        // Act
        var delivery = briefing.AddDelivery(taskId, "key-visual.psd", "2026/07/29/abc.psd", 4096, uploaderId);

        // Assert
        delivery.TaskId.Should().Be(taskId);
        delivery.BriefingId.Should().Be(briefing.Id);
        delivery.UploadedById.Should().Be(uploaderId);
    }

    [Theory]
    [InlineData(BriefingStatus.Draft)]
    [InlineData(BriefingStatus.AwaitingApproval)]
    [InlineData(BriefingStatus.ChangesRequested)]
    [InlineData(BriefingStatus.Approved)]
    [InlineData(BriefingStatus.Rejected)]
    [InlineData(BriefingStatus.Completed)]
    [InlineData(BriefingStatus.Cancelled)]
    public void AddDelivery_WhenBriefingIsNotInProgress_ThrowsDomainException(BriefingStatus status)
    {
        // Arrange
        var briefing = NewDraft();
        ForceStatus(briefing, status);

        // Act
        var act = () => briefing.AddDelivery(Guid.NewGuid(), "key-visual.psd", "2026/07/29/abc.psd", 4096, Guid.NewGuid());

        // Assert
        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void AddDelivery_WhenTaskIdIsEmpty_ThrowsDomainException()
    {
        // Arrange
        var briefing = NewDraft();
        ForceStatus(briefing, BriefingStatus.InProgress);

        // Act
        var act = () => briefing.AddDelivery(Guid.Empty, "key-visual.psd", "2026/07/29/abc.psd", 4096, Guid.NewGuid());

        // Assert
        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void AddDelivery_WhenCalledTwiceForTheSameTask_KeepsBothDeliveries()
    {
        // Arrange
        var briefing = NewDraft();
        ForceStatus(briefing, BriefingStatus.InProgress);
        var taskId = Guid.NewGuid();
        var uploaderId = Guid.NewGuid();

        // Act
        briefing.AddDelivery(taskId, "v1.psd", "2026/07/29/a.psd", 4096, uploaderId);
        briefing.AddDelivery(taskId, "v2.psd", "2026/07/29/b.psd", 8192, uploaderId);

        // Assert
        briefing.Attachments.Should().HaveCount(2);
    }

    [Fact]
    public void AddDelivery_WhenBriefingIsInProgress_DoesNotChangeStatus()
    {
        // Arrange
        var briefing = NewDraft();
        ForceStatus(briefing, BriefingStatus.InProgress);

        // Act
        briefing.AddDelivery(Guid.NewGuid(), "key-visual.psd", "2026/07/29/abc.psd", 4096, Guid.NewGuid());

        // Assert
        briefing.Status.Should().Be(BriefingStatus.InProgress);
    }

    // --- Overdue marking (RF028, BRF-043) ---------------------------------------------------------------

    private static readonly DateTime DueAtUtc = new(2026, 8, 1, 12, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime DetectedAtUtc = new(2026, 8, 2, 0, 15, 0, DateTimeKind.Utc);

    [Fact]
    public void MarkOverdue_WhenBriefingIsInExecution_RecordsTheDetectionInstant()
    {
        // Arrange
        var briefing = NewDraft();
        ForceStatus(briefing, BriefingStatus.InProgress);

        // Act
        briefing.MarkOverdue(DueAtUtc, DetectedAtUtc);

        // Assert
        briefing.OverdueSince.Should().Be(DetectedAtUtc);
    }

    [Fact]
    public void MarkOverdue_WhenBriefingIsInExecution_RaisesDeadlineOverdueForTheRequester()
    {
        // Arrange
        var briefing = NewDraft();
        ForceStatus(briefing, BriefingStatus.InProgress);

        // Act
        briefing.MarkOverdue(DueAtUtc, DetectedAtUtc);

        // Assert
        briefing.DomainEvents.OfType<DeadlineOverdue>().Single()
            .Should().BeEquivalentTo(new
            {
                Subject = DemandSubject.Briefing,
                SubjectId = briefing.Id,
                BriefingId = briefing.Id,
                ResponsibleId = briefing.RequesterId,
                DueAtUtc,
                DetectedAtUtc
            });
    }

    [Fact]
    public void MarkOverdue_WhenBriefingIsAwaitingApproval_MarksItAnyway()
    {
        // Arrange
        // Sitting unreviewed past the requested date is exactly the delay RF030 exists to surface.
        var briefing = UnderReview();

        // Act
        briefing.MarkOverdue(DueAtUtc, DetectedAtUtc);

        // Assert
        briefing.IsOverdue.Should().BeTrue();
    }

    [Fact]
    public void MarkOverdue_WhenBriefingIsAlreadyMarked_KeepsTheFirstDetectionInstant()
    {
        // Arrange
        var briefing = NewDraft();
        ForceStatus(briefing, BriefingStatus.InProgress);
        briefing.MarkOverdue(DueAtUtc, DetectedAtUtc);

        // Act
        briefing.MarkOverdue(DueAtUtc, DetectedAtUtc.AddHours(1));

        // Assert
        briefing.OverdueSince.Should().Be(DetectedAtUtc);
    }

    [Fact]
    public void MarkOverdue_WhenBriefingIsAlreadyMarked_DoesNotRaiseTheEventAgain()
    {
        // Arrange
        var briefing = NewDraft();
        ForceStatus(briefing, BriefingStatus.InProgress);
        briefing.MarkOverdue(DueAtUtc, DetectedAtUtc);

        // Act
        briefing.MarkOverdue(DueAtUtc, DetectedAtUtc.AddHours(1));

        // Assert
        briefing.DomainEvents.OfType<DeadlineOverdue>().Should().HaveCount(1);
    }

    [Theory]
    [InlineData(BriefingStatus.Draft)]
    [InlineData(BriefingStatus.Rejected)]
    [InlineData(BriefingStatus.Cancelled)]
    [InlineData(BriefingStatus.Completed)]
    public void MarkOverdue_WhenBriefingIsNotUnderTheSectorsWatch_ThrowsDomainException(BriefingStatus status)
    {
        // Arrange
        var briefing = NewDraft();
        ForceStatus(briefing, status);

        // Act
        var act = () => briefing.MarkOverdue(DueAtUtc, DetectedAtUtc);

        // Assert
        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void Edit_WhenBriefingWasOverdue_ClearsTheMark()
    {
        // Arrange
        // AwaitingApproval is both editable (RF009) and swept by the monitor, so one briefing covers both.
        var briefing = UnderReview();
        briefing.MarkOverdue(DueAtUtc, DetectedAtUtc);

        // Act
        briefing.Edit(
            "Launch campaign",
            Priority.High,
            "Social media assets",
            "Promote the new product",
            "Marketing",
            DateOnly.FromDateTime(DateTime.UtcNow).AddDays(30));

        // Assert
        briefing.OverdueSince.Should().BeNull();
    }

    [Fact]
    public void Complete_WhenBriefingWasOverdue_KeepsTheMark()
    {
        // Arrange
        // The mark is the record that this demand was delivered late; completion does not erase it.
        var briefing = NewDraft();
        ForceStatus(briefing, BriefingStatus.InProgress);
        briefing.MarkOverdue(DueAtUtc, DetectedAtUtc);

        // Act
        briefing.Complete();

        // Assert
        briefing.OverdueSince.Should().Be(DetectedAtUtc);
    }

    [Fact]
    public void IsOverdue_WhenBriefingWasNeverMarked_ReturnsFalse()
    {
        // Arrange
        var briefing = NewDraft();

        // Act
        var isOverdue = briefing.IsOverdue;

        // Assert
        isOverdue.Should().BeFalse();
    }

    // --- Relevant changes (RF034, BRF-049) -----------------------------------------------------------------

    [Fact]
    public void Edit_WhenBriefingIsUnderReview_RaisesEntityChanged()
    {
        // Arrange
        var briefing = UnderReview();

        // Act
        briefing.Edit("Launch campaign v2", Priority.High, null, null, null, null);

        // Assert
        briefing.DomainEvents.OfType<EntityChanged>().Should().ContainSingle()
            .Which.Should().BeEquivalentTo(
                new EntityChanged(
                    DemandSubject.Briefing,
                    briefing.Id,
                    briefing.Id,
                    EntityChangeKind.Edited,
                    null));
    }

    /// <summary>
    /// RF034 speaks of the people <b>involved</b>; a draft has none besides the person editing it, so the
    /// edit announces nothing.
    /// </summary>
    [Fact]
    public void Edit_WhenBriefingIsStillADraft_RaisesNoEntityChanged()
    {
        // Arrange
        var briefing = CompleteDraft();

        // Act
        briefing.Edit("Launch campaign v2", Priority.High, null, null, null, null);

        // Assert
        briefing.DomainEvents.OfType<EntityChanged>().Should().BeEmpty();
    }

    [Fact]
    public void Cancel_WhenBriefingWasSubmitted_RaisesEntityChanged()
    {
        // Arrange
        var briefing = UnderReview();

        // Act
        briefing.Cancel();

        // Assert
        briefing.DomainEvents.OfType<EntityChanged>().Should().ContainSingle()
            .Which.Change.Should().Be(EntityChangeKind.Cancelled);
    }

    [Fact]
    public void Cancel_WhenBriefingIsStillADraft_RaisesNoEntityChanged()
    {
        // Arrange
        var briefing = NewDraft();

        // Act
        briefing.Cancel();

        // Assert
        briefing.DomainEvents.OfType<EntityChanged>().Should().BeEmpty();
    }

    [Fact]
    public void Complete_WhenBriefingIsCompleted_RaisesEntityChanged()
    {
        // Arrange
        var briefing = NewDraft();
        ForceStatus(briefing, BriefingStatus.InProgress);

        // Act
        briefing.Complete();

        // Assert
        briefing.DomainEvents.OfType<EntityChanged>().Should().ContainSingle()
            .Which.Change.Should().Be(EntityChangeKind.Completed);
    }

    /// <summary>
    /// Delivery is at least once (BRF-049), so a redelivered completion event must not produce a second
    /// "your briefing is done".
    /// </summary>
    [Fact]
    public void Complete_WhenBriefingIsAlreadyCompleted_RaisesNoEntityChanged()
    {
        // Arrange
        var briefing = NewDraft();
        ForceStatus(briefing, BriefingStatus.Completed);

        // Act
        briefing.Complete();

        // Assert
        briefing.DomainEvents.OfType<EntityChanged>().Should().BeEmpty();
    }

    [Fact]
    public void Reopen_WhenBriefingReturnsToExecution_RaisesEntityChanged()
    {
        // Arrange
        var briefing = NewDraft();
        ForceStatus(briefing, BriefingStatus.Completed);

        // Act
        briefing.Reopen();

        // Assert
        briefing.DomainEvents.OfType<EntityChanged>().Should().ContainSingle()
            .Which.Change.Should().Be(EntityChangeKind.Reopened);
    }

    [Fact]
    public void Reopen_WhenBriefingIsAlreadyInProgress_RaisesNoEntityChanged()
    {
        // Arrange
        var briefing = NewDraft();
        ForceStatus(briefing, BriefingStatus.InProgress);

        // Act
        briefing.Reopen();

        // Assert
        briefing.DomainEvents.OfType<EntityChanged>().Should().BeEmpty();
    }

    /// <summary>
    /// The review transitions have events of their own (BRF-015); a second one here would notify the
    /// requester twice for the same decision (RF031, RF032).
    /// </summary>
    [Fact]
    public void Approve_WhenBriefingIsApproved_RaisesNoEntityChanged()
    {
        // Arrange
        var briefing = UnderReview();

        // Act
        briefing.Approve(Guid.NewGuid());

        // Assert
        briefing.DomainEvents.OfType<EntityChanged>().Should().BeEmpty();
    }

    [Fact]
    public void Reject_WhenBriefingIsRejected_RaisesNoEntityChanged()
    {
        // Arrange
        var briefing = UnderReview();

        // Act
        briefing.Reject("Out of budget.", Guid.NewGuid());

        // Assert
        briefing.DomainEvents.OfType<EntityChanged>().Should().BeEmpty();
    }

    [Fact]
    public void RequestChanges_WhenChangesAreRequested_RaisesNoEntityChanged()
    {
        // Arrange
        var briefing = UnderReview();

        // Act
        briefing.RequestChanges("Add the objective.", Guid.NewGuid());

        // Assert
        briefing.DomainEvents.OfType<EntityChanged>().Should().BeEmpty();
    }
}
