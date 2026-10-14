using BriefMe.Domain.Entities;
using BriefMe.Domain.Enums;
using BriefMe.Domain.ValueObjects;
using BriefMe.Infrastructure.Persistence;
using BriefMe.Infrastructure.Persistence.Interceptors;
using BriefMe.Infrastructure.Tests.TestDoubles;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace BriefMe.Infrastructure.Tests.Persistence;

public class AuditableEntitySaveChangesInterceptorTests
{
    private static AppDbContext CreateContext(string? currentUserId)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .AddInterceptors(new AuditableEntitySaveChangesInterceptor(new FakeCurrentUser(currentUserId)))
            .Options;

        return new AppDbContext(options);
    }

    [Fact]
    public async Task SaveChangesAsync_WhenEntityIsAdded_PopulatesCreatedAtAndCreatedBy()
    {
        // Arrange
        await using var context = CreateContext("admin@briefme.com");
        var user = User.Create("Ana Silva", Email.Create("ana@empresa.com").Value, "hash", UserRole.Requester);
        context.Users.Add(user);

        // Act
        await context.SaveChangesAsync();

        // Assert
        user.CreatedAt.Should().NotBe(default);
        user.CreatedBy.Should().Be("admin@briefme.com");
    }

    [Fact]
    public async Task SaveChangesAsync_WhenEntityIsModified_PopulatesUpdatedAtAndUpdatedBy()
    {
        // Arrange
        await using var context = CreateContext("admin@briefme.com");
        var user = User.Create("Ana Silva", Email.Create("ana@empresa.com").Value, "hash", UserRole.Requester);
        context.Users.Add(user);
        await context.SaveChangesAsync();

        // Act
        user.ChangePassword("new-hash");
        await context.SaveChangesAsync();

        // Assert
        user.UpdatedAt.Should().NotBeNull();
        user.UpdatedBy.Should().Be("admin@briefme.com");
    }
}
