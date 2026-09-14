using BriefMe.Application.UseCases.Roles.CreateRole;
using BriefMe.Domain.Entities;
using BriefMe.Domain.Repositories;
using FluentAssertions;
using Moq;

namespace BriefMe.Application.Tests.UseCases.Roles.CreateRole;

public class CreateRoleCommandHandlerTests
{
    private readonly Mock<IRoleRepository> _roleRepositoryMock = new();
    private readonly Mock<IUnitOfWork> _unitOfWorkMock = new();
    private readonly CreateRoleCommandHandler _handler;

    public CreateRoleCommandHandlerTests()
    {
        _handler = new CreateRoleCommandHandler(_roleRepositoryMock.Object, _unitOfWorkMock.Object);

        _roleRepositoryMock
            .Setup(r => r.NameExistsAsync(It.IsAny<string>(), null, default))
            .ReturnsAsync(false);
    }

    [Fact]
    public async Task Handle_WhenNameIsUnique_CreatesRoleWithoutPermissions()
    {
        // Arrange
        var command = new CreateRoleCommand("Designer", "Perfil de designer");

        // Act
        var result = await _handler.Handle(command, default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Name.Should().Be("Designer");
        result.Value.PermissionCodes.Should().BeEmpty();
        _roleRepositoryMock.Verify(r => r.AddAsync(It.IsAny<Role>(), default), Times.Once);
        _unitOfWorkMock.Verify(u => u.CommitAsync(default), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenNameAlreadyExists_ReturnsFailureWithoutPersisting()
    {
        // Arrange
        var command = new CreateRoleCommand("Designer", null);

        _roleRepositoryMock.Setup(r => r.NameExistsAsync("Designer", null, default)).ReturnsAsync(true);

        // Act
        var result = await _handler.Handle(command, default);

        // Assert
        result.IsFailure.Should().BeTrue();
        _roleRepositoryMock.Verify(r => r.AddAsync(It.IsAny<Role>(), default), Times.Never);
        _unitOfWorkMock.Verify(u => u.CommitAsync(default), Times.Never);
    }
}
