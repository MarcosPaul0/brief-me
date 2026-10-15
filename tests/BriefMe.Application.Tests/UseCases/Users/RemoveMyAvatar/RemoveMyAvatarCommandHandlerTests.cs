using BriefMe.Application.Abstractions;
using BriefMe.Application.UseCases.Users.RemoveMyAvatar;
using BriefMe.Domain.Common;
using BriefMe.Domain.Entities;
using BriefMe.Domain.Enums;
using BriefMe.Domain.Repositories;
using BriefMe.Domain.ValueObjects;
using FluentAssertions;
using Moq;

namespace BriefMe.Application.Tests.UseCases.Users.RemoveMyAvatar;

public class RemoveMyAvatarCommandHandlerTests
{
    private readonly Mock<IUserRepository> _userRepositoryMock = new();
    private readonly Mock<IUnitOfWork> _unitOfWorkMock = new();
    private readonly Mock<ICurrentUser> _currentUserMock = new();
    private readonly Mock<IFileStorage> _fileStorageMock = new();
    private readonly RemoveMyAvatarCommandHandler _handler;

    public RemoveMyAvatarCommandHandlerTests()
    {
        _handler = new RemoveMyAvatarCommandHandler(
            _userRepositoryMock.Object,
            _unitOfWorkMock.Object,
            _currentUserMock.Object,
            _fileStorageMock.Object);
    }

    private User ArrangeSignedInUser()
    {
        var user = User.Create("Ana Silva", Email.Create("ana@briefme.com").Value, "hash", UserRole.Designer);
        _currentUserMock.Setup(c => c.UserId).Returns(user.Id.ToString());
        _userRepositoryMock.Setup(r => r.GetByIdAsync(user.Id, default)).ReturnsAsync(user);
        return user;
    }

    [Fact]
    public async Task Handle_WhenUserHasAvatar_ReturnsProfileWithoutAvatarVersion()
    {
        // Arrange
        var user = ArrangeSignedInUser();
        user.ChangeAvatar("avatars/current.png", AvatarFormat.Png);

        // Act
        var result = await _handler.Handle(new RemoveMyAvatarCommand(), default);

        // Assert
        result.Value.AvatarVersion.Should().BeNull();
    }

    [Fact]
    public async Task Handle_WhenUserHasAvatar_CommitsAndDeletesTheObject()
    {
        // Arrange
        var user = ArrangeSignedInUser();
        user.ChangeAvatar("avatars/current.png", AvatarFormat.Png);

        // Act
        await _handler.Handle(new RemoveMyAvatarCommand(), default);

        // Assert
        _unitOfWorkMock.Verify(u => u.CommitAsync(default), Times.Once);
        _fileStorageMock.Verify(s => s.DeleteAsync("avatars/current.png", default), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenUserHasNoAvatar_SucceedsWithoutTouchingStorage()
    {
        // Arrange
        ArrangeSignedInUser();

        // Act
        var result = await _handler.Handle(new RemoveMyAvatarCommand(), default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        _fileStorageMock.Verify(s => s.DeleteAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenCallerIsNotAuthenticated_ReturnsUnauthorized()
    {
        // Arrange
        _currentUserMock.Setup(c => c.UserId).Returns((string?)null);

        // Act
        var result = await _handler.Handle(new RemoveMyAvatarCommand(), default);

        // Assert
        result.Type.Should().Be(ErrorType.Unauthorized);
    }
}
