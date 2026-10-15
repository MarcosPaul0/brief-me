using BriefMe.Application.Abstractions;
using BriefMe.Application.Common.Briefings;
using BriefMe.Application.Common.Errors;
using BriefMe.Application.Tests.TestDoubles;
using BriefMe.Application.UseCases.Briefings.CreateBriefing;
using BriefMe.Domain.Common;
using BriefMe.Domain.Entities;
using BriefMe.Domain.Enums;
using BriefMe.Domain.Repositories;
using FluentAssertions;
using Moq;

namespace BriefMe.Application.Tests.UseCases.Briefings.CreateBriefing;

public class CreateBriefingCommandHandlerTests
{
    private readonly Mock<IBriefingRepository> _briefingRepositoryMock = new();
    private readonly Mock<ICategoryRepository> _categoryRepositoryMock = new();
    private readonly Mock<ICurrentUser> _currentUserMock = new();
    private readonly Mock<IUnitOfWork> _unitOfWorkMock = new();
    private readonly Mock<IFileStorage> _fileStorageMock = new();
    private readonly Guid _requesterId = Guid.NewGuid();
    private readonly CreateBriefingCommandHandler _handler;

    public CreateBriefingCommandHandlerTests()
    {
        _handler = new CreateBriefingCommandHandler(
            _briefingRepositoryMock.Object,
            _categoryRepositoryMock.Object,
            _currentUserMock.Object,
            _unitOfWorkMock.Object,
            new BriefingAttachmentStager(_fileStorageMock.Object));

        _currentUserMock.Setup(u => u.UserId).Returns(_requesterId.ToString());
        _categoryRepositoryMock
            .Setup(r => r.ExistsActiveAsync(It.IsAny<Guid>(), default))
            .ReturnsAsync(true);
    }

