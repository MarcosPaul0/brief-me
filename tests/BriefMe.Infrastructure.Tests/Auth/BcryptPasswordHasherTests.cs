using BriefMe.Infrastructure.Auth;
using FluentAssertions;
using Microsoft.Extensions.Options;

namespace BriefMe.Infrastructure.Tests.Auth;

public class BcryptPasswordHasherTests
{
    private readonly BcryptPasswordHasher _hasher = new(Options.Create(new PasswordHasherSettings { WorkFactor = 4 }));

    [Fact]
    public void Hash_WhenCalledTwiceWithSamePassword_ReturnsDifferentHashesThatBothVerify()
    {
        // Arrange
        const string password = "S3nhaSegura!";

        // Act
        var hash1 = _hasher.Hash(password);
        var hash2 = _hasher.Hash(password);

        // Assert
        hash1.Should().NotBe(hash2);
        _hasher.Verify(password, hash1).Should().BeTrue();
        _hasher.Verify(password, hash2).Should().BeTrue();
    }

    [Fact]
    public void Verify_WhenPasswordIsCorrect_ReturnsTrue()
    {
        // Arrange
        const string password = "S3nhaSegura!";
        var hash = _hasher.Hash(password);

        // Act
        var result = _hasher.Verify(password, hash);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void Verify_WhenPasswordIsIncorrect_ReturnsFalse()
    {
        // Arrange
        var hash = _hasher.Hash("S3nhaSegura!");

        // Act
        var result = _hasher.Verify("SenhaErrada!", hash);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void Hash_WhenWorkFactorIsConfigured_ProducesHashEncodingThatFactor()
    {
        // Arrange
        var hasher = new BcryptPasswordHasher(Options.Create(new PasswordHasherSettings { WorkFactor = 10 }));

        // Act
        var hash = hasher.Hash("S3nhaSegura!");

        // Assert
        hash.Should().Contain("$10$");
    }
}
