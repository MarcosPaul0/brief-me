using BriefMe.Application.Common.Errors;
using BriefMe.Application.UseCases.Sla.ConfigureSlaRule;
using BriefMe.Domain.Common;
using BriefMe.Domain.Entities;
using BriefMe.Domain.Enums;
using BriefMe.Domain.Repositories;
using FluentAssertions;
using Moq;

namespace BriefMe.Application.Tests.UseCases.Sla.ConfigureSlaRule;

public class ConfigureSlaRuleCommandHandlerTests
{
    private static readonly Guid VideoCategoryId = Guid.NewGuid();

    private readonly Mock<ISlaRuleRepository> _slaRuleRepositoryMock = new();
    private readonly Mock<ICategoryRepository> _categoryRepositoryMock = new();
    private readonly Mock<IUnitOfWork> _unitOfWorkMock = new();
    private readonly ConfigureSlaRuleCommandHandler _handler;

    public ConfigureSlaRuleCommandHandlerTests()
    {
        _handler = new ConfigureSlaRuleCommandHandler(
            _slaRuleRepositoryMock.Object,
            _categoryRepositoryMock.Object,
            _unitOfWorkMock.Object);

        _categoryRepositoryMock
            .Setup(r => r.ExistsActiveAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // No rule anywhere unless a test arranges one — the default is a system with an empty policy table.
        _slaRuleRepositoryMock
            .Setup(r => r.GetByScopeAsync(It.IsAny<Guid?>(), It.IsAny<Priority?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((SlaRule?)null);
    }

    /// <summary>Registers an existing rule as the one covering its own scope.</summary>
    private SlaRule ArrangeExistingRule(Guid? categoryId, Priority? priority, int deadlineHours)
    {
        var rule = SlaRule.Create(categoryId, priority, deadlineHours);

        _slaRuleRepositoryMock
            .Setup(r => r.GetByScopeAsync(categoryId, priority, It.IsAny<CancellationToken>()))
            .ReturnsAsync(rule);

        return rule;
    }

    [Fact]
    public async Task Handle_WhenScopeHasNoRule_CreatesTheRuleWithTheConfiguredDeadline()
    {
        // Arrange
        var command = new ConfigureSlaRuleCommand(VideoCategoryId, Priority.Urgent, 24);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.CategoryId.Should().Be(VideoCategoryId);
        result.Value.Priority.Should().Be(Priority.Urgent);
        result.Value.DeadlineHours.Should().Be(24);
        result.Value.Scope.Should().Be(SlaRuleScope.CategoryAndPriority);
    }

    [Fact]
    public async Task Handle_WhenScopeHasNoRule_PersistsTheNewRuleOnce()
    {
        // Arrange
        var command = new ConfigureSlaRuleCommand(VideoCategoryId, Priority.Urgent, 24);

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        _slaRuleRepositoryMock.Verify(
            r => r.AddAsync(It.IsAny<SlaRule>(), It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWorkMock.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenBothScopeSidesAreAbsent_CreatesTheGlobalRule()
    {
        // Arrange
        var command = new ConfigureSlaRuleCommand(null, null, 240);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Value.Scope.Should().Be(SlaRuleScope.Global);
        result.Value.CategoryId.Should().BeNull();
        result.Value.Priority.Should().BeNull();
    }

    [Fact]
    public async Task Handle_WhenCategoryIdIsEmpty_TreatsTheScopeAsHavingNoCategory()
    {
        // Arrange
        var command = new ConfigureSlaRuleCommand(Guid.Empty, Priority.Urgent, 48);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Value.CategoryId.Should().BeNull();
        result.Value.Scope.Should().Be(SlaRuleScope.Priority);
        _slaRuleRepositoryMock.Verify(
            r => r.GetByScopeAsync(null, Priority.Urgent, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenScopeAlreadyHasARule_ChangesThatRuleDeadline()
    {
        // Arrange
        var existing = ArrangeExistingRule(VideoCategoryId, Priority.Urgent, 24);
        var command = new ConfigureSlaRuleCommand(VideoCategoryId, Priority.Urgent, 12);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Id.Should().Be(existing.Id);
        result.Value.DeadlineHours.Should().Be(12);
        existing.Deadline.Hours.Should().Be(12);
    }

    [Fact]
    public async Task Handle_WhenScopeAlreadyHasARule_DoesNotAddASecondRule()
    {
        // Arrange
        ArrangeExistingRule(null, null, 240);
        var command = new ConfigureSlaRuleCommand(null, null, 120);

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        _slaRuleRepositoryMock.Verify(
            r => r.AddAsync(It.IsAny<SlaRule>(), It.IsAny<CancellationToken>()), Times.Never);
        _unitOfWorkMock.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenTheSameCommandRunsTwice_LeavesTheSameDeadline()
    {
        // Arrange
        var existing = ArrangeExistingRule(VideoCategoryId, null, 120);
        var command = new ConfigureSlaRuleCommand(VideoCategoryId, null, 72);

        // Act
        await _handler.Handle(command, CancellationToken.None);
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        existing.Deadline.Hours.Should().Be(72);
    }

    [Fact]
    public async Task Handle_WhenCategoryDoesNotExistOrIsInactive_ReturnsNotFound()
    {
        // Arrange
        _categoryRepositoryMock
            .Setup(r => r.ExistsActiveAsync(VideoCategoryId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var command = new ConfigureSlaRuleCommand(VideoCategoryId, Priority.Urgent, 24);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Type.Should().Be(ErrorType.NotFound);
        result.Error.Should().Be(CategoryErrors.NotFoundOrInactive);
    }

    [Fact]
    public async Task Handle_WhenCategoryDoesNotExistOrIsInactive_DoesNotCommit()
    {
        // Arrange
        _categoryRepositoryMock
            .Setup(r => r.ExistsActiveAsync(VideoCategoryId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var command = new ConfigureSlaRuleCommand(VideoCategoryId, Priority.Urgent, 24);

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        _unitOfWorkMock.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenScopeHasNoCategory_DoesNotLookUpCategories()
    {
        // Arrange
        var command = new ConfigureSlaRuleCommand(null, Priority.Low, 480);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        _categoryRepositoryMock.Verify(
            r => r.ExistsActiveAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(int.MaxValue)]
    public async Task Handle_WhenDeadlineIsOutsideTheAcceptedRange_ReturnsFailureWithoutTouchingRepositories(int deadlineHours)
    {
        // Arrange
        var command = new ConfigureSlaRuleCommand(VideoCategoryId, Priority.Urgent, deadlineHours);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        _slaRuleRepositoryMock.Verify(
            r => r.GetByScopeAsync(It.IsAny<Guid?>(), It.IsAny<Priority?>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _unitOfWorkMock.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }
}
