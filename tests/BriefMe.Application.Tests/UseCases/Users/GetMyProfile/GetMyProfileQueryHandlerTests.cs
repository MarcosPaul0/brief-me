using BriefMe.Application.Abstractions;
using BriefMe.Application.UseCases.Users.GetMyProfile;
using BriefMe.Domain.Common;
using BriefMe.Domain.Entities;
using BriefMe.Domain.Enums;
using BriefMe.Domain.Repositories;
using BriefMe.Domain.ValueObjects;
using FluentAssertions;
using Moq;

namespace BriefMe.Application.Tests.UseCases.Users.GetMyProfile;

public class GetMyProfileQueryHandlerTests
{
    private readonly Mock<IUserRepository> _userRepositoryMock = new();
    private readonly Mock<ICurrentUser> _currentUserMock = new();
    private readonly GetMyProfileQueryHandler _handler;

    public GetMyProfileQueryHandlerTests()
    {
        _handler = new GetMyProfileQueryHandler(_userRepositoryMock.Object, _currentUserMock.Object);
    }

    [Fact]
    public async Task Handle_WhenSignedInUserExists_ReturnsTheirProfile()
    {
        // Arrange
        var user = User.Create("Ana Silva", Email.Create("ana@briefme.com").Value, "hash", UserRole.Requester);
        _currentUserMock.Setup(c => c.UserId).Returns(user.Id.ToString());
        _userRepositoryMock.Setup(r => r.GetByIdAsync(user.Id, default)).ReturnsAsync(user);

        // Act
        var result = await _handler.Handle(new GetMyProfileQuery(), default);

        // Assert
        result.Value.UserId.Should().Be(user.Id);
    }

    [Fact]
    public async Task Handle_WhenSignedInUserNoLongerExists_ReturnsNotFound()
    {
        // Arrange
        _currentUserMock.Setup(c => c.UserId).Returns(Guid.NewGuid().ToString());

        // Act
        var result = await _handler.Handle(new GetMyProfileQuery(), default);

        // Assert
        result.Type.Should().Be(ErrorType.NotFound);
    }

    [Fact]
    public async Task Handle_WhenCallerIsNotAuthenticated_ReturnsUnauthorized()
    {
        // Arrange
        _currentUserMock.Setup(c => c.UserId).Returns((string?)null);

        // Act
        var result = await _handler.Handle(new GetMyProfileQuery(), default);

        // Assert
        result.Type.Should().Be(ErrorType.Unauthorized);
    }
}
