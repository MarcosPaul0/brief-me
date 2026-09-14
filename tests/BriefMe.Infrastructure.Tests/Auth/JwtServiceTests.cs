using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using BriefMe.Domain.Entities;
using BriefMe.Domain.ValueObjects;
using BriefMe.Infrastructure.Auth;
using BriefMe.Infrastructure.Persistence;
using BriefMe.Infrastructure.Persistence.Repositories;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
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

    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new AppDbContext(options);
    }

    private static User CreateUser(string fullName, string email) =>
        User.Create(fullName, Email.Create(email).Value, "hashed-password");

    [Fact]
    public void GenerateAccessToken_WhenUserHasNoRoles_ReturnsTokenWithIdentityClaimsAndNoRoleOrPermissionClaims()
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
        jwt.Claims.Should().NotContain(c => c.Type == ClaimTypes.Role);
        jwt.Claims.Should().NotContain(c => c.Type == CustomClaimTypes.Permission);
        expiresAt.Should().BeCloseTo(DateTime.UtcNow.AddMinutes(15), TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task GenerateAccessToken_WhenUserHasRolesWithPermissions_IncludesRoleAndDistinctPermissionClaims()
    {
        // Arrange
        await using var context = CreateContext();
        var createPermission = Permission.Create("users.create", "Criar usuários", null, "Usuários");
        var updatePermission = Permission.Create("users.update", "Editar usuários", null, "Usuários");

        var designerRole = Role.Create("Designer", null);
        designerRole.AddPermission(createPermission.Id);

        var managerRole = Role.Create("Gestor de Design", null);
        managerRole.AddPermission(createPermission.Id);
        managerRole.AddPermission(updatePermission.Id);

        var user = CreateUser("Ana Silva", "ana@briefme.com");
        user.AssignRole(designerRole.Id);
        user.AssignRole(managerRole.Id);

        await context.Permissions.AddRangeAsync(createPermission, updatePermission);
        await context.Roles.AddRangeAsync(designerRole, managerRole);
        await context.Users.AddAsync(user);
        await context.SaveChangesAsync();

        var hydratedUser = await new UserRepository(context).GetByEmailAsync(Email.Create("ana@briefme.com").Value);
        var service = new JwtService(Options.Create(CreateSettings()));

        // Act
        var (token, _) = service.GenerateAccessToken(hydratedUser!);

        // Assert
        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);
        jwt.Claims.Where(c => c.Type == ClaimTypes.Role).Select(c => c.Value).Should().BeEquivalentTo("Designer", "Gestor de Design");
        jwt.Claims.Where(c => c.Type == CustomClaimTypes.Permission).Select(c => c.Value).Should().BeEquivalentTo("users.create", "users.update");
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
