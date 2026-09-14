using BriefMe.Application.Abstractions;
using BriefMe.Application.UseCases.Briefings.EditBriefing;
using BriefMe.Application.UseCases.Briefings.RequestChangesBriefing;
using BriefMe.Application.UseCases.Briefings.SubmitBriefingForApproval;
using BriefMe.Domain.Entities;
using BriefMe.Domain.Enums;
using BriefMe.Domain.Repositories;
using FluentAssertions;
using Moq;

namespace BriefMe.Application.Tests.UseCases.Briefings.RequestChangesBriefing;

/// <summary>
/// Acceptance criterion of BRF-018 (RF014): "given changes requested, when the requester edits and resubmits,
/// then the briefing returns to AwaitingApproval". The cycle spans three handlers, so no single handler test
/// can prove it — these run the real handlers in sequence over one aggregate, with only the repository, the
/// unit of work and the authenticated identity mocked. No reflection is used to force state: every transition
/// is reached through the use cases themselves.
/// </summary>
public class RequestChangesReviewCycleTests
{
    private const string ChangeRequestReason = "Add the target audience and the distribution channel.";

    private readonly Mock<IBriefingRepository> _briefingRepositoryMock = new();
    private readonly Mock<IUnitOfWork> _unitOfWorkMock = new();
    private readonly Guid _managerId = Guid.NewGuid();
    private readonly Guid _requesterId = Guid.NewGuid();
    private readonly Briefing _briefing;

    private readonly RequestChangesBriefingCommandHandler _requestChangesHandler;
    private readonly EditBriefingCommandHandler _editHandler;
    private readonly SubmitBriefingForApprovalCommandHandler _submitHandler;

    public RequestChangesReviewCycleTests()
    {
        _briefing = BriefingUnderReview();

        _briefingRepositoryMock
            .Setup(r => r.GetByIdAsync(_briefing.Id, default))
            .ReturnsAsync(_briefing);

        _requestChangesHandler = new RequestChangesBriefingCommandHandler(
            _briefingRepositoryMock.Object,
            CurrentUser(_managerId),
            _unitOfWorkMock.Object);

        _editHandler = new EditBriefingCommandHandler(
            _briefingRepositoryMock.Object,
            CurrentUser(_requesterId),
            _unitOfWorkMock.Object);

        _submitHandler = new SubmitBriefingForApprovalCommandHandler(
            _briefingRepositoryMock.Object,
            CurrentUser(_requesterId),
            _unitOfWorkMock.Object);
    }

    private static ICurrentUser CurrentUser(Guid userId)
    {
        var mock = new Mock<ICurrentUser>();
        mock.Setup(u => u.UserId).Returns(userId.ToString());
        return mock.Object;
    }

    private Briefing BriefingUnderReview()
    {
        var briefing = Briefing.Create(
            "Launch campaign",
            Priority.High,
            Guid.NewGuid(),
            _requesterId,
            description: "Social media assets",
            objective: "Promote the new product",
            requestingArea: "Marketing",
            desiredDeliveryDate: DateOnly.FromDateTime(DateTime.UtcNow).AddDays(10));

        briefing.AddAttachment("brief.pdf", "/storage/brief.pdf", 1024, _requesterId);
        briefing.SubmitForApproval();

        return briefing;
    }

    private EditBriefingCommand CorrectionCommand() => new(
        _briefing.Id,
        "Launch campaign — revised",
        Priority.High,
        Description: "Social media assets for the launch",
        Objective: "Promote the new product to young professionals",
        RequestingArea: "Marketing",
        DesiredDeliveryDate: DateOnly.FromDateTime(DateTime.UtcNow).AddDays(15));

    private async Task RequestChangesAsync() =>
        await _requestChangesHandler.Handle(
            new RequestChangesBriefingCommand(_briefing.Id, ChangeRequestReason),
            default);

    [Fact]
    public async Task Cycle_WhenChangesAreRequested_TheRequesterCanEditTheBriefing()
    {
        // Arrange
        await RequestChangesAsync();

        // Act
        var edit = await _editHandler.Handle(CorrectionCommand(), default);

        // Assert
        edit.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task Cycle_WhenTheRequesterEditsAndResubmits_TheBriefingReturnsToAwaitingApproval()
    {
        // Arrange
        await RequestChangesAsync();
        await _editHandler.Handle(CorrectionCommand(), default);

        // Act
        var resubmission = await _submitHandler.Handle(
            new SubmitBriefingForApprovalCommand(_briefing.Id),
            default);

        // Assert
        resubmission.IsSuccess.Should().BeTrue();
        resubmission.Value.Status.Should().Be(BriefingStatus.AwaitingApproval);
    }

    [Fact]
    public async Task Cycle_WhenTheBriefingIsResubmitted_ThePreviousDecisionIsCleared()
    {
        // Arrange
        await RequestChangesAsync();
        await _editHandler.Handle(CorrectionCommand(), default);

        // Act
        await _submitHandler.Handle(new SubmitBriefingForApprovalCommand(_briefing.Id), default);

        // Assert
        _briefing.DecisionJustification.Should().BeNull();
        _briefing.ReviewedById.Should().BeNull();
    }
}
