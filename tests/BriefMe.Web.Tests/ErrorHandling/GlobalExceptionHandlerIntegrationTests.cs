using System.Net;
using System.Net.Http.Json;
using BriefMe.Application.Abstractions.Messaging;
using BriefMe.Application.UseCases.Auth.Login;
using BriefMe.Web.Contracts.Auth;
using BriefMe.Web.Controllers;
using BriefMe.Web.ErrorHandling;
using FluentAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Moq;

namespace BriefMe.Web.Tests.ErrorHandling;

public class GlobalExceptionHandlerIntegrationTests
{
    private static async Task<IHost> StartHostAsync(ISender sender)
    {
        return await new HostBuilder()
            .ConfigureWebHost(webBuilder =>
            {
                webBuilder.UseTestServer();
                webBuilder.ConfigureServices(services =>
                {
                    services.AddRouting();
                    services.AddControllers().AddApplicationPart(typeof(AuthController).Assembly);
                    services.AddSingleton(sender);
                    services.AddExceptionHandler<GlobalExceptionHandler>();
                    services.AddProblemDetails();
                });
                webBuilder.Configure(app =>
                {
                    app.UseExceptionHandler();
                    app.UseRouting();
                    app.UseEndpoints(endpoints =>
                    {
                        endpoints.MapControllers();
                        endpoints.Map("/non-api", (Func<Task>)(() => throw new InvalidOperationException("boom")));
                    });
                });
            })
            .StartAsync();
    }

    [Fact]
    public async Task Request_WhenApiControllerActionThrows_Returns500ProblemDetailsWithTraceId()
    {
        // Arrange
        var senderMock = new Mock<ISender>();
        senderMock.Setup(s => s.Send(It.IsAny<LoginCommand>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("unexpected failure"));

        using var host = await StartHostAsync(senderMock.Object);
        var client = host.GetTestClient();

        // Act
        var response = await client.PostAsJsonAsync("/auth/login", new LoginRequest("ana@briefme.com", "senha"));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        problem!.Extensions.Should().ContainKey("traceId");
    }

    [Fact]
    public async Task Request_WhenNonApiEndpointThrows_IsNotHandledByGlobalExceptionHandler()
    {
        // Arrange — no [ApiController] metadata on "/non-api", so GlobalExceptionHandler.TryHandleAsync
        // must return false; ASP.NET Core's built-in ProblemDetails fallback (from AddProblemDetails())
        // still produces a generic 500 (with its own generic title), proving GlobalExceptionHandler's
        // handler-specific "Erro interno do servidor." title never gets written for this route.
        using var host = await StartHostAsync(new Mock<ISender>().Object);
        var client = host.GetTestClient();

        // Act
        var response = await client.GetAsync("/non-api");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        problem!.Title.Should().NotBe("Erro interno do servidor.");
    }
}
