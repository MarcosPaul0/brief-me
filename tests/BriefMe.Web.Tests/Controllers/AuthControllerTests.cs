using BriefMe.Application.Abstractions.Messaging;
using BriefMe.Application.Common.Dtos;
using BriefMe.Application.UseCases.Auth.Login;
using BriefMe.Application.UseCases.Auth.Logout;
using BriefMe.Application.UseCases.Auth.RefreshToken;
using BriefMe.Application.UseCases.Auth;
using BriefMe.Domain.Common;
using BriefMe.Domain.Entities;
using BriefMe.Domain.Enums;
using BriefMe.Domain.ValueObjects;
using BriefMe.Infrastructure.Auth;
using BriefMe.Web.Contracts.Auth;
using BriefMe.Web.Controllers;
using BriefMe.Web.Extensions;
using FluentAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Moq;
using System.Net.Http.Json;
using System.Net;

namespace BriefMe.Web.Tests.Controllers;

public class AuthControllerTests
{
    private const string SecretKey = "web-tests-auth-controller-secret-key-32b!!";

    private static JwtSettings CreateJwtSettings() => new()
    {
        Issuer = "BriefMe.Web.Tests",
        Audience = "BriefMe.Web.Tests.Audience",
        SecretKey = SecretKey,
        AccessTokenMinutes = 15
    };

    private static User CreateUser()
    {
        var user = User.Create("Ana Silva", Email.Create("ana@briefme.com").Value, "hashed-password", UserRole.Designer);

        return user;
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
                    services.AddControllers().AddApplicationPart(typeof(AuthController).Assembly);
                    services.AddSingleton<IOptions<JwtSettings>>(Options.Create(CreateJwtSettings()));
                    services.AddJwtAuthentication();
                    services.AddSingleton(sender);
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

    [Fact]
    public async Task Login_WhenCredentialsAreValid_ReturnsOkWithTokens()
    {
        // Arrange
        var user = CreateUser();
        var authenticationResult = AuthenticationDto.FromUserAndToken(user, "access-token", DateTime.UtcNow.AddMinutes(15), "refresh-token");
        var senderMock = new Mock<ISender>();
        senderMock.Setup(s => s.Send(It.IsAny<LoginCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(authenticationResult));

        using var host = await StartHostAsync(senderMock.Object);
        var client = host.GetTestClient();

        // Act
        var response = await client.PostAsJsonAsync("/auth/login", new LoginRequest(user.Email.Value, "correct-password"));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<AuthenticationDto>();
        body!.AccessToken.Should().Be("access-token");
        body.RefreshToken.Should().Be("refresh-token");
    }

    [Fact]
    public async Task Login_WhenCredentialsAreInvalid_ReturnsUnauthorizedProblemWithGenericDetail()
    {
        // Arrange
        var senderMock = new Mock<ISender>();
        senderMock.Setup(s => s.Send(It.IsAny<LoginCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Unauthorized<AuthenticationDto>("E-mail ou senha inválidos."));

        using var host = await StartHostAsync(senderMock.Object);
        var client = host.GetTestClient();

        // Act
        var response = await client.PostAsJsonAsync("/auth/login", new LoginRequest("ana@briefme.com", "wrong-password"));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        problem!.Detail.Should().Be("E-mail ou senha inválidos.");
    }

    [Fact]
    public async Task Refresh_WhenTokenIsValid_ReturnsOkWithNewTokens()
    {
        // Arrange
        var user = CreateUser();
        var authenticationResult = AuthenticationDto.FromUserAndToken(user, "new-access-token", DateTime.UtcNow.AddMinutes(15), "new-refresh-token");
        var senderMock = new Mock<ISender>();
        senderMock.Setup(s => s.Send(It.IsAny<RefreshTokenCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(authenticationResult));

        using var host = await StartHostAsync(senderMock.Object);
        var client = host.GetTestClient();

        // Act
        var response = await client.PostAsJsonAsync("/auth/refresh", new RefreshTokenRequest("old-refresh-token"));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<AuthenticationDto>();
        body!.AccessToken.Should().Be("new-access-token");
    }

    [Fact]
    public async Task Refresh_WhenTokenIsInvalidOrExpired_ReturnsUnauthorizedProblem()
    {
        // Arrange
        var senderMock = new Mock<ISender>();
        senderMock.Setup(s => s.Send(It.IsAny<RefreshTokenCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Unauthorized<AuthenticationDto>("Refresh token inválido ou expirado."));

        using var host = await StartHostAsync(senderMock.Object);
        var client = host.GetTestClient();

        // Act
        var response = await client.PostAsJsonAsync("/auth/refresh", new RefreshTokenRequest("revoked-token"));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        problem!.Detail.Should().Be("Refresh token inválido ou expirado.");
    }

    [Fact]
    public async Task Logout_WhenNoAccessTokenProvided_ReturnsUnauthorized()
    {
        // Arrange
        var senderMock = new Mock<ISender>();
        using var host = await StartHostAsync(senderMock.Object);
        var client = host.GetTestClient();

        // Act
        var response = await client.PostAsJsonAsync("/auth/logout", new LogoutRequest("some-refresh-token"));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        senderMock.Verify(s => s.Send(It.IsAny<LogoutCommand>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Logout_WhenAuthenticatedAndRevocationSucceeds_ReturnsNoContent()
    {
        // Arrange
        var senderMock = new Mock<ISender>();
        senderMock.Setup(s => s.Send(It.IsAny<LogoutCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success());

        using var host = await StartHostAsync(senderMock.Object);
        var client = host.GetTestClient();
        var jwtService = new JwtService(Options.Create(CreateJwtSettings()));
        var (token, _) = jwtService.GenerateAccessToken(CreateUser());
        client.DefaultRequestHeaders.Authorization = new("Bearer", token);

        // Act
        var response = await client.PostAsJsonAsync("/auth/logout", new LogoutRequest("some-refresh-token"));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Logout_WhenAuthenticatedAndRevocationFails_ReturnsUnauthorizedProblem()
    {
        // Arrange
        var senderMock = new Mock<ISender>();
        senderMock.Setup(s => s.Send(It.IsAny<LogoutCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Unauthorized("Usuário não autenticado."));

        using var host = await StartHostAsync(senderMock.Object);
        var client = host.GetTestClient();
        var jwtService = new JwtService(Options.Create(CreateJwtSettings()));
        var (token, _) = jwtService.GenerateAccessToken(CreateUser());
        client.DefaultRequestHeaders.Authorization = new("Bearer", token);

        // Act
        var response = await client.PostAsJsonAsync("/auth/logout", new LogoutRequest(AllDevices: true));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        problem!.Detail.Should().Be("Usuário não autenticado.");
    }
}
