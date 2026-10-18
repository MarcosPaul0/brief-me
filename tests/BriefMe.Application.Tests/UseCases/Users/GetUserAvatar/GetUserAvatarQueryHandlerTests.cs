using BriefMe.Application.Abstractions;
using BriefMe.Application.UseCases.Users.GetUserAvatar;
using BriefMe.Domain.Common;
using BriefMe.Domain.Entities;
using BriefMe.Domain.Enums;
using BriefMe.Domain.Repositories;
using BriefMe.Domain.ValueObjects;
using FluentAssertions;
using Moq;

namespace BriefMe.Application.Tests.UseCases.Users.GetUserAvatar;

public class GetUserAvatarQueryHandlerTests
{
    private readonly Mock<IUserRepository> _userRepositoryMock = new();
    private readonly Mock<IFileStorage> _fileStorageMock = new();
    private readonly GetUserAvatarQueryHandler _handler;

    public GetUserAvatarQueryHandlerTests()
    {
        _handler = new GetUserAvatarQueryHandler(_userRepositoryMock.Object, _fileStorageMock.Object);
    }

    private User ArrangeUser()
    {
        var user = User.Create("Ana Silva", Email.Create("ana@briefme.com").Value, "hash", UserRole.Requester);
        _userRepositoryMock.Setup(r => r.GetByIdAsync(user.Id, default)).ReturnsAsync(user);
        return user;
    }

    [Fact]
    public async Task Handle_WhenAvatarExists_ReturnsStreamWithStoredContentType()
    {
        // Arrange
        var user = ArrangeUser();
        user.ChangeAvatar("avatars/a.webp", AvatarFormat.WebP);
        var content = new MemoryStream([1, 2, 3]);
        _fileStorageMock.Setup(s => s.OpenReadAsync("avatars/a.webp", default)).ReturnsAsync(content);

        // Act
        var result = await _handler.Handle(new GetUserAvatarQuery(user.Id), default);

        // Assert
        result.Value.Should().BeEquivalentTo(new { Content = content, ContentType = "image/webp" });
    }

    [Fact]
    public async Task Handle_WhenUserHasNoAvatar_ReturnsNotFound()
    {
        // Arrange
        var user = ArrangeUser();

        // Act
        var result = await _handler.Handle(new GetUserAvatarQuery(user.Id), default);

        // Assert
        result.Type.Should().Be(ErrorType.NotFound);
    }

    [Fact]
    public async Task Handle_WhenStoredObjectIsMissing_ReturnsNotFound()
    {
        // Arrange
        var user = ArrangeUser();
        user.ChangeAvatar("avatars/gone.png", AvatarFormat.Png);
        _fileStorageMock.Setup(s => s.OpenReadAsync("avatars/gone.png", default)).ReturnsAsync((Stream?)null);

        // Act
        var result = await _handler.Handle(new GetUserAvatarQuery(user.Id), default);

        // Assert
        result.Type.Should().Be(ErrorType.NotFound);
    }

    [Fact]
    public async Task Handle_WhenUserDoesNotExist_ReturnsNotFound()
    {
        // Arrange
        var unknownId = Guid.NewGuid();

        // Act
        var result = await _handler.Handle(new GetUserAvatarQuery(unknownId), default);

        // Assert
        result.Type.Should().Be(ErrorType.NotFound);
    }
}
