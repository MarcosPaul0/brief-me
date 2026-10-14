using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using BriefMe.Domain.Entities;
using BriefMe.Domain.Enums;
using BriefMe.Domain.ValueObjects;
using BriefMe.Infrastructure.Auth;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace BriefMe.Infrastructure.Tests.Auth;

public class JwtServiceTests
{
    private const string SecretKey = "unit-test-secret-key-with-at-least-32-bytes!";

    private static JwtSettings CreateSettings() => new()
    {
        Issuer = "BriefMe.Tests",
        Audience = "BriefMe.Tests.Audience",
        SecretKey = SecretKey,
        AccessTokenMinutes = 15
    };

    private static User CreateUser(string fullName, string email, UserRole role = UserRole.Requester) =>
        User.Create(fullName, Email.Create(email).Value, "hashed-password", role);

    [Fact]
    public void GenerateAccessToken_WhenCalled_ReturnsTokenWithIdentityClaims()
    {
        // Arrange
        var service = new JwtService(Options.Create(CreateSettings()));
        var user = CreateUser("Ana Silva", "ana@briefme.com");

        // Act
        var (token, expiresAt) = service.GenerateAccessToken(user);

        // Assert
        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);
        jwt.Subject.Should().Be(user.Id.ToString());
        jwt.Claims.Single(c => c.Type == JwtRegisteredClaimNames.Email).Value.Should().Be(user.Email.Value);
        jwt.Claims.Single(c => c.Type == JwtRegisteredClaimNames.Name).Value.Should().Be(user.FullName);
        expiresAt.Should().BeCloseTo(DateTime.UtcNow.AddMinutes(15), TimeSpan.FromSeconds(5));
    }

    [Theory]
    [InlineData(UserRole.Admin, "Admin")]
    [InlineData(UserRole.Designer, "Designer")]
    [InlineData(UserRole.Requester, "Requester")]
    public void GenerateAccessToken_WhenCalled_CarriesExactlyTheUsersRole(UserRole role, string expectedClaim)
    {
        // Arrange
        var service = new JwtService(Options.Create(CreateSettings()));
        var user = CreateUser("Ana Silva", "ana@briefme.com", role);

        // Act
        var (token, _) = service.GenerateAccessToken(user);

        // Assert
        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);
        jwt.Claims.Where(c => c.Type == ClaimTypes.Role).Select(c => c.Value).Should().Equal(expectedClaim);
    }

    [Fact]
    public void GenerateAccessToken_WhenTokenIsGenerated_HasValidSignatureAndClaimsAgainstConfiguredParameters()
    {
        // Arrange
        var settings = CreateSettings();
        var service = new JwtService(Options.Create(settings));
        var user = CreateUser("Ana Silva", "ana@briefme.com");

        // Act
        var (token, _) = service.GenerateAccessToken(user);

        // Assert
        var handler = new JwtSecurityTokenHandler();
        var validationParameters = new TokenValidationParameters
        {
            ValidIssuer = settings.Issuer,
            ValidAudience = settings.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(System.Text.Encoding.UTF8.GetBytes(settings.SecretKey)),
            ClockSkew = TimeSpan.Zero
        };

        var action = () => handler.ValidateToken(token, validationParameters, out _);

        action.Should().NotThrow();
    }

    [Fact]
    public void Constructor_WhenSecretKeyIsTooShort_ThrowsInvalidOperationException()
    {
        // Arrange
        var settings = CreateSettings();
        var shortKeySettings = new JwtSettings
        {
            Issuer = settings.Issuer,
            Audience = settings.Audience,
            SecretKey = "too-short",
            AccessTokenMinutes = settings.AccessTokenMinutes
        };

        // Act
        var action = () => new JwtService(Options.Create(shortKeySettings));

        // Assert
        action.Should().Throw<InvalidOperationException>();
    }
}
