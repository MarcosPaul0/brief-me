using BriefMe.Domain.Entities;
using BriefMe.Domain.Enums;
using BriefMe.Domain.Repositories;
using BriefMe.Domain.ValueObjects;
using BriefMe.Infrastructure.Persistence;
using BriefMe.Infrastructure.Persistence.Repositories;
using BriefMe.Infrastructure.Tests.TestDoubles;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace BriefMe.Infrastructure.Tests.Persistence.Repositories;

public class UserRepositoryTests
{
    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new AppDbContext(options);
    }

    private static User CreateUser(
        string fullName,
        string email,
        UserStatus status = UserStatus.Active,
        UserRole role = UserRole.Requester)
    {
        var user = User.Create(fullName, Email.Create(email).Value, "hashed-password", role);

        if (status == UserStatus.Inactive)
            user.Deactivate();

        return user;
    }

    [Fact]
    public async Task GetByEmailAsync_WhenEmailDoesNotExist_ReturnsNull()
    {
        // Arrange
        await using var context = CreateContext();
        var repository = new UserRepository(context);

        // Act
        var result = await repository.GetByEmailAsync(Email.Create("naoexiste@briefme.com").Value);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task EmailExistsAsync_WhenEmailUsedByAnotherUser_ReturnsTrue()
    {
        // Arrange
        await using var context = CreateContext();
        var user = CreateUser("Ana Silva", "ana@briefme.com");
        await context.Users.AddAsync(user);
        await context.SaveChangesAsync();
        var repository = new UserRepository(context);

        // Act
        var result = await repository.EmailExistsAsync(Email.Create("ana@briefme.com").Value, ignoreId: Guid.NewGuid());

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task EmailExistsAsync_WhenIgnoreIdMatchesOwner_ReturnsFalse()
    {
        // Arrange
        await using var context = CreateContext();
        var user = CreateUser("Ana Silva", "ana@briefme.com");
        await context.Users.AddAsync(user);
        await context.SaveChangesAsync();
        var repository = new UserRepository(context);

        // Act
        var result = await repository.EmailExistsAsync(Email.Create("ana@briefme.com").Value, ignoreId: user.Id);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task EmailExistsAsync_WhenEmailDoesNotExist_ReturnsFalse()
    {
        // Arrange
        await using var context = CreateContext();
        var repository = new UserRepository(context);

        // Act
        var result = await repository.EmailExistsAsync(Email.Create("naoexiste@briefme.com").Value);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task GetByIdsAsync_WhenIdsMatchUsers_ReturnsOnlyThoseUsers()
    {
        // Arrange
        await using var context = CreateContext();
        var ana = CreateUser("Ana Silva", "ana@briefme.com");
        var bruno = CreateUser("Bruno Costa", "bruno@briefme.com");
        var carla = CreateUser("Carla Dias", "carla@briefme.com");
        await context.Users.AddRangeAsync(ana, bruno, carla);
        await context.SaveChangesAsync();
        var repository = new UserRepository(context);

        // Act
        var result = await repository.GetByIdsAsync([ana.Id, carla.Id]);

        // Assert
        result.Select(user => user.FullName).Should().BeEquivalentTo(["Ana Silva", "Carla Dias"]);
    }

    [Fact]
    public async Task GetByIdsAsync_WhenAnIdDoesNotExist_SkipsItInsteadOfFailing()
    {
        // Arrange
        await using var context = CreateContext();
        var ana = CreateUser("Ana Silva", "ana@briefme.com");
        await context.Users.AddAsync(ana);
        await context.SaveChangesAsync();
        var repository = new UserRepository(context);

        // Act
        var result = await repository.GetByIdsAsync([ana.Id, Guid.NewGuid()]);

        // Assert
        result.Should().ContainSingle().Which.Id.Should().Be(ana.Id);
    }

    [Fact]
    public async Task GetByIdsAsync_WhenNoIdsInformed_ReturnsEmptyWithoutQuerying()
    {
        // Arrange
        await using var context = CreateContext();
        var repository = new UserRepository(context);

        // Act
        var result = await repository.GetByIdsAsync([]);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetPagedAsync_WhenSearchMatchesFullNameOrEmail_ReturnsFilteredPage()
    {
        // Arrange
        await using var context = CreateContext();
        await context.Users.AddRangeAsync(
            CreateUser("Ana Silva", "ana@briefme.com"),
            CreateUser("Bruno Costa", "bruno@briefme.com"));
        await context.SaveChangesAsync();
        var repository = new UserRepository(context);

        // Act
        var result = await repository.GetPagedAsync(new UserFilter(Search: "ana"), page: 1, pageSize: 10, sort: new UserSort());

        // Assert
        result.Single().FullName.Should().Be("Ana Silva");
    }

    [Fact]
    public async Task GetPagedAsync_WhenStatusFilterApplied_ReturnsOnlyMatchingStatus()
    {
        // Arrange
        await using var context = CreateContext();
        await context.Users.AddRangeAsync(
            CreateUser("Ana Silva", "ana@briefme.com", UserStatus.Active),
            CreateUser("Bruno Costa", "bruno@briefme.com", UserStatus.Inactive));
        await context.SaveChangesAsync();
        var repository = new UserRepository(context);

        // Act
        var result = await repository.GetPagedAsync(new UserFilter(Status: UserStatus.Inactive), page: 1, pageSize: 10, sort: new UserSort());

        // Assert
        result.Single().FullName.Should().Be("Bruno Costa");
    }

    [Fact]
    public async Task GetPagedAsync_WhenPageBeyondTotal_ReturnsEmpty()
    {
        // Arrange
        await using var context = CreateContext();
        await context.Users.AddRangeAsync(
            CreateUser("Ana Silva", "ana@briefme.com"),
            CreateUser("Bruno Costa", "bruno@briefme.com"));
        await context.SaveChangesAsync();
        var repository = new UserRepository(context);

        // Act
        var result = await repository.GetPagedAsync(new UserFilter(), page: 2, pageSize: 10, sort: new UserSort());

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetPagedAsync_WhenSortedByEmailDescending_ReturnsOrderedResults()
    {
        // Arrange
        await using var context = CreateContext();
        await context.Users.AddRangeAsync(
            CreateUser("Ana Silva", "ana@briefme.com"),
            CreateUser("Bruno Costa", "bruno@briefme.com"));
        await context.SaveChangesAsync();
        var repository = new UserRepository(context);

        // Act
        var result = await repository.GetPagedAsync(
            new UserFilter(),
            page: 1,
            pageSize: 10,
            sort: new UserSort(UserSortField.Email, Descending: true));

        // Assert
        result.Select(u => u.Email.Value).Should().ContainInOrder("bruno@briefme.com", "ana@briefme.com");
    }

    [Fact]
    public async Task CountAsync_WhenResultIsPaged_CountsEveryMatchNotJustThePage()
    {
        // Arrange
        await using var context = CreateContext();
        await context.Users.AddRangeAsync(
            CreateUser("Ana Silva", "ana@briefme.com"),
            CreateUser("Bruno Costa", "bruno@briefme.com"),
            CreateUser("Carla Dias", "carla@briefme.com"));
        await context.SaveChangesAsync();
        var repository = new UserRepository(context);

        // Act
        var totalCount = await repository.CountAsync(new UserFilter());

        // Assert
        totalCount.Should().Be(3);
    }

    [Fact]
    public async Task CountAsync_WhenStatusFilterApplied_CountsMatchingOnly()
    {
        // Arrange
        await using var context = CreateContext();
        await context.Users.AddRangeAsync(
            CreateUser("Ana Silva", "ana@briefme.com", UserStatus.Active),
            CreateUser("Bruno Costa", "bruno@briefme.com", UserStatus.Inactive));
        await context.SaveChangesAsync();
        var repository = new UserRepository(context);

        // Act
        var totalCount = await repository.CountAsync(new UserFilter(Status: UserStatus.Inactive));

        // Assert
        totalCount.Should().Be(1);
    }

    [Fact]
    public async Task GetByEmailAsync_WhenEmailExists_ReturnsUserWithItsRole()
    {
        // Arrange
        await using var context = CreateContext();
        await context.Users.AddAsync(CreateUser("Ana Silva", "ana@briefme.com", role: UserRole.Designer));
        await context.SaveChangesAsync();
        var repository = new UserRepository(context);

        // Act
        var result = await repository.GetByEmailAsync(Email.Create("ana@briefme.com").Value);

        // Assert
        result!.Role.Should().Be(UserRole.Designer);
    }

    [Fact]
    public async Task GetPagedAsync_WhenRoleFilterApplied_ReturnsOnlyUsersWithRole()
    {
        // Arrange
        await using var context = CreateContext();
        await context.Users.AddRangeAsync(
            CreateUser("Ana Silva", "ana@briefme.com", role: UserRole.Designer),
            CreateUser("Bruno Costa", "bruno@briefme.com", role: UserRole.Requester));
        await context.SaveChangesAsync();
        var repository = new UserRepository(context);

        // Act
        var result = await repository.GetPagedAsync(new UserFilter(Role: UserRole.Designer), page: 1, pageSize: 10, sort: new UserSort());

        // Assert
        result.Should().ContainSingle().Which.FullName.Should().Be("Ana Silva");
    }
}
