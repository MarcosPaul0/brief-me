using BriefMe.Application.Abstractions;
using BriefMe.Application.Common.Errors;
using BriefMe.Application.UseCases.Users.UploadMyAvatar;
using BriefMe.Domain.Common;
using BriefMe.Domain.Entities;
using BriefMe.Domain.Enums;
using BriefMe.Domain.Repositories;
using BriefMe.Domain.ValueObjects;
using FluentAssertions;
using Moq;

namespace BriefMe.Application.Tests.UseCases.Users.UploadMyAvatar;

public class UploadMyAvatarCommandHandlerTests
{
    private static readonly byte[] PngBytes = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x01, 0x02, 0x03];
    private static readonly byte[] GifBytes = [0x47, 0x49, 0x46, 0x38, 0x39, 0x61, 0x01, 0x02];

    private readonly Mock<IUserRepository> _userRepositoryMock = new();
    private readonly Mock<IUnitOfWork> _unitOfWorkMock = new();
    private readonly Mock<ICurrentUser> _currentUserMock = new();
    private readonly Mock<IFileStorage> _fileStorageMock = new();
    private readonly UploadMyAvatarCommandHandler _handler;

    public UploadMyAvatarCommandHandlerTests()
    {
        _handler = new UploadMyAvatarCommandHandler(
            _userRepositoryMock.Object,
            _unitOfWorkMock.Object,
            _currentUserMock.Object,
            _fileStorageMock.Object);
    }

    private User ArrangeSignedInUser()
    {
        var user = User.Create("Ana Silva", Email.Create("ana@briefme.com").Value, "hash", UserRole.Requester);
        _currentUserMock.Setup(c => c.UserId).Returns(user.Id.ToString());
        _userRepositoryMock.Setup(r => r.GetByIdAsync(user.Id, default)).ReturnsAsync(user);
        return user;
    }

    private static UploadMyAvatarCommand CommandFor(byte[] bytes) => new(new MemoryStream(bytes), bytes.Length);

    [Fact]
    public async Task Handle_WhenImageIsPng_ReturnsProfileWithAvatarVersion()
    {
        // Arrange
        ArrangeSignedInUser();

        // Act
        var result = await _handler.Handle(CommandFor(PngBytes), default);

        // Assert
        result.Value.AvatarVersion.Should().NotBeNull();
    }

    [Fact]
    public async Task Handle_WhenImageIsPng_SavesObjectUnderUserPrefixWithDetectedContentType()
    {
        // Arrange
        var user = ArrangeSignedInUser();

        // Act
        await _handler.Handle(CommandFor(PngBytes), default);

        // Assert
        _fileStorageMock.Verify(s => s.SaveAsync(
            It.Is<string>(key => key.StartsWith($"avatars/{user.Id:N}/") && key.EndsWith(".png")),
            It.IsAny<Stream>(),
            "image/png",
            default), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenImageIsPng_CommitsTheChange()
    {
        // Arrange
        ArrangeSignedInUser();

        // Act
        await _handler.Handle(CommandFor(PngBytes), default);

        // Assert
        _unitOfWorkMock.Verify(u => u.CommitAsync(default), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenUserAlreadyHasAvatar_DeletesThePreviousObject()
    {
        // Arrange
        var user = ArrangeSignedInUser();
        user.ChangeAvatar("avatars/old.png", AvatarFormat.Png);

        // Act
        await _handler.Handle(CommandFor(PngBytes), default);

        // Assert
        _fileStorageMock.Verify(s => s.DeleteAsync("avatars/old.png", default), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenUserHasNoPreviousAvatar_DeletesNothing()
    {
        // Arrange
        ArrangeSignedInUser();

        // Act
        await _handler.Handle(CommandFor(PngBytes), default);

        // Assert
        _fileStorageMock.Verify(s => s.DeleteAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenBytesAreNotASupportedImage_ReturnsFormatFailureWithoutSaving()
    {
        // Arrange
        ArrangeSignedInUser();

        // Act
        var result = await _handler.Handle(CommandFor(GifBytes), default);

        // Assert
        result.Error.Should().Be(AvatarFormat.InvalidFormatMessage);
        _fileStorageMock.Verify(s => s.SaveAsync(It.IsAny<string>(), It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenStreamIsLargerThanDeclared_ReturnsTooLargeWithoutSaving()
    {
        // Arrange
        ArrangeSignedInUser();
        var oversized = new byte[AvatarFormat.MaxSizeBytes + 1];
        PngBytes.CopyTo(oversized, 0);
        var command = new UploadMyAvatarCommand(new MemoryStream(oversized), PngBytes.Length);

        // Act
        var result = await _handler.Handle(command, default);

        // Assert
        result.Error.Should().Be(UserErrors.Validation.AvatarTooLarge(AvatarFormat.MaxSizeBytes));
        _fileStorageMock.Verify(s => s.SaveAsync(It.IsAny<string>(), It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenCallerIsNotAuthenticated_ReturnsUnauthorized()
    {
        // Arrange
        _currentUserMock.Setup(c => c.UserId).Returns((string?)null);

        // Act
        var result = await _handler.Handle(CommandFor(PngBytes), default);

        // Assert
        result.Type.Should().Be(ErrorType.Unauthorized);
    }

    [Fact]
    public async Task Handle_WhenSignedInUserNoLongerExists_ReturnsNotFound()
    {
        // Arrange
        _currentUserMock.Setup(c => c.UserId).Returns(Guid.NewGuid().ToString());

        // Act
        var result = await _handler.Handle(CommandFor(PngBytes), default);

        // Assert
        result.Type.Should().Be(ErrorType.NotFound);
    }
}
