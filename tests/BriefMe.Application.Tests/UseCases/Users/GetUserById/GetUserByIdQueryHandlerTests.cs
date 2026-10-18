using BriefMe.Application.UseCases.Users.GetUserById;
using BriefMe.Domain.Entities;
using BriefMe.Domain.Enums;
using BriefMe.Domain.Repositories;
using BriefMe.Domain.ValueObjects;
using FluentAssertions;
using Moq;

namespace BriefMe.Application.Tests.UseCases.Users.GetUserById;

public class GetUserByIdQueryHandlerTests
{
    private readonly Mock<IUserRepository> _userRepositoryMock = new();
    private readonly GetUserByIdQueryHandler _handler;

    public GetUserByIdQueryHandlerTests()
    {
        _handler = new GetUserByIdQueryHandler(_userRepositoryMock.Object);
    }

    [Fact]
    public async Task Handle_WhenUserExists_ReturnsUserWithRole()
    {
        // Arrange
        var user = User.Create("Ana Silva", Email.Create("ana@briefme.com").Value, "hashed-password", UserRole.Designer);
        _userRepositoryMock.Setup(r => r.GetByIdAsync(user.Id, default)).ReturnsAsync(user);

        // Act
        var result = await _handler.Handle(new GetUserByIdQuery(user.Id), default);

        // Assert
        result.Value.Should().BeEquivalentTo(new { user.FullName, Email = "ana@briefme.com", Role = UserRole.Designer });
    }

    [Fact]
    public async Task Handle_WhenUserDoesNotExist_ReturnsFailure()
    {
        // Arrange
        var userId = Guid.NewGuid();
        _userRepositoryMock.Setup(r => r.GetByIdAsync(userId, default)).ReturnsAsync((User?)null);

        // Act
        var result = await _handler.Handle(new GetUserByIdQuery(userId), default);

        // Assert
        result.IsFailure.Should().BeTrue();
    }
}
