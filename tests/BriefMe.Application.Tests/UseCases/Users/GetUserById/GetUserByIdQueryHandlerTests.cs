using BriefMe.Application.UseCases.Users.GetUserById;
using BriefMe.Domain.Entities;
using BriefMe.Domain.Repositories;
using BriefMe.Domain.ValueObjects;
using FluentAssertions;
using Moq;

namespace BriefMe.Application.Tests.UseCases.Users.GetUserById;

public class GetUserByIdQueryHandlerTests
{
    private readonly Mock<IUserRepository> _userRepositoryMock = new();
    private readonly Mock<IRoleRepository> _roleRepositoryMock = new();
    private readonly GetUserByIdQueryHandler _handler;

    public GetUserByIdQueryHandlerTests()
    {
        _handler = new GetUserByIdQueryHandler(_userRepositoryMock.Object, _roleRepositoryMock.Object);
    }

    [Fact]
    public async Task Handle_WhenUserExists_ReturnsUserResultWithRoleNames()
    {
        // Arrange
        var role = Role.Create("Designer", null);
        var user = User.Create("Ana Silva", Email.Create("ana@briefme.com").Value, "hashed-password");
        user.AssignRole(role.Id);

        _userRepositoryMock.Setup(r => r.GetByIdWithRolesAsync(user.Id, default)).ReturnsAsync(user);
        _roleRepositoryMock
            .Setup(r => r.GetByIdsAsync(It.Is<IEnumerable<Guid>>(ids => ids.Contains(role.Id)), default))
            .ReturnsAsync([role]);

        var query = new GetUserByIdQuery(user.Id);

        // Act
        var result = await _handler.Handle(query, default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.UserId.Should().Be(user.Id);
        result.Value.FullName.Should().Be("Ana Silva");
        result.Value.Email.Should().Be("ana@briefme.com");
        result.Value.Roles.Should().BeEquivalentTo(["Designer"]);
    }

    [Fact]
    public async Task Handle_WhenUserHasNoRoles_ReturnsUserResultWithoutQueryingRoles()
    {
        // Arrange
        var user = User.Create("Ana Silva", Email.Create("ana@briefme.com").Value, "hashed-password");

        _userRepositoryMock.Setup(r => r.GetByIdWithRolesAsync(user.Id, default)).ReturnsAsync(user);

        var query = new GetUserByIdQuery(user.Id);

        // Act
        var result = await _handler.Handle(query, default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Roles.Should().BeEmpty();
        _roleRepositoryMock.Verify(r => r.GetByIdsAsync(It.IsAny<IEnumerable<Guid>>(), default), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenUserDoesNotExist_ReturnsFailure()
    {
        // Arrange
        var userId = Guid.NewGuid();
        _userRepositoryMock.Setup(r => r.GetByIdWithRolesAsync(userId, default)).ReturnsAsync((User?)null);

        var query = new GetUserByIdQuery(userId);

        // Act
        var result = await _handler.Handle(query, default);

        // Assert
        result.IsFailure.Should().BeTrue();
    }
}