    [Fact]
    public async Task Handle_WhenCommandIsValid_PersistsDraftAndReturnsDto()
    {
        // Arrange
        var categoryId = Guid.NewGuid();
        var command = new CreateBriefingCommand(
            "Summer campaign",
            Priority.High,
            categoryId,
            Description: "Social media assets",
            Objective: "Increase engagement");

        // Act
        var result = await _handler.Handle(command, default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Status.Should().Be(BriefingStatus.Draft);
        result.Value.Title.Should().Be("Summer campaign");
        result.Value.CategoryId.Should().Be(categoryId);
        result.Value.RequesterId.Should().Be(_requesterId);
    }

    [Fact]
    public async Task Handle_WhenCommandIsValid_AddsBriefingAndCommitsOnce()
    {
        // Arrange
        var command = new CreateBriefingCommand("Summer campaign", Priority.Medium, Guid.NewGuid());

        // Act
        await _handler.Handle(command, default);

        // Assert
        _briefingRepositoryMock.Verify(r => r.AddAsync(It.IsAny<Briefing>(), default), Times.Once);
        _unitOfWorkMock.Verify(u => u.CommitAsync(default), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenCategoryDoesNotExist_ReturnsNotFoundWithoutPersisting()
    {
        // Arrange
        var command = new CreateBriefingCommand("Summer campaign", Priority.Medium, Guid.NewGuid());

        _categoryRepositoryMock
            .Setup(r => r.ExistsActiveAsync(command.CategoryId, default))
            .ReturnsAsync(false);

        // Act
        var result = await _handler.Handle(command, default);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Type.Should().Be(ErrorType.NotFound);
        _briefingRepositoryMock.Verify(r => r.AddAsync(It.IsAny<Briefing>(), default), Times.Never);
        _unitOfWorkMock.Verify(u => u.CommitAsync(default), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenCurrentUserIsNotAuthenticated_ReturnsUnauthorizedWithoutTouchingCategory()
    {
        // Arrange
        _currentUserMock.Setup(u => u.UserId).Returns((string?)null);
        var command = new CreateBriefingCommand("Summer campaign", Priority.Medium, Guid.NewGuid());

        // Act
        var result = await _handler.Handle(command, default);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Type.Should().Be(ErrorType.Unauthorized);
        _categoryRepositoryMock.Verify(r => r.ExistsActiveAsync(It.IsAny<Guid>(), default), Times.Never);
        _briefingRepositoryMock.Verify(r => r.AddAsync(It.IsAny<Briefing>(), default), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenAttachmentsAreValid_StoresEachFileAndAttachesItToTheBriefing()
    {
        // Arrange
        Briefing? added = null;
        _briefingRepositoryMock
            .Setup(r => r.AddAsync(It.IsAny<Briefing>(), default))
            .Callback<Briefing, CancellationToken>((briefing, _) => added = briefing);
        var command = new CreateBriefingCommand(
            "Summer campaign", Priority.Medium, Guid.NewGuid(),
            Attachments: [TestUploads.Pdf("guide.pdf"), TestUploads.Mp4("clip.mp4")]);

        // Act
        var result = await _handler.Handle(command, default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        added!.Attachments.Select(a => (a.FileName, a.Kind)).Should().Equal(
            ("guide.pdf", AttachmentKind.Pdf),
            ("clip.mp4", AttachmentKind.Video));
        _fileStorageMock.Verify(
            s => s.SaveAsync(It.Is<string>(key => key.StartsWith($"briefings/{added.Id:N}/")), It.IsAny<Stream>(), It.IsAny<string>(), default),
            Times.Exactly(2));
    }

    [Fact]
    public async Task Handle_WhenNoAttachments_DoesNotTouchStorage()
    {
        // Arrange
        var command = new CreateBriefingCommand("Summer campaign", Priority.Medium, Guid.NewGuid());

        // Act
        await _handler.Handle(command, default);

        // Assert
        _fileStorageMock.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Handle_WhenAnAttachmentHasUnsupportedFormat_FailsWithoutPersistingOrStoring()
    {
        // Arrange
        var command = new CreateBriefingCommand(
            "Summer campaign", Priority.Medium, Guid.NewGuid(),
            Attachments: [TestUploads.Pdf(), TestUploads.Html("logo.svg")]);

        // Act
        var result = await _handler.Handle(command, default);

        // Assert
        result.Error.Should().Be(BriefingErrors.AttachmentRejected("logo.svg", Domain.ValueObjects.AttachmentFormat.UnsupportedFormatMessage));
        _briefingRepositoryMock.Verify(r => r.AddAsync(It.IsAny<Briefing>(), It.IsAny<CancellationToken>()), Times.Never);
        _unitOfWorkMock.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
        _fileStorageMock.Verify(s => s.SaveAsync(It.IsAny<string>(), It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenCommitFails_DeletesStoredFilesAndRethrows()
    {
        // Arrange
        var storedKeys = new List<string>();
        _fileStorageMock
            .Setup(s => s.SaveAsync(It.IsAny<string>(), It.IsAny<Stream>(), It.IsAny<string>(), default))
            .Callback<string, Stream, string, CancellationToken>((key, _, _, _) => storedKeys.Add(key));
        _unitOfWorkMock.Setup(u => u.CommitAsync(default)).ThrowsAsync(new InvalidOperationException("db down"));
        var command = new CreateBriefingCommand(
            "Summer campaign", Priority.Medium, Guid.NewGuid(),
            Attachments: [TestUploads.Pdf(), TestUploads.Png()]);

        // Act
        var act = () => _handler.Handle(command, default);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>();
        storedKeys.Should().HaveCount(2);
        _fileStorageMock.Verify(s => s.DeleteAsync(storedKeys[0], It.IsAny<CancellationToken>()), Times.Once);
        _fileStorageMock.Verify(s => s.DeleteAsync(storedKeys[1], It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenCategoryDoesNotExist_DoesNotStoreAttachments()
    {
        // Arrange
        var command = new CreateBriefingCommand(
            "Summer campaign", Priority.Medium, Guid.NewGuid(), Attachments: [TestUploads.Pdf()]);
        _categoryRepositoryMock.Setup(r => r.ExistsActiveAsync(command.CategoryId, default)).ReturnsAsync(false);

        // Act
        await _handler.Handle(command, default);

        // Assert
        _fileStorageMock.VerifyNoOtherCalls();
    }
}
