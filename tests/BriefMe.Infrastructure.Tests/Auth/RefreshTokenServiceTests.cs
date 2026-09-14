using System.Security.Cryptography;
using System.Text;
using BriefMe.Domain.Entities;
using BriefMe.Infrastructure.Auth;
using BriefMe.Infrastructure.Persistence;
using BriefMe.Infrastructure.Persistence.Repositories;
using BriefMe.Infrastructure.Tests.TestDoubles;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace BriefMe.Infrastructure.Tests.Auth;

public class RefreshTokenServiceTests
{
    private static RefreshTokenSettings CreateSettings() => new() { ExpirationDays = 7 };

    private static string Hash(string plainToken) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(plainToken)));

    private static (AppDbContext Context, RefreshTokenService Service) CreateSut()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        var context = new AppDbContext(options);
        var repository = new RefreshTokenRepository(context);
        var unitOfWork = new UnitOfWork(context, new RecordingDomainEventDispatcher());
        var service = new RefreshTokenService(repository, unitOfWork, Options.Create(CreateSettings()));

        return (context, service);
    }

    [Fact]
    public async Task GenerateAsync_WhenCalled_PersistsHashedTokenAndReturnsPlainToken()
    {
        // Arrange
        var (context, service) = CreateSut();
        var userId = Guid.NewGuid();

        // Act
        var plainToken = await service.GenerateAsync(userId, "127.0.0.1");

        // Assert
        var stored = await context.RefreshTokens.SingleAsync();
        plainToken.Should().NotBeNullOrWhiteSpace();
        stored.TokenHash.Should().NotBe(plainToken);
        stored.UserId.Should().Be(userId);
        stored.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task GenerateAsync_WhenCalledTwice_ReturnsDifferentTokens()
    {
        // Arrange
        var (_, service) = CreateSut();
        var userId = Guid.NewGuid();

        // Act
        var first = await service.GenerateAsync(userId, null);
        var second = await service.GenerateAsync(userId, null);

        // Assert
        first.Should().NotBe(second);
    }

    [Fact]
    public async Task RotateAsync_WhenTokenIsActive_RevokesOldTokenAndPersistsNewOne()
    {
        // Arrange
        var (context, service) = CreateSut();
        var userId = Guid.NewGuid();
        var currentToken = await service.GenerateAsync(userId, "127.0.0.1");

        // Act
        var result = await service.RotateAsync(currentToken, "127.0.0.2");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.UserId.Should().Be(userId);
        result.Value.Token.Should().NotBe(currentToken);

        var tokens = await context.RefreshTokens.ToListAsync();
        tokens.Should().HaveCount(2);
        tokens.Should().ContainSingle(t => t.IsRevoked && t.ReplacedByTokenHash != null);
        tokens.Should().ContainSingle(t => t.IsActive);
    }

    [Fact]
    public async Task RotateAsync_WhenTokenDoesNotExist_ReturnsFailure()
    {
        // Arrange
        var (_, service) = CreateSut();

        // Act
        var result = await service.RotateAsync("token-inexistente", null);

        // Assert
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task RotateAsync_WhenTokenIsExpired_ReturnsFailure()
    {
        // Arrange
        var (context, service) = CreateSut();
        var userId = Guid.NewGuid();
        const string plainToken = "plain-value-for-expired-token";
        var expired = RefreshToken.Create(Hash(plainToken), userId, DateTime.UtcNow.AddDays(-1), null);
        await context.RefreshTokens.AddAsync(expired);
        await context.SaveChangesAsync();

        // Act
        var result = await service.RotateAsync(plainToken, null);

        // Assert
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task RotateAsync_WhenTokenAlreadyRevoked_RevokesAllActiveTokensOfUserAndReturnsFailure()
    {
        // Arrange
        var (context, service) = CreateSut();
        var userId = Guid.NewGuid();
        var reusedToken = await service.GenerateAsync(userId, "127.0.0.1");
        var firstRotation = await service.RotateAsync(reusedToken, "127.0.0.1");
        firstRotation.IsSuccess.Should().BeTrue();

        // Act
        var result = await service.RotateAsync(reusedToken, "127.0.0.3");

        // Assert
        result.IsFailure.Should().BeTrue();
        var tokens = await context.RefreshTokens.Where(t => t.UserId == userId).ToListAsync();
        tokens.Should().OnlyContain(t => t.IsRevoked);
    }

    [Fact]
    public async Task RevokeAsync_WhenTokenIsActive_MarksTokenAsRevoked()
    {
        // Arrange
        var (context, service) = CreateSut();
        var userId = Guid.NewGuid();
        var token = await service.GenerateAsync(userId, "127.0.0.1");

        // Act
        await service.RevokeAsync(token, "127.0.0.1");

        // Assert
        var stored = await context.RefreshTokens.SingleAsync();
        stored.IsRevoked.Should().BeTrue();
    }

    [Fact]
    public async Task RevokeAsync_WhenTokenDoesNotExist_DoesNotThrow()
    {
        // Arrange
        var (_, service) = CreateSut();

        // Act
        var act = async () => await service.RevokeAsync("token-inexistente", null);

        // Assert
        await act.Should().NotThrowAsync();
    }
}
