using BriefMe.Domain.Entities;
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
        var permission = Permission.Create("users.create", "Criar usuários", null, "Usuários");
        context.Permissions.Add(permission);

        // Act
        await context.SaveChangesAsync();

        // Assert
        permission.CreatedAt.Should().NotBe(default);
        permission.CreatedBy.Should().Be("admin@briefme.com");
    }

    [Fact]
    public async Task SaveChangesAsync_WhenEntityIsModified_PopulatesUpdatedAtAndUpdatedBy()
    {
        // Arrange
        await using var context = CreateContext("admin@briefme.com");
        var role = Role.Create("Designer", null);
        context.Roles.Add(role);
        await context.SaveChangesAsync();

        // Act
        role.Rename("Designer Sênior");
        await context.SaveChangesAsync();

        // Assert
        role.UpdatedAt.Should().NotBeNull();
        role.UpdatedBy.Should().Be("admin@briefme.com");
    }
}
