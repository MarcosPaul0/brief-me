using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using BriefMe.Infrastructure.Auth;
using BriefMe.Web.Auth;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace BriefMe.Web.Tests.Auth;

public class JwtClaimsPrincipalFactoryTests
{
    private const string SecretKey = "web-tests-jwt-secret-key-with-at-least-32-bytes!";

    private static JwtSettings CreateSettings() => new()
    {
        Issuer = "BriefMe.Web.Tests",
        Audience = "BriefMe.Web.Tests.Audience",
        SecretKey = SecretKey,
        AccessTokenMinutes = 15
    };

    private static JwtClaimsPrincipalFactory CreateFactory(JwtSettings settings) =>
        new(Options.Create(settings));

    private static string CreateToken(JwtSettings settings, IEnumerable<Claim> claims, TimeSpan expiresIn)
    {
        var signingCredentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(settings.SecretKey)),
            SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: settings.Issuer,
            audience: settings.Audience,
            claims: claims,
            expires: DateTime.UtcNow.Add(expiresIn),
            signingCredentials: signingCredentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    [Fact]
    public void TryCreatePrincipal_WhenTokenIsValid_ReturnsTrueWithExpectedClaims()
    {
        // Arrange
        var settings = CreateSettings();
        var userId = Guid.NewGuid().ToString();
        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, userId),
            new Claim(JwtRegisteredClaimNames.Email, "ana@briefme.com"),
            new Claim(JwtRegisteredClaimNames.Name, "Ana Silva"),
            new Claim(ClaimTypes.Role, "Designer")
        };
        var token = CreateToken(settings, claims, TimeSpan.FromMinutes(15));
        var factory = CreateFactory(settings);

        // Act
        var success = factory.TryCreatePrincipal(token, out var principal);

        // Assert
        success.Should().BeTrue();
        principal!.FindFirst(ClaimTypes.NameIdentifier)!.Value.Should().Be(userId);
        principal.FindFirst(ClaimTypes.Email)!.Value.Should().Be("ana@briefme.com");
        // "name" (unlike "sub"/"email") isn't in JwtSecurityTokenHandler's default inbound claim map, so it stays as-is.
        principal.FindFirst(JwtRegisteredClaimNames.Name)!.Value.Should().Be("Ana Silva");
        principal.IsInRole("Designer").Should().BeTrue();
    }

    [Fact]
    public void TryCreatePrincipal_WhenTokenIsExpired_ReturnsFalse()
    {
        // Arrange
        var settings = CreateSettings();
        var token = CreateToken(settings, [new Claim(JwtRegisteredClaimNames.Sub, Guid.NewGuid().ToString())], TimeSpan.FromMinutes(-5));
        var factory = CreateFactory(settings);

        // Act
        var success = factory.TryCreatePrincipal(token, out var principal);

        // Assert
        success.Should().BeFalse();
        principal.Should().BeNull();
    }

    [Fact]
    public void TryCreatePrincipal_WhenTokenIsTampered_ReturnsFalse()
    {
        // Arrange
        var settings = CreateSettings();
        var token = CreateToken(settings, [new Claim(JwtRegisteredClaimNames.Sub, Guid.NewGuid().ToString())], TimeSpan.FromMinutes(15));
        var tamperedToken = token[..^2] + "xx";
        var factory = CreateFactory(settings);

        // Act
        var success = factory.TryCreatePrincipal(tamperedToken, out var principal);

        // Assert
        success.Should().BeFalse();
        principal.Should().BeNull();
    }

    [Fact]
    public void TryCreatePrincipal_WhenTokenIsMalformed_ReturnsFalseWithoutThrowing()
    {
        // Arrange
        var factory = CreateFactory(CreateSettings());

        // Act
        var action = () => factory.TryCreatePrincipal("not-a-jwt", out var principal);

        // Assert
        action.Should().NotThrow();
        factory.TryCreatePrincipal("not-a-jwt", out var result).Should().BeFalse();
        result.Should().BeNull();
    }
}
