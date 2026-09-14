using BriefMe.Domain.Entities;
using BriefMe.Domain.Exceptions;
using FluentAssertions;

namespace BriefMe.Domain.Tests.Entities;

public class RefreshTokenTests
{
    [Fact]
    public void Create_WhenCalled_SetsFieldsAndTokenIsActive()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var expiresAt = DateTime.UtcNow.AddDays(7);

        // Act
        var token = RefreshToken.Create("hash", userId, expiresAt, "127.0.0.1");

        // Assert
        token.TokenHash.Should().Be("hash");
        token.UserId.Should().Be(userId);
        token.ExpiresAt.Should().Be(expiresAt);
        token.CreatedByIp.Should().Be("127.0.0.1");
        token.IsActive.Should().BeTrue();
    }

    [Fact]
    public void IsActive_WhenExpiresAtIsInThePast_ReturnsFalse()
    {
        // Arrange
        var token = RefreshToken.Create("hash", Guid.NewGuid(), DateTime.UtcNow.AddDays(-1), "127.0.0.1");

        // Act
        var isActive = token.IsActive;

        // Assert
        isActive.Should().BeFalse();
    }

    [Fact]
    public void IsActive_WhenNotExpiredAndNotRevoked_ReturnsTrue()
    {
        // Arrange
        var token = RefreshToken.Create("hash", Guid.NewGuid(), DateTime.UtcNow.AddDays(7), "127.0.0.1");

        // Act
        var isActive = token.IsActive;

        // Assert
        isActive.Should().BeTrue();
    }

    [Fact]
    public void IsExpired_WhenExpiresAtIsInThePast_ReturnsTrue()
    {
        // Arrange
        var token = RefreshToken.Create("hash", Guid.NewGuid(), DateTime.UtcNow.AddDays(-1), "127.0.0.1");

        // Act
        var isExpired = token.IsExpired;

        // Assert
        isExpired.Should().BeTrue();
    }

    [Fact]
    public void Revoke_WhenTokenIsActive_SetsRevokedAtAndIsActiveBecomesFalse()
    {
        // Arrange
        var token = RefreshToken.Create("hash", Guid.NewGuid(), DateTime.UtcNow.AddDays(7), "127.0.0.1");

        // Act
        token.Revoke("127.0.0.1");

        // Assert
        token.RevokedAt.Should().NotBeNull();
        token.IsActive.Should().BeFalse();
    }

    [Fact]
    public void Revoke_WhenReplacedByTokenHashProvided_SetsRotationFields()
    {
        // Arrange
        var token = RefreshToken.Create("hash", Guid.NewGuid(), DateTime.UtcNow.AddDays(7), "127.0.0.1");

        // Act
        token.Revoke("10.0.0.1", "new-hash");

        // Assert
        token.RevokedByIp.Should().Be("10.0.0.1");
        token.ReplacedByTokenHash.Should().Be("new-hash");
        token.IsRevoked.Should().BeTrue();
    }

    [Fact]
    public void Revoke_WhenTokenAlreadyRevoked_ThrowsDomainException()
    {
        // Arrange
        var token = RefreshToken.Create("hash", Guid.NewGuid(), DateTime.UtcNow.AddDays(7), "127.0.0.1");
        token.Revoke("127.0.0.1");

        // Act
        var act = () => token.Revoke("127.0.0.1");

        // Assert
        act.Should().Throw<DomainException>();
    }
}
