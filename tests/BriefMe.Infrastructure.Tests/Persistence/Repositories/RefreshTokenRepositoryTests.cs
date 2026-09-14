using BriefMe.Domain.Entities;
using BriefMe.Infrastructure.Persistence;
using BriefMe.Infrastructure.Persistence.Repositories;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace BriefMe.Infrastructure.Tests.Persistence.Repositories;

public class RefreshTokenRepositoryTests
{
    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new AppDbContext(options);
    }

    [Fact]
    public async Task GetByTokenHashAsync_WhenHashExists_ReturnsToken()
    {
        // Arrange
        await using var context = CreateContext();
        var token = RefreshToken.Create("hash-1", Guid.NewGuid(), DateTime.UtcNow.AddDays(7), "127.0.0.1");
        await context.RefreshTokens.AddAsync(token);
        await context.SaveChangesAsync();
        var repository = new RefreshTokenRepository(context);

        // Act
        var result = await repository.GetByTokenHashAsync("hash-1");

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(token.Id);
    }

    [Fact]
    public async Task GetByTokenHashAsync_WhenHashDoesNotExist_ReturnsNull()
    {
        // Arrange
        await using var context = CreateContext();
        var repository = new RefreshTokenRepository(context);

        // Act
        var result = await repository.GetByTokenHashAsync("inexistente");

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetActiveByUserAsync_WhenUserHasActiveAndInactiveTokens_ReturnsOnlyActiveOnes()
    {
        // Arrange
        await using var context = CreateContext();
        var userId = Guid.NewGuid();
        var active = RefreshToken.Create("active-hash", userId, DateTime.UtcNow.AddDays(7), null);
        var expired = RefreshToken.Create("expired-hash", userId, DateTime.UtcNow.AddDays(-1), null);
        var revoked = RefreshToken.Create("revoked-hash", userId, DateTime.UtcNow.AddDays(7), null);
        revoked.Revoke(null);

        await context.RefreshTokens.AddRangeAsync(active, expired, revoked);
        await context.SaveChangesAsync();
        var repository = new RefreshTokenRepository(context);

        // Act
        var result = await repository.GetActiveByUserAsync(userId);

        // Assert
        result.Should().ContainSingle(rt => rt.Id == active.Id);
    }

    [Fact]
    public async Task GetActiveByUserAsync_WhenUserHasNoTokens_ReturnsEmpty()
    {
        // Arrange
        await using var context = CreateContext();
        var repository = new RefreshTokenRepository(context);

        // Act
        var result = await repository.GetActiveByUserAsync(Guid.NewGuid());

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task RevokeAllActiveForUserAsync_WhenUserHasActiveTokens_RevokesAllOfThem()
    {
        // Arrange
        await using var context = CreateContext();
        var userId = Guid.NewGuid();
        var first = RefreshToken.Create("hash-1", userId, DateTime.UtcNow.AddDays(7), null);
        var second = RefreshToken.Create("hash-2", userId, DateTime.UtcNow.AddDays(7), null);

        await context.RefreshTokens.AddRangeAsync(first, second);
        await context.SaveChangesAsync();
        var repository = new RefreshTokenRepository(context);

        // Act
        await repository.RevokeAllActiveForUserAsync(userId, "127.0.0.1");
        await context.SaveChangesAsync();

        // Assert
        var tokens = await context.RefreshTokens.Where(rt => rt.UserId == userId).ToListAsync();
        tokens.Should().OnlyContain(rt => rt.IsRevoked);
    }

    [Fact]
    public async Task RevokeAllActiveForUserAsync_WhenTokenAlreadyRevoked_DoesNotThrow()
    {
        // Arrange
        await using var context = CreateContext();
        var userId = Guid.NewGuid();
        var active = RefreshToken.Create("hash-1", userId, DateTime.UtcNow.AddDays(7), null);
        var alreadyRevoked = RefreshToken.Create("hash-2", userId, DateTime.UtcNow.AddDays(7), null);
        alreadyRevoked.Revoke(null);

        await context.RefreshTokens.AddRangeAsync(active, alreadyRevoked);
        await context.SaveChangesAsync();
        var repository = new RefreshTokenRepository(context);

        // Act
        var act = async () => await repository.RevokeAllActiveForUserAsync(userId, "127.0.0.1");

        // Assert
        await act.Should().NotThrowAsync();
    }
}
