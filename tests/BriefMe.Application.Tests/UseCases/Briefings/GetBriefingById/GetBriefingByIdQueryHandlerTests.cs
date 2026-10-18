using BriefMe.Application.Abstractions;
using BriefMe.Application.Common.Briefings;
using BriefMe.Application.Common.Errors;
using BriefMe.Application.UseCases.Briefings.GetBriefingById;
using BriefMe.Domain.Common;
using BriefMe.Domain.Entities;
using BriefMe.Domain.Enums;
using BriefMe.Domain.Repositories;
using BriefMe.Domain.ValueObjects;
using FluentAssertions;
using Moq;

namespace BriefMe.Application.Tests.UseCases.Briefings.GetBriefingById;

public class GetBriefingByIdQueryHandlerTests
{
    private readonly Mock<IBriefingRepository> _briefingRepositoryMock = new();
    private readonly Mock<ICategoryRepository> _categoryRepositoryMock = new();
    private readonly Mock<IUserRepository> _userRepositoryMock = new();
    private readonly Mock<ICurrentUser> _currentUserMock = new();
    private readonly Mock<IAttachmentLinkSigner> _signerMock = new();
    private readonly Guid _requesterId = Guid.NewGuid();
    private readonly GetBriefingByIdQueryHandler _handler;

    public GetBriefingByIdQueryHandlerTests()
    {
        _handler = new GetBriefingByIdQueryHandler(
            _briefingRepositoryMock.Object,
            new BriefingComplementsLoader(_categoryRepositoryMock.Object, _userRepositoryMock.Object),
            _currentUserMock.Object,
            _signerMock.Object);

        _categoryRepositoryMock
            .Setup(r => r.GetByIdsAsync(It.IsAny<IReadOnlyCollection<Guid>>(), default))
            .ReturnsAsync([]);
        _userRepositoryMock
            .Setup(r => r.GetByIdsAsync(It.IsAny<IReadOnlyCollection<Guid>>(), default))
            .ReturnsAsync([]);
    }

    private Briefing SubmittedBriefing()
    {
        var briefing = Briefing.Create("Banner", Priority.Medium, Guid.NewGuid(), _requesterId,
            "description", "objective", "Marketing", DateOnly.FromDateTime(DateTime.UtcNow).AddDays(5));
        briefing.Submit();
        _briefingRepositoryMock.Setup(r => r.GetByIdWithAttachmentsAsync(briefing.Id, default)).ReturnsAsync(briefing);
        return briefing;
    }

    private void ActAs(Guid userId, UserRole role)
    {
        _currentUserMock.Setup(u => u.UserId).Returns(userId.ToString());
        _currentUserMock.Setup(u => u.Role).Returns(role);
    }

    [Fact]
    public async Task Handle_WhenCallerOwnsTheBriefing_ReturnsDetail()
    {
        // Arrange
        var briefing = SubmittedBriefing();
        ActAs(_requesterId, UserRole.Requester);

        // Act
        var result = await _handler.Handle(new GetBriefingByIdQuery(briefing.Id), default);

        // Assert
        result.Value.Id.Should().Be(briefing.Id);
    }

    [Fact]
    public async Task Handle_WhenAnotherRequesterOpensAQueuedBriefing_ReturnsForbidden()
    {
        // Arrange
        var briefing = SubmittedBriefing();
        ActAs(Guid.NewGuid(), UserRole.Requester);

        // Act
        var result = await _handler.Handle(new GetBriefingByIdQuery(briefing.Id), default);

        // Assert
        result.Type.Should().Be(ErrorType.Forbidden);
        result.Error.Should().Be(BriefingErrors.NotVisible);
    }

