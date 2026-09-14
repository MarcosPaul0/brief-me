using BriefMe.Domain.Entities;
using BriefMe.Domain.ValueObjects;
using BriefMe.Infrastructure.Persistence;
using BriefMe.Infrastructure.Persistence.Interceptors;
using BriefMe.Infrastructure.Tests.TestDoubles;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace BriefMe.Infrastructure.Tests.Persistence;

public class SoftDeleteQueryFilterTests
{
    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .AddInterceptors(new AuditableEntitySaveChangesInterceptor(new FakeCurrentUser()))
            .Options;

        return new AppDbContext(options);
    }

    [Fact]
    public async Task Users_WhenUserIsSoftDeleted_IsExcludedFromDefaultQuery()
    {
        // Arrange
        await using var context = CreateContext();
        var active = User.Create("Ativo Silva", Email.Create("ativo@empresa.com").Value, "hash");
        var deleted = User.Create("Deletado Souza", Email.Create("deletado@empresa.com").Value, "hash");
        deleted.MarkAsDeleted("admin@briefme.com");
        context.Users.AddRange(active, deleted);
        await context.SaveChangesAsync();

        // Act
        var users = await context.Users.ToListAsync();

        // Assert
        users.Should().ContainSingle().Which.Id.Should().Be(active.Id);
    }

    [Fact]
    public async Task Users_WhenIgnoringQueryFilters_IncludesSoftDeletedUser()
    {
        // Arrange
        await using var context = CreateContext();
        var active = User.Create("Ativo Silva", Email.Create("ativo@empresa.com").Value, "hash");
        var deleted = User.Create("Deletado Souza", Email.Create("deletado@empresa.com").Value, "hash");
        deleted.MarkAsDeleted("admin@briefme.com");
        context.Users.AddRange(active, deleted);
        await context.SaveChangesAsync();

        // Act
        var users = await context.Users.IgnoreQueryFilters().ToListAsync();

        // Assert
        users.Should().HaveCount(2);
    }
}
