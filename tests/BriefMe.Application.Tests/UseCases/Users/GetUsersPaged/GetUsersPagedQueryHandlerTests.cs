using BriefMe.Application.UseCases.Users.GetUsersPaged;
using BriefMe.Domain.Common;
using BriefMe.Domain.Entities;
using BriefMe.Domain.Enums;
using BriefMe.Domain.Repositories;
using BriefMe.Domain.ValueObjects;
using FluentAssertions;
using Moq;

namespace BriefMe.Application.Tests.UseCases.Users.GetUsersPaged;

public class GetUsersPagedQueryHandlerTests
{
    private readonly Mock<IUserRepository> _userRepositoryMock = new();
    private readonly Mock<IRoleRepository> _roleRepositoryMock = new();
    private readonly GetUsersPagedQueryHandler _handler;

    public GetUsersPagedQueryHandlerTests()
    {
        _handler = new GetUsersPagedQueryHandler(_userRepositoryMock.Object, _roleRepositoryMock.Object);
    }

    private static User CreateUser(string fullName = "Ana Silva", string email = "ana@briefme.com") =>
        User.Create(fullName, Email.Create(email).Value, "hashed-password");

    [Fact]
    public async Task Handle_WhenRepositoryReturnsPage_ReturnsMappedItemsWithPaginationMetadata()
    {
        // Arrange
        var role = Role.Create("Designer", null);
        var user = CreateUser();
        user.AssignRole(role.Id);

        var query = new GetUsersPagedQuery(Search: null, Status: null, RoleId: null, Page: 2, PageSize: 10);
        var expectedFilter = new UserFilter(null, null, null);
        var expectedSort = new UserSort(UserSortField.FullName, false);

        _userRepositoryMock
            .Setup(r => r.GetPagedAsync(expectedFilter, 2, 10, expectedSort, default))
            .ReturnsAsync([user]);

        _userRepositoryMock
            .Setup(r => r.CountAsync(expectedFilter, default))
            .ReturnsAsync(25);

        _roleRepositoryMock
            .Setup(r => r.GetByIdsAsync(It.Is<IEnumerable<Guid>>(ids => ids.Contains(role.Id)), default))
            .ReturnsAsync([role]);

        // Act
        var result = await _handler.Handle(query, default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Page.Should().Be(2);
        result.Value.PageSize.Should().Be(10);
        result.Value.TotalCount.Should().Be(25);
        var item = result.Value.Items.Should().ContainSingle().Subject;
        item.UserId.Should().Be(user.Id);
        item.FullName.Should().Be("Ana Silva");
        item.Email.Should().Be("ana@briefme.com");
        item.Status.Should().Be(UserStatus.Active);
        item.Roles.Should().BeEquivalentTo(["Designer"]);
    }

    [Fact]
    public async Task Handle_WhenFiltersAndSortAreInformed_PassesThemToRepository()
    {
        // Arrange
        var roleId = Guid.NewGuid();
        var query = new GetUsersPagedQuery(
            Search: "ana",
            Status: UserStatus.Active,
            RoleId: roleId,
            Page: 1,
            PageSize: 20,
            SortField: UserSortField.Email,
            SortDescending: true);

        var expectedFilter = new UserFilter("ana", UserStatus.Active, roleId);
        var expectedSort = new UserSort(UserSortField.Email, true);

        _userRepositoryMock
            .Setup(r => r.GetPagedAsync(expectedFilter, 1, 20, expectedSort, default))
            .ReturnsAsync([]);

        // Act
        var result = await _handler.Handle(query, default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        _userRepositoryMock.Verify(r => r.GetPagedAsync(expectedFilter, 1, 20, expectedSort, default), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenPageIsEmpty_ReturnsEmptyItemsWithoutQueryingRoles()
    {
        // Arrange
        var query = new GetUsersPagedQuery(Search: null, Status: null, RoleId: null, Page: 1, PageSize: 10);

        _userRepositoryMock
            .Setup(r => r.GetPagedAsync(It.IsAny<UserFilter>(), 1, 10, It.IsAny<UserSort>(), default))
            .ReturnsAsync([]);

        // Act
        var result = await _handler.Handle(query, default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Should().BeEmpty();
        _roleRepositoryMock.Verify(r => r.GetByIdsAsync(It.IsAny<IEnumerable<Guid>>(), default), Times.Never);
    }
}
