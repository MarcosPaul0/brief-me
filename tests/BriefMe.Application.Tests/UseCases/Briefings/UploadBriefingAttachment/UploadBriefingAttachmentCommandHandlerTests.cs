using System.Text;
using BriefMe.Application.Abstractions;
using BriefMe.Application.Common.Errors;
using BriefMe.Application.UseCases.Briefings.UploadBriefingAttachment;
using BriefMe.Domain.Common;
using BriefMe.Domain.Entities;
using BriefMe.Domain.Enums;
using BriefMe.Domain.Repositories;
using FluentAssertions;
using Moq;

namespace BriefMe.Application.Tests.UseCases.Briefings.UploadBriefingAttachment;

public class UploadBriefingAttachmentCommandHandlerTests
{
    private const string StorageKey = "2026/07/31/0f3a1c.pdf";

    private readonly Mock<IBriefingRepository> _briefingRepositoryMock = new();
    private readonly Mock<IFileValidator> _fileValidatorMock = new();
    private readonly Mock<IFileStorageService> _fileStorageMock = new();
    private readonly Mock<ICurrentUser> _currentUserMock = new();
    private readonly Mock<IUnitOfWork> _unitOfWorkMock = new();

    private readonly Guid _requesterId = Guid.NewGuid();
    private readonly UploadBriefingAttachmentCommandHandler _handler;

    public UploadBriefingAttachmentCommandHandlerTests()
    {
        _handler = new UploadBriefingAttachmentCommandHandler(
            _briefingRepositoryMock.Object,
            _fileValidatorMock.Object,
            _fileStorageMock.Object,
            _currentUserMock.Object,
            _unitOfWorkMock.Object);

        _currentUserMock.Setup(u => u.UserId).Returns(_requesterId.ToString());

        _fileValidatorMock
            .Setup(v => v.Validate(It.IsAny<string>(), It.IsAny<long>()))
            .Returns(Result.Success());

        _fileStorageMock
            .Setup(s => s.SaveAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(StorageKey);
    }

    private static UploadBriefingAttachmentCommand Command(Guid briefingId, string fileName = "referencia.pdf") =>
        new(briefingId, fileName, 2048, new MemoryStream(Encoding.UTF8.GetBytes("bytes")));

    private Briefing ArrangeBriefing(BriefingStatus status = BriefingStatus.Draft, Guid? requesterId = null)
    {
        var briefing = Briefing.Create(
            "Campanha de fim de ano",
            Priority.Medium,
            Guid.NewGuid(),
            requesterId ?? _requesterId);

        ForceStatus(briefing, status);

        _briefingRepositoryMock
            .Setup(r => r.GetByIdAsync(briefing.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(briefing);

        return briefing;
    }

    private static void ForceStatus(Briefing briefing, BriefingStatus status)
    {
        typeof(Briefing)
            .GetProperty(nameof(Briefing.Status))!
            .SetValue(briefing, status);
    }

    [Fact]
    public async Task Handle_WhenBriefingIsADraftOwnedByCaller_StoresTheFileAndReturnsTheAttachment()
    {
        // Arrange
        var briefing = ArrangeBriefing();

        // Act
        var result = await _handler.Handle(Command(briefing.Id), default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.FileName.Should().Be("referencia.pdf");
        result.Value.UploadedById.Should().Be(_requesterId);
        result.Value.TaskId.Should().BeNull();
    }

    [Fact]
    public async Task Handle_WhenUploadSucceeds_CommitsOnce()
    {
        // Arrange
        var briefing = ArrangeBriefing();

        // Act
        await _handler.Handle(Command(briefing.Id), default);

        // Assert
        _unitOfWorkMock.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData(BriefingStatus.Draft)]
    [InlineData(BriefingStatus.AwaitingApproval)]
    [InlineData(BriefingStatus.ChangesRequested)]
    public async Task Handle_WhenBriefingIsStillComposable_Succeeds(BriefingStatus status)
    {
        // Arrange
        var briefing = ArrangeBriefing(status);

        // Act
        var result = await _handler.Handle(Command(briefing.Id), default);

        // Assert
        result.IsSuccess.Should().BeTrue();
    }

    [Theory]
    [InlineData(BriefingStatus.Approved)]
    [InlineData(BriefingStatus.Rejected)]
    [InlineData(BriefingStatus.InProgress)]
    [InlineData(BriefingStatus.Completed)]
    [InlineData(BriefingStatus.Cancelled)]
    public async Task Handle_WhenBriefingHasMovedOn_ReturnsConflictWithoutStoring(BriefingStatus status)
    {
        // Arrange
        var briefing = ArrangeBriefing(status);

        // Act
        var result = await _handler.Handle(Command(briefing.Id), default);

        // Assert
        result.Type.Should().Be(ErrorType.Conflict);
        _fileStorageMock.Verify(
            s => s.SaveAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_WhenFileValidationFails_ReturnsFailureWithoutStoring()
    {
        // Arrange
        var briefing = ArrangeBriefing();
        _fileValidatorMock
            .Setup(v => v.Validate(It.IsAny<string>(), It.IsAny<long>()))
            .Returns(Result.Failure([new ValidationError("File", "File is too large.")]));

        // Act
        var result = await _handler.Handle(Command(briefing.Id), default);

        // Assert
        result.IsFailure.Should().BeTrue();
        _fileStorageMock.Verify(
            s => s.SaveAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_WhenFileValidationFails_DoesNotLoadTheBriefing()
    {
        // Arrange
        _fileValidatorMock
            .Setup(v => v.Validate(It.IsAny<string>(), It.IsAny<long>()))
            .Returns(Result.Failure([new ValidationError("File", "File is too large.")]));

        // Act
        await _handler.Handle(Command(Guid.NewGuid()), default);

        // Assert
        _briefingRepositoryMock.Verify(
            r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_WhenContentIsNull_ReturnsValidationFailure()
    {
        // Arrange
        var briefing = ArrangeBriefing();

        // Act
        var result = await _handler.Handle(
            new UploadBriefingAttachmentCommand(briefing.Id, "referencia.pdf", 2048, Content: null),
            default);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Errors.Should().ContainSingle(e => e.ErrorMessage == BriefingErrors.Validation.FileContentRequired);
    }

    [Fact]
    public async Task Handle_WhenCallerIsNotTheOwner_ReturnsForbiddenWithoutStoring()
    {
        // Arrange
        var briefing = ArrangeBriefing(requesterId: Guid.NewGuid());

        // Act
        var result = await _handler.Handle(Command(briefing.Id), default);

        // Assert
        result.Type.Should().Be(ErrorType.Forbidden);
        _fileStorageMock.Verify(
            s => s.SaveAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_WhenBriefingNotFound_ReturnsNotFound()
    {
        // Arrange
        _briefingRepositoryMock
            .Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Briefing?)null);

        // Act
        var result = await _handler.Handle(Command(Guid.NewGuid()), default);

        // Assert
        result.Type.Should().Be(ErrorType.NotFound);
    }

    [Fact]
    public async Task Handle_WhenCurrentUserIsNotAuthenticated_ReturnsUnauthorizedWithoutValidating()
    {
        // Arrange
        _currentUserMock.Setup(u => u.UserId).Returns((string?)null);

        // Act
        var result = await _handler.Handle(Command(Guid.NewGuid()), default);

        // Assert
        result.Type.Should().Be(ErrorType.Unauthorized);
        _fileValidatorMock.Verify(v => v.Validate(It.IsAny<string>(), It.IsAny<long>()), Times.Never);
    }
}
