using BriefMe.Domain.Entities;
using BriefMe.Domain.Enums;
using BriefMe.Domain.ValueObjects;
using BriefMe.Infrastructure.Persistence;
using BriefMe.Infrastructure.Persistence.Repositories;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace BriefMe.Infrastructure.Tests.Persistence.Repositories;

public class EfRepositoryTests
{
    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new AppDbContext(options);
    }

    [Fact]
    public async Task GetByIdAsync_WhenEntityExists_ReturnsEntity()
    {
        // Arrange
        await using var context = CreateContext();
        var repository = new EfRepository<User>(context);
        var user = User.Create("Ana Silva", Email.Create("ana@empresa.com").Value, "hash", UserRole.Requester);
        await repository.AddAsync(user);
        await context.SaveChangesAsync();

        // Act
        var result = await repository.GetByIdAsync(user.Id);

        // Assert
        result.Should().Be(user);
    }

    [Fact]
    public async Task GetByIdAsync_WhenEntityDoesNotExist_ReturnsNull()
    {
        // Arrange
        await using var context = CreateContext();
        var repository = new EfRepository<User>(context);

        // Act
        var result = await repository.GetByIdAsync(Guid.NewGuid());

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task AddAsync_WhenCommitted_PersistsEntity()
    {
        // Arrange
        await using var context = CreateContext();
        var repository = new EfRepository<User>(context);
        var user = User.Create("Ana Silva", Email.Create("ana@empresa.com").Value, "hash", UserRole.Requester);

        // Act
        await repository.AddAsync(user);
        await context.SaveChangesAsync();

        // Assert
        (await context.Users.FindAsync(user.Id)).Should().NotBeNull();
    }

    [Fact]
    public async Task Update_WhenCommitted_PersistsChanges()
    {
        // Arrange
        await using var context = CreateContext();
        var repository = new EfRepository<User>(context);
        var user = User.Create("Ana Silva", Email.Create("ana@empresa.com").Value, "hash", UserRole.Requester);
        await repository.AddAsync(user);
        await context.SaveChangesAsync();

        // Act
        user.ChangePassword("new-hash");
        repository.Update(user);
        await context.SaveChangesAsync();

        // Assert
        var persisted = await context.Users.FindAsync(user.Id);
        persisted!.PasswordHash.Should().Be("new-hash");
    }

    [Fact]
    public async Task Remove_WhenCommitted_DeletesEntity()
    {
        // Arrange
        await using var context = CreateContext();
        var repository = new EfRepository<User>(context);
        var user = User.Create("Ana Silva", Email.Create("ana@empresa.com").Value, "hash", UserRole.Requester);
        await repository.AddAsync(user);
        await context.SaveChangesAsync();

        // Act
        repository.Remove(user);
        await context.SaveChangesAsync();

        // Assert
        (await context.Users.FindAsync(user.Id)).Should().BeNull();
    }

    [Fact]
    public async Task Query_WhenEntityIsSoftDeleted_ExcludesItByDefault()
    {
        // Arrange
        await using var context = CreateContext();
        var repository = new EfRepository<User>(context);
        var user = User.Create("Ana Silva", Email.Create("ana@empresa.com").Value, "hash", UserRole.Requester);
        await repository.AddAsync(user);
        await context.SaveChangesAsync();
        user.MarkAsDeleted("admin@briefme.com");
        repository.Update(user);
        await context.SaveChangesAsync();

        // Act
        var result = repository.Query().ToList();

        // Assert
        result.Should().BeEmpty();
    }
}
