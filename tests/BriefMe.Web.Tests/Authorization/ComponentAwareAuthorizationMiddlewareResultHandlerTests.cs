using System.Net;
using BriefMe.Infrastructure.Auth;
using BriefMe.Web.Extensions;
using FluentAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Components.Endpoints;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace BriefMe.Web.Tests.Authorization;

public class ComponentAwareAuthorizationMiddlewareResultHandlerTests
{
    private static JwtSettings CreateSettings() => new()
    {
        Issuer = "BriefMe.Web.Tests",
        Audience = "BriefMe.Web.Tests.Audience",
        SecretKey = "web-tests-jwt-secret-key-with-at-least-32-bytes!",
        AccessTokenMinutes = 15
    };

    private static async Task<IHost> StartHostAsync()
    {
        var host = await new HostBuilder()
            .ConfigureWebHost(webBuilder =>
            {
                webBuilder.UseTestServer();
                webBuilder.ConfigureServices(services =>
                {
                    services.AddRouting();
                    services.AddSingleton<IOptions<JwtSettings>>(Options.Create(CreateSettings()));
                    // Registers ComponentAwareAuthorizationMiddlewareResultHandler as production does (AuthenticationExtensions.AddJwtAuthentication).
                    services.AddJwtAuthentication();
                });
                webBuilder.Configure(app =>
                {
                    app.UseRouting();
                    app.UseAuthentication();
                    app.UseAuthorization();
                    app.UseEndpoints(endpoints =>
                    {
                        endpoints.MapGet("/component-route", () => Results.Ok())
                            .RequireAuthorization()
                            .WithMetadata(new ComponentTypeMetadata(typeof(object)));

                        endpoints.MapGet("/api-route", () => Results.Ok())
                            .RequireAuthorization();
                    });
                });
            })
            .StartAsync();

        return host;
    }

    [Fact]
    public async Task HandleAsync_WhenEndpointCarriesComponentTypeMetadataAndUserIsUnauthenticated_FallsThroughToEndpoint()
    {
        // Arrange
        using var host = await StartHostAsync();
        var client = host.GetTestClient();

        // Act
        var response = await client.GetAsync("/component-route");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task HandleAsync_WhenEndpointHasNoComponentTypeMetadataAndUserIsUnauthenticated_Returns401()
    {
        // Arrange
        using var host = await StartHostAsync();
        var client = host.GetTestClient();

        // Act
        var response = await client.GetAsync("/api-route");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
