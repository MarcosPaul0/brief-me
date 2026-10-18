using BriefMe.Application.Abstractions;
using BriefMe.Application.Common.Briefings;
using BriefMe.Application.UseCases.Briefings.GetBriefingsByDesigner;
using BriefMe.Domain.Common;
using BriefMe.Domain.Enums;
using BriefMe.Domain.Repositories;
using FluentAssertions;
using Moq;

namespace BriefMe.Application.Tests.UseCases.Briefings.GetBriefingsByDesigner;

public class GetBriefingsByDesignerQueryHandlerTests
{
    private readonly Mock<IBriefingRepository> _briefingRepositoryMock = new();
    private readonly Mock<ICategoryRepository> _categoryRepositoryMock = new();
    private readonly Mock<IUserRepository> _userRepositoryMock = new();
    private readonly Mock<ICurrentUser> _currentUserMock = new();
    private readonly GetBriefingsByDesignerQueryHandler _handler;

    public GetBriefingsByDesignerQueryHandlerTests()
    {
        _handler = new GetBriefingsByDesignerQueryHandler(
            _briefingRepositoryMock.Object,
            new BriefingComplementsLoader(_categoryRepositoryMock.Object, _userRepositoryMock.Object),
            _currentUserMock.Object);

        _categoryRepositoryMock.Setup(r => r.GetByIdsAsync(It.IsAny<IReadOnlyCollection<Guid>>(), default)).ReturnsAsync([]);
        _userRepositoryMock.Setup(r => r.GetByIdsAsync(It.IsAny<IReadOnlyCollection<Guid>>(), default)).ReturnsAsync([]);
    }

    [Fact]
    public async Task Handle_Always_ScopesTheQueryToTheCallerAndStatus()
    {
        // Arrange
        var designerId = Guid.NewGuid();
        _currentUserMock.Setup(u => u.UserId).Returns(designerId.ToString());
        _briefingRepositoryMock
            .Setup(r => r.GetByDesignerAsync(It.IsAny<Guid>(), It.IsAny<BriefingStatus?>(), 1, 10, default))
            .ReturnsAsync([]);

        // Act
        await _handler.Handle(new GetBriefingsByDesignerQuery(1, 10, BriefingStatus.InProgress), default);

        // Assert
        _briefingRepositoryMock.Verify(r => r.GetByDesignerAsync(designerId, BriefingStatus.InProgress, 1, 10, default), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenCurrentUserIsNotAuthenticated_ReturnsUnauthorized()
    {
        // Arrange
        _currentUserMock.Setup(u => u.UserId).Returns((string?)null);

        // Act
        var result = await _handler.Handle(new GetBriefingsByDesignerQuery(1, 10), default);

        // Assert
        result.Type.Should().Be(ErrorType.Unauthorized);
    }
}
