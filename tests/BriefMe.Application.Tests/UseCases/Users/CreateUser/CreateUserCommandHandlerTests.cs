using BriefMe.Application.Abstractions;
using BriefMe.Application.UseCases.Users.CreateUser;
using BriefMe.Domain.Entities;
using BriefMe.Domain.Enums;
using BriefMe.Domain.Repositories;
using BriefMe.Domain.ValueObjects;
using FluentAssertions;
using Moq;

namespace BriefMe.Application.Tests.UseCases.Users.CreateUser;

public class CreateUserCommandHandlerTests
{
    private readonly Mock<IUserRepository> _userRepositoryMock = new();
    private readonly Mock<IPasswordHasher> _passwordHasherMock = new();
    private readonly Mock<IUnitOfWork> _unitOfWorkMock = new();
    private readonly CreateUserCommandHandler _handler;

    public CreateUserCommandHandlerTests()
    {
        _handler = new CreateUserCommandHandler(
            _userRepositoryMock.Object,
            _passwordHasherMock.Object,
            _unitOfWorkMock.Object);

        _userRepositoryMock
            .Setup(r => r.EmailExistsAsync(It.IsAny<Email>(), null, default))
            .ReturnsAsync(false);
        _passwordHasherMock.Setup(h => h.Hash(It.IsAny<string>())).Returns("hashed-password");
    }

    [Theory]
    [InlineData(UserRole.Admin)]
    [InlineData(UserRole.Designer)]
    [InlineData(UserRole.Requester)]
    public async Task Handle_WhenDataIsValid_ReturnsUserWithTheGivenRole(UserRole role)
    {
        // Arrange
        var command = new CreateUserCommand("Ana Silva", "ana@briefme.com", "Senha123", role);

        // Act
        var result = await _handler.Handle(command, default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Role.Should().Be(role);
    }

    [Fact]
    public async Task Handle_WhenDataIsValid_PersistsActiveUser()
    {
        // Arrange
        var command = new CreateUserCommand("Ana Silva", "ana@briefme.com", "Senha123", UserRole.Designer);

        // Act
        await _handler.Handle(command, default);

        // Assert
        _userRepositoryMock.Verify(r => r.AddAsync(
            It.Is<User>(u => u.Status == UserStatus.Active && u.Role == UserRole.Designer), default), Times.Once);
        _unitOfWorkMock.Verify(u => u.CommitAsync(default), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenEmailAlreadyExists_ReturnsFailureWithoutPersisting()
    {
        // Arrange
        var command = new CreateUserCommand("Ana Silva", "ana@briefme.com", "Senha123", UserRole.Requester);

        _userRepositoryMock
            .Setup(r => r.EmailExistsAsync(It.IsAny<Email>(), null, default))
            .ReturnsAsync(true);

        // Act
        var result = await _handler.Handle(command, default);

        // Assert
        result.IsFailure.Should().BeTrue();
        _unitOfWorkMock.Verify(u => u.CommitAsync(default), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenPasswordDoesNotMeetPolicy_ReturnsFailureWithoutCheckingUniqueness()
    {
        // Arrange
        var command = new CreateUserCommand("Ana Silva", "ana@briefme.com", "fraca", UserRole.Requester);

        // Act
        var result = await _handler.Handle(command, default);

        // Assert
        result.IsFailure.Should().BeTrue();
        _userRepositoryMock.Verify(r => r.EmailExistsAsync(It.IsAny<Email>(), It.IsAny<Guid?>(), default), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenEmailIsMalformed_ReturnsFailureWithoutCheckingUniqueness()
    {
        // Arrange
        var command = new CreateUserCommand("Ana Silva", "not-an-email", "Senha123", UserRole.Requester);

        // Act
        var result = await _handler.Handle(command, default);

        // Assert
        result.IsFailure.Should().BeTrue();
        _userRepositoryMock.Verify(r => r.EmailExistsAsync(It.IsAny<Email>(), It.IsAny<Guid?>(), default), Times.Never);
    }
}
