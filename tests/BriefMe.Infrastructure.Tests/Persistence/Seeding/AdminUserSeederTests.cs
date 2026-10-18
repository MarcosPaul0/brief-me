using BriefMe.Application.Abstractions;
using BriefMe.Domain.Enums;
using BriefMe.Infrastructure.Persistence;
using BriefMe.Infrastructure.Persistence.Repositories;
using BriefMe.Infrastructure.Persistence.Seeding;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace BriefMe.Infrastructure.Tests.Persistence.Seeding;

public class AdminUserSeederTests
{
    private const string AdminEmail = "admin@empresa.com";
    private const string DefaultPassword = "Admin@123";

    // Deterministic stub so tests never depend on BCrypt cost/output.
    private sealed class StubPasswordHasher : IPasswordHasher
    {
        public string Hash(string password) => $"hashed:{password}";

        public bool Verify(string password, string hash) => hash == $"hashed:{password}";
    }

    private static AppDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static AdminUserSeeder CreateSeeder(AppDbContext context, IPasswordHasher hasher) =>
        new(
            new UserRepository(context),
            hasher,
            new UnitOfWork(context));

    [Fact]
    public async Task SeedAsync_WhenDatabaseIsFresh_CreatesActiveAdmin()
    {
        // Arrange
        await using var context = CreateContext();
        var seeder = CreateSeeder(context, new StubPasswordHasher());

        // Act
        await seeder.SeedAsync();

        // Assert
        var admin = await context.Users.SingleAsync();
        admin.Email.Value.Should().Be(AdminEmail);
        admin.Status.Should().Be(UserStatus.Active);
        admin.Role.Should().Be(UserRole.Admin);
    }

    [Fact]
    public async Task SeedAsync_WhenDatabaseIsFresh_StoresBcryptHashNotPlaintext()
    {
        // Arrange
        await using var context = CreateContext();
        var hasher = new StubPasswordHasher();
        var seeder = CreateSeeder(context, hasher);

        // Act
        await seeder.SeedAsync();

        // Assert
        var admin = await context.Users.SingleAsync();
        admin.PasswordHash.Should().NotBe(DefaultPassword);
        hasher.Verify(DefaultPassword, admin.PasswordHash).Should().BeTrue();
    }

    [Fact]
    public async Task SeedAsync_WhenRunTwice_DoesNotDuplicateTheAdminUser()
    {
        // Arrange
        await using var context = CreateContext();
        var seeder = CreateSeeder(context, new StubPasswordHasher());

        // Act
        await seeder.SeedAsync();
        await seeder.SeedAsync();

        // Assert
        var count = await context.Users.CountAsync();
        count.Should().Be(1);
    }
}
