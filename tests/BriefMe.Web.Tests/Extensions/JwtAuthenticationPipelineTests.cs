using System.Net;
using System.Net.Http.Json;
using BriefMe.Domain.Entities;
using BriefMe.Domain.ValueObjects;
using BriefMe.Infrastructure.Auth;
using BriefMe.Web.Extensions;
using FluentAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace BriefMe.Web.Tests.Extensions;

public class JwtAuthenticationPipelineTests
{
    private const string SecretKey = "web-tests-jwt-secret-key-with-at-least-32-bytes!";

    private static JwtSettings CreateSettings(int accessTokenMinutes = 15) => new()
    {
        Issuer = "BriefMe.Web.Tests",
        Audience = "BriefMe.Web.Tests.Audience",
        SecretKey = SecretKey,
        AccessTokenMinutes = accessTokenMinutes
    };

    private static User CreateUser(string roleName)
    {
        var user = User.Create("Ana Silva", Email.Create("ana@briefme.com").Value, "hashed-password");
        var role = Role.Create(roleName, null);
        user.AssignRole(role.Id);

        return user;
    }

    private static async Task<IHost> StartHostAsync(JwtSettings jwtSettings, string requiredRole = "Administrador")
    {
        var host = await new HostBuilder()
            .ConfigureWebHost(webBuilder =>
            {
                webBuilder.UseTestServer();
                webBuilder.ConfigureServices(services =>
                {
                    services.AddRouting();
                    services.AddSingleton<IOptions<JwtSettings>>(Options.Create(jwtSettings));
                    services.AddJwtAuthentication();
                });
                webBuilder.Configure(app =>
                {
                    app.UseRouting();
                    app.UseAuthentication();
                    app.UseAuthorization();
                    app.UseEndpoints(endpoints =>
                    {
                        endpoints.MapGet("/secure", () => Results.Ok()).RequireAuthorization();
                        endpoints.MapGet("/admin", () => Results.Ok())
                            .RequireAuthorization(policy => policy.RequireRole(requiredRole));
                    });
                });
            })
            .StartAsync();

        return host;
    }

    [Fact]
    public async Task Secure_WhenNoTokenProvided_Returns401WithProblemDetails()
    {
        // Arrange
        using var host = await StartHostAsync(CreateSettings());
        var client = host.GetTestClient();

        // Act
        var response = await client.GetAsync("/secure");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        problem!.Status.Should().Be((int)HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Secure_WhenTokenIsTampered_Returns401WithProblemDetails()
    {
        // Arrange
        var settings = CreateSettings();
        using var host = await StartHostAsync(settings);
        var client = host.GetTestClient();
        var jwtService = new JwtService(Options.Create(settings));
        var (token, _) = jwtService.GenerateAccessToken(CreateUser("Designer"));
        client.DefaultRequestHeaders.Authorization = new("Bearer", token[..^2] + "xx");

        // Act
        var response = await client.GetAsync("/secure");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        problem!.Detail.Should().Be("Token de acesso inválido.");
    }

    [Fact]
    public async Task Secure_WhenTokenIsExpired_Returns401WithExpiredDetail()
    {
        // Arrange
        var settings = CreateSettings(accessTokenMinutes: -5);
        using var host = await StartHostAsync(settings);
        var client = host.GetTestClient();
        var jwtService = new JwtService(Options.Create(settings));
        var (token, _) = jwtService.GenerateAccessToken(CreateUser("Designer"));
        client.DefaultRequestHeaders.Authorization = new("Bearer", token);

        // Act
        var response = await client.GetAsync("/secure");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        problem!.Detail.Should().Be("Token de acesso expirado.");
    }

    [Fact]
    public async Task Secure_WhenTokenIsValid_ReturnsOk()
    {
        // Arrange
        var settings = CreateSettings();
        using var host = await StartHostAsync(settings);
        var client = host.GetTestClient();
        var jwtService = new JwtService(Options.Create(settings));
        var (token, _) = jwtService.GenerateAccessToken(CreateUser("Designer"));
        client.DefaultRequestHeaders.Authorization = new("Bearer", token);

        // Act
        var response = await client.GetAsync("/secure");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Admin_WhenValidTokenLacksRequiredRole_Returns403WithProblemDetails()
    {
        // Arrange
        var settings = CreateSettings();
        using var host = await StartHostAsync(settings, requiredRole: "Administrador");
        var client = host.GetTestClient();
        var jwtService = new JwtService(Options.Create(settings));
        var (token, _) = jwtService.GenerateAccessToken(CreateUser("Designer"));
        client.DefaultRequestHeaders.Authorization = new("Bearer", token);

        // Act
        var response = await client.GetAsync("/admin");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        problem!.Status.Should().Be((int)HttpStatusCode.Forbidden);
    }
}
