using BriefMe.Infrastructure.Auth;
using FluentAssertions;
using Microsoft.IdentityModel.Tokens;

namespace BriefMe.Infrastructure.Tests.Auth;

public class JwtTokenValidationParametersTests
{
    private const string SecretKey = "unit-test-secret-key-with-at-least-32-bytes!";

    private static JwtSettings CreateSettings() => new()
    {
        Issuer = "BriefMe.Tests",
        Audience = "BriefMe.Tests.Audience",
        SecretKey = SecretKey,
        AccessTokenMinutes = 15
    };

    [Fact]
    public void Build_WhenCalled_EnablesIssuerAudienceLifetimeAndSigningKeyValidation()
    {
        // Arrange
        var settings = CreateSettings();

        // Act
        var result = JwtTokenValidationParameters.Build(settings);

        // Assert
        result.ValidateIssuer.Should().BeTrue();
        result.ValidateAudience.Should().BeTrue();
        result.ValidateLifetime.Should().BeTrue();
        result.ValidateIssuerSigningKey.Should().BeTrue();
    }

    [Fact]
    public void Build_WhenCalled_UsesIssuerAudienceAndSigningKeyFromSettings()
    {
        // Arrange
        var settings = CreateSettings();

        // Act
        var result = JwtTokenValidationParameters.Build(settings);

        // Assert
        result.ValidIssuer.Should().Be(settings.Issuer);
        result.ValidAudience.Should().Be(settings.Audience);
        var key = result.IssuerSigningKey.Should().BeOfType<SymmetricSecurityKey>().Subject;
        key.Key.Should().Equal(System.Text.Encoding.UTF8.GetBytes(settings.SecretKey));
    }

    [Fact]
    public void Build_WhenCalled_SetsClockSkewToOneMinute()
    {
        // Arrange
        var settings = CreateSettings();

        // Act
        var result = JwtTokenValidationParameters.Build(settings);

        // Assert
        result.ClockSkew.Should().Be(TimeSpan.FromMinutes(1));
    }
}
