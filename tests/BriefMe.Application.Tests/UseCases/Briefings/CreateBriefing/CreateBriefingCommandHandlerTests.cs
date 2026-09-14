using BriefMe.Application.Abstractions;
using BriefMe.Application.UseCases.Briefings.CreateBriefing;
using BriefMe.Domain.Common;
using BriefMe.Domain.Entities;
using BriefMe.Domain.Enums;
using BriefMe.Domain.Repositories;
using FluentAssertions;
using Moq;

namespace BriefMe.Application.Tests.UseCases.Briefings.CreateBriefing;

public class CreateBriefingCommandHandlerTests
{
    private readonly Mock<IBriefingRepository> _briefingRepositoryMock = new();
    private readonly Mock<ICategoryRepository> _categoryRepositoryMock = new();
    private readonly Mock<ICurrentUser> _currentUserMock = new();
    private readonly Mock<IUnitOfWork> _unitOfWorkMock = new();
    private readonly Guid _requesterId = Guid.NewGuid();
    private readonly CreateBriefingCommandHandler _handler;

    public CreateBriefingCommandHandlerTests()
    {
        _handler = new CreateBriefingCommandHandler(
            _briefingRepositoryMock.Object,
            _categoryRepositoryMock.Object,
            _currentUserMock.Object,
            _unitOfWorkMock.Object);

        _currentUserMock.Setup(u => u.UserId).Returns(_requesterId.ToString());
        _categoryRepositoryMock
            .Setup(r => r.ExistsActiveAsync(It.IsAny<Guid>(), default))
            .ReturnsAsync(true);
    }

    [Fact]
    public async Task Handle_WhenCommandIsValid_PersistsDraftAndReturnsDto()
    {
        // Arrange
        var categoryId = Guid.NewGuid();
        var command = new CreateBriefingCommand(
            "Summer campaign",
            Priority.High,
            categoryId,
            Description: "Social media assets",
            Objective: "Increase engagement");

        // Act
        var result = await _handler.Handle(command, default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Status.Should().Be(BriefingStatus.Draft);
        result.Value.Title.Should().Be("Summer campaign");
        result.Value.CategoryId.Should().Be(categoryId);
        result.Value.RequesterId.Should().Be(_requesterId);
    }

    [Fact]
    public async Task Handle_WhenCommandIsValid_AddsBriefingAndCommitsOnce()
    {
        // Arrange
        var command = new CreateBriefingCommand("Summer campaign", Priority.Medium, Guid.NewGuid());

        // Act
        await _handler.Handle(command, default);

        // Assert
        _briefingRepositoryMock.Verify(r => r.AddAsync(It.IsAny<Briefing>(), default), Times.Once);
        _unitOfWorkMock.Verify(u => u.CommitAsync(default), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenCategoryDoesNotExist_ReturnsNotFoundWithoutPersisting()
    {
        // Arrange
        var command = new CreateBriefingCommand("Summer campaign", Priority.Medium, Guid.NewGuid());

        _categoryRepositoryMock
            .Setup(r => r.ExistsActiveAsync(command.CategoryId, default))
            .ReturnsAsync(false);

        // Act
        var result = await _handler.Handle(command, default);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Type.Should().Be(ErrorType.NotFound);
        _briefingRepositoryMock.Verify(r => r.AddAsync(It.IsAny<Briefing>(), default), Times.Never);
        _unitOfWorkMock.Verify(u => u.CommitAsync(default), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenCurrentUserIsNotAuthenticated_ReturnsUnauthorizedWithoutTouchingCategory()
    {
        // Arrange
        _currentUserMock.Setup(u => u.UserId).Returns((string?)null);
        var command = new CreateBriefingCommand("Summer campaign", Priority.Medium, Guid.NewGuid());

        // Act
        var result = await _handler.Handle(command, default);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Type.Should().Be(ErrorType.Unauthorized);
        _categoryRepositoryMock.Verify(r => r.ExistsActiveAsync(It.IsAny<Guid>(), default), Times.Never);
        _briefingRepositoryMock.Verify(r => r.AddAsync(It.IsAny<Briefing>(), default), Times.Never);
    }
}
