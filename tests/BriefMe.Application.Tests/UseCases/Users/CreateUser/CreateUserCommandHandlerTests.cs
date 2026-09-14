using BriefMe.Application.Abstractions;
using BriefMe.Application.UseCases.Users.CreateUser;
using BriefMe.Domain.Entities;
using BriefMe.Domain.Repositories;
using BriefMe.Domain.ValueObjects;
using FluentAssertions;
using Moq;

namespace BriefMe.Application.Tests.UseCases.Users.CreateUser;

public class CreateUserCommandHandlerTests
{
    private readonly Mock<IUserRepository> _userRepositoryMock = new();
    private readonly Mock<IRoleRepository> _roleRepositoryMock = new();
    private readonly Mock<IPasswordHasher> _passwordHasherMock = new();
    private readonly Mock<IUnitOfWork> _unitOfWorkMock = new();
    private readonly CreateUserCommandHandler _handler;

    public CreateUserCommandHandlerTests()
    {
        _handler = new CreateUserCommandHandler(
            _userRepositoryMock.Object,
            _roleRepositoryMock.Object,
            _passwordHasherMock.Object,
            _unitOfWorkMock.Object);

        _userRepositoryMock
            .Setup(r => r.EmailExistsAsync(It.IsAny<Email>(), null, default))
            .ReturnsAsync(false);
        _passwordHasherMock.Setup(h => h.Hash(It.IsAny<string>())).Returns("hashed-password");
    }

    [Fact]
    public async Task Handle_WhenDataIsValidAndRolesExist_CreatesActiveUserWithAssignedRoles()
    {
        // Arrange
        var role = Role.Create("Designer", null);
        var command = new CreateUserCommand("Ana Silva", "ana@briefme.com", "Senha123", [role.Id]);

        _roleRepositoryMock.Setup(r => r.GetByIdsAsync(It.Is<IEnumerable<Guid>>(ids => ids.Contains(role.Id)), default))
            .ReturnsAsync([role]);

        // Act
        var result = await _handler.Handle(command, default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.FullName.Should().Be("Ana Silva");
        result.Value.Email.Should().Be("ana@briefme.com");
        result.Value.Roles.Should().ContainSingle().Which.Should().Be("Designer");
        _userRepositoryMock.Verify(r => r.AddAsync(It.IsAny<User>(), default), Times.Once);
        _unitOfWorkMock.Verify(u => u.CommitAsync(default), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenRoleIdsIsEmpty_CreatesUserWithoutRoles()
    {
        // Arrange
        var command = new CreateUserCommand("Ana Silva", "ana@briefme.com", "Senha123", []);

        // Act
        var result = await _handler.Handle(command, default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Roles.Should().BeEmpty();
        _roleRepositoryMock.Verify(r => r.GetByIdsAsync(It.IsAny<IEnumerable<Guid>>(), default), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenEmailAlreadyExists_ReturnsFailureWithoutPersisting()
    {
        // Arrange
        var command = new CreateUserCommand("Ana Silva", "ana@briefme.com", "Senha123", []);

        _userRepositoryMock.Setup(r => r.EmailExistsAsync(It.IsAny<Email>(), null, default)).ReturnsAsync(true);

        // Act
        var result = await _handler.Handle(command, default);

        // Assert
        result.IsFailure.Should().BeTrue();
        _userRepositoryMock.Verify(r => r.AddAsync(It.IsAny<User>(), default), Times.Never);
        _unitOfWorkMock.Verify(u => u.CommitAsync(default), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenPasswordDoesNotMeetPolicy_ReturnsFailureWithoutCheckingUniqueness()
    {
        // Arrange
        var command = new CreateUserCommand("Ana Silva", "ana@briefme.com", "weak", []);

        // Act
        var result = await _handler.Handle(command, default);

        // Assert
        result.IsFailure.Should().BeTrue();
        _userRepositoryMock.Verify(r => r.EmailExistsAsync(It.IsAny<Email>(), null, default), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenEmailIsMalformed_ReturnsFailureWithoutCheckingUniqueness()
    {
        // Arrange
        var command = new CreateUserCommand("Ana Silva", "not-an-email", "Senha123", []);

        // Act
        var result = await _handler.Handle(command, default);

        // Assert
        result.IsFailure.Should().BeTrue();
        _userRepositoryMock.Verify(r => r.EmailExistsAsync(It.IsAny<Email>(), null, default), Times.Never);
        _userRepositoryMock.Verify(r => r.AddAsync(It.IsAny<User>(), default), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenSomeRoleIdsDoNotExist_ReturnsFailureWithoutPersisting()
    {
        // Arrange
        var existingRole = Role.Create("Designer", null);
        var missingRoleId = Guid.NewGuid();
        var command = new CreateUserCommand("Ana Silva", "ana@briefme.com", "Senha123", [existingRole.Id, missingRoleId]);

        _roleRepositoryMock.Setup(r => r.GetByIdsAsync(It.IsAny<IEnumerable<Guid>>(), default))
            .ReturnsAsync([existingRole]);

        // Act
        var result = await _handler.Handle(command, default);

        // Assert
        result.IsFailure.Should().BeTrue();
        _userRepositoryMock.Verify(r => r.AddAsync(It.IsAny<User>(), default), Times.Never);
        _unitOfWorkMock.Verify(u => u.CommitAsync(default), Times.Never);
    }
}