    [Fact]
    public async Task Handle_WhenAnyDesignerOpensAQueuedBriefing_ReturnsDetail()
    {
        // Arrange
        var briefing = SubmittedBriefing();
        ActAs(Guid.NewGuid(), UserRole.Designer);

        // Act
        var result = await _handler.Handle(new GetBriefingByIdQuery(briefing.Id), default);

        // Assert
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_WhenAssignedDesignerOpensIt_ReturnsDetail()
    {
        // Arrange
        var designerId = Guid.NewGuid();
        var briefing = SubmittedBriefing();
        briefing.Claim(designerId);
        ActAs(designerId, UserRole.Designer);

        // Act
        var result = await _handler.Handle(new GetBriefingByIdQuery(briefing.Id), default);

        // Assert
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_WhenAnotherDesignerOpensAClaimedBriefing_ReturnsForbidden()
    {
        // Arrange
        var briefing = SubmittedBriefing();
        briefing.Claim(Guid.NewGuid());
        ActAs(Guid.NewGuid(), UserRole.Designer);

        // Act
        var result = await _handler.Handle(new GetBriefingByIdQuery(briefing.Id), default);

        // Assert
        result.Type.Should().Be(ErrorType.Forbidden);
    }

    [Fact]
    public async Task Handle_WhenBriefingNotFound_ReturnsNotFound()
    {
        // Arrange
        var id = Guid.NewGuid();
        ActAs(_requesterId, UserRole.Requester);
        _briefingRepositoryMock.Setup(r => r.GetByIdWithAttachmentsAsync(id, default)).ReturnsAsync((Briefing?)null);

        // Act
        var result = await _handler.Handle(new GetBriefingByIdQuery(id), default);

        // Assert
        result.Type.Should().Be(ErrorType.NotFound);
    }

    [Fact]
    public async Task Handle_WhenCurrentUserIsNotAuthenticated_ReturnsUnauthorizedWithoutLoading()
    {
        // Arrange
        _currentUserMock.Setup(u => u.UserId).Returns((string?)null);

        // Act
        var result = await _handler.Handle(new GetBriefingByIdQuery(Guid.NewGuid()), default);

        // Assert
        result.Type.Should().Be(ErrorType.Unauthorized);
        _briefingRepositoryMock.Verify(r => r.GetByIdWithAttachmentsAsync(It.IsAny<Guid>(), default), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenBriefingHasAttachments_ReturnsThemInUploadOrderWithTheirTokens()
    {
        // Arrange
        var briefing = SubmittedBriefing();
        var first = briefing.AddAttachment("first.pdf", "briefings/first.pdf", AttachmentFormat.Pdf, 100);
        var second = briefing.AddAttachment("second.mp4", "briefings/second.mp4", AttachmentFormat.Mp4, 200);
        typeof(BriefingAttachment).GetProperty(nameof(BriefingAttachment.UploadedAt))!.SetValue(first, DateTime.UtcNow.AddMinutes(1));
        _signerMock.Setup(s => s.Issue(briefing.Id, first.Id)).Returns("token-first");
        _signerMock.Setup(s => s.Issue(briefing.Id, second.Id)).Returns("token-second");
        ActAs(_requesterId, UserRole.Requester);

        // Act
        var result = await _handler.Handle(new GetBriefingByIdQuery(briefing.Id), default);

        // Assert
        result.Value.Attachments.Select(a => (a.FileName, a.Kind, a.ContentToken)).Should().Equal(
            ("second.mp4", AttachmentKind.Video, "token-second"),
            ("first.pdf", AttachmentKind.Pdf, "token-first"));
    }

    [Fact]
    public async Task Handle_WhenBriefingHasNoAttachments_ReturnsEmptyList()
    {
        // Arrange
        var briefing = SubmittedBriefing();
        ActAs(_requesterId, UserRole.Requester);

        // Act
        var result = await _handler.Handle(new GetBriefingByIdQuery(briefing.Id), default);

        // Assert
        result.Value.Attachments.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_WhenCallerCannotSeeTheBriefing_IssuesNoToken()
    {
        // Arrange
        var briefing = SubmittedBriefing();
        briefing.AddAttachment("secret.pdf", "briefings/secret.pdf", AttachmentFormat.Pdf, 100);
        ActAs(Guid.NewGuid(), UserRole.Requester);

        // Act
        await _handler.Handle(new GetBriefingByIdQuery(briefing.Id), default);

        // Assert
        _signerMock.Verify(s => s.Issue(It.IsAny<Guid>(), It.IsAny<Guid>()), Times.Never);
    }
}
