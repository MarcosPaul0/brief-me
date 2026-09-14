using BriefMe.Application.Abstractions;
using BriefMe.Application.Abstractions.Messaging;
using BriefMe.Application.Common.Dtos;
using BriefMe.Application.UseCases.Sla.ConfigureSlaRule;
using BriefMe.Domain.Common;
using BriefMe.Domain.Constants;
using BriefMe.Domain.Enums;
using BriefMe.Infrastructure.Auth;
using BriefMe.Web.Auth;
using BriefMe.Web.Contracts.Sla;
using BriefMe.Web.Controllers;
using BriefMe.Web.Extensions;
using FluentAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Moq;
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;

namespace BriefMe.Web.Tests.Controllers;

/// <summary>
/// Covers the authorization edge of BRF-042 (RF027): configuring SLA is the Administrator's act, and the
/// <c>sla.configure</c> permission is what the seed gives that profile alone.
/// </summary>
public class SlaRulesControllerTests
{
    private const string SecretKey = "web-tests-sla-rules-controller-secret-key-32b!";

    /// <summary>The Design Manager's seeded permission set (USR-047) — deliberately without sla.configure.</summary>
    private static readonly string[] DesignManagerPermissions =
    [
        Permissions.Briefings.Approve,
        Permissions.Briefings.Reject,
        Permissions.Tasks.Create,
        Permissions.Tasks.Assign
    ];

    private static JwtSettings CreateJwtSettings() => new()
    {
        Issuer = "BriefMe.Web.Tests",
        Audience = "BriefMe.Web.Tests.Audience",
        SecretKey = SecretKey,
        AccessTokenMinutes = 15
    };

    private static string GenerateToken(JwtSettings settings, Guid userId, string[]? roles = null, string[]? permissions = null)
    {
        var keyBytes = Encoding.UTF8.GetBytes(settings.SecretKey);
        var credentials = new SigningCredentials(new SymmetricSecurityKey(keyBytes), SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, userId.ToString()),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };
        claims.AddRange((roles ?? []).Select(role => new Claim(ClaimTypes.Role, role)));
        claims.AddRange((permissions ?? []).Select(permission => new Claim(CustomClaimTypes.Permission, permission)));

        var token = new JwtSecurityToken(
            issuer: settings.Issuer,
            audience: settings.Audience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(settings.AccessTokenMinutes),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private static async Task<IHost> StartHostAsync(ISender sender)
    {
        return await new HostBuilder()
            .ConfigureWebHost(webBuilder =>
            {
                webBuilder.UseTestServer();
                webBuilder.ConfigureServices(services =>
                {
                    services.AddRouting();
                    services.AddHttpContextAccessor();
                    services.AddControllers().AddApplicationPart(typeof(SlaRulesController).Assembly);
                    services.AddSingleton<IOptions<JwtSettings>>(Options.Create(CreateJwtSettings()));
                    services.AddJwtAuthentication();
                    services.AddSingleton(sender);
                    services.AddScoped<ICurrentUser, HttpContextCurrentUser>();
                });
                webBuilder.Configure(app =>
                {
                    app.UseRouting();
                    app.UseAuthentication();
                    app.UseAuthorization();
                    app.UseEndpoints(endpoints => endpoints.MapControllers());
                });
            })
            .StartAsync();
    }

    private static void Authenticate(HttpClient client, string token) =>
        client.DefaultRequestHeaders.Authorization = new("Bearer", token);

    [Fact]
    public async Task Configure_WhenCallerHasSlaConfigurePermission_ReturnsOkWithTheConfiguredRule()
    {
        // Arrange
        var dto = new SlaRuleDto(Guid.NewGuid(), null, null, 240, SlaRuleScope.Global);
        var senderMock = new Mock<ISender>();
        senderMock.Setup(s => s.Send(It.IsAny<ConfigureSlaRuleCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(dto));

        using var host = await StartHostAsync(senderMock.Object);
        var client = host.GetTestClient();
        Authenticate(client, GenerateToken(CreateJwtSettings(), Guid.NewGuid(), permissions: [Permissions.Configuration.SlaConfigure]));

        // Act
        var response = await client.PutAsJsonAsync("/sla-rules", new ConfigureSlaRuleRequest(null, null, 240));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<SlaRuleDto>();
        body!.DeadlineHours.Should().Be(240);
    }

    [Fact]
    public async Task Configure_WhenCallerIsDesignManagerWithoutSlaConfigure_ReturnsForbidden()
    {
        // Arrange
        var senderMock = new Mock<ISender>();

        using var host = await StartHostAsync(senderMock.Object);
        var client = host.GetTestClient();
        Authenticate(client, GenerateToken(
            CreateJwtSettings(),
            Guid.NewGuid(),
            roles: [Roles.GestorDeDesign],
            permissions: DesignManagerPermissions));

        // Act
        var response = await client.PutAsJsonAsync("/sla-rules", new ConfigureSlaRuleRequest(null, null, 240));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        senderMock.Verify(
            s => s.Send(It.IsAny<ConfigureSlaRuleCommand>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Configure_WhenRequestIsNotAuthenticated_ReturnsUnauthorized()
    {
        // Arrange
        var senderMock = new Mock<ISender>();

        using var host = await StartHostAsync(senderMock.Object);
        var client = host.GetTestClient();

        // Act
        var response = await client.PutAsJsonAsync("/sla-rules", new ConfigureSlaRuleRequest(null, null, 240));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        senderMock.Verify(
            s => s.Send(It.IsAny<ConfigureSlaRuleCommand>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Configure_WhenTheUseCaseFails_MapsTheResultToProblemDetails()
    {
        // Arrange
        var senderMock = new Mock<ISender>();
        senderMock.Setup(s => s.Send(It.IsAny<ConfigureSlaRuleCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.NotFound<SlaRuleDto>("Category not found or inactive."));

        using var host = await StartHostAsync(senderMock.Object);
        var client = host.GetTestClient();
        Authenticate(client, GenerateToken(CreateJwtSettings(), Guid.NewGuid(), permissions: [Permissions.Configuration.SlaConfigure]));

        // Act
        var response = await client.PutAsJsonAsync(
            "/sla-rules",
            new ConfigureSlaRuleRequest(Guid.NewGuid(), Priority.Urgent, 24));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
