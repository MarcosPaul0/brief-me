using BriefMe.Application.Common.Briefings;
using BriefMe.Application.UseCases.Briefings.GetBriefingQueue;
using BriefMe.Domain.Entities;
using BriefMe.Domain.Enums;
using BriefMe.Domain.Repositories;
using BriefMe.Domain.ValueObjects;
using FluentAssertions;
using Moq;

namespace BriefMe.Application.Tests.UseCases.Briefings.GetBriefingQueue;

public class GetBriefingQueueQueryHandlerTests
{
    private readonly Mock<IBriefingRepository> _briefingRepositoryMock = new();
    private readonly Mock<ICategoryRepository> _categoryRepositoryMock = new();
    private readonly Mock<IUserRepository> _userRepositoryMock = new();
    private readonly GetBriefingQueueQueryHandler _handler;

    public GetBriefingQueueQueryHandlerTests()
    {
        _handler = new GetBriefingQueueQueryHandler(
            _briefingRepositoryMock.Object,
            new BriefingComplementsLoader(_categoryRepositoryMock.Object, _userRepositoryMock.Object));
    }

    [Fact]
    public async Task Handle_WhenQueueHasBriefings_ReturnsRowsWithRequesterNameAndTotal()
    {
        // Arrange
        var requester = User.Create("Rita Requester", Email.Create("rita@briefme.com").Value, "hash", UserRole.Requester);
        var category = Category.Create("Evento");
        var briefing = Briefing.Create("Banner", Priority.High, category.Id, requester.Id);
        _briefingRepositoryMock.Setup(r => r.GetQueueAsync(1, 10, default)).ReturnsAsync([briefing]);
        _briefingRepositoryMock.Setup(r => r.CountQueueAsync(default)).ReturnsAsync(12);
        _categoryRepositoryMock.Setup(r => r.GetByIdsAsync(It.IsAny<IReadOnlyCollection<Guid>>(), default)).ReturnsAsync([category]);
        _userRepositoryMock.Setup(r => r.GetByIdsAsync(It.IsAny<IReadOnlyCollection<Guid>>(), default)).ReturnsAsync([requester]);

        // Act
        var result = await _handler.Handle(new GetBriefingQueueQuery(1, 10), default);

        // Assert
        result.Value.TotalCount.Should().Be(12);
        result.Value.Items.Single().Should().BeEquivalentTo(new { RequesterName = "Rita Requester", CategoryName = "Evento" });
    }
}
