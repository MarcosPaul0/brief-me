using BriefMe.Application.Abstractions;
using BriefMe.Application.Abstractions.Messaging;
using BriefMe.Application.Common.Dtos;
using BriefMe.Application.Common.Errors;
using BriefMe.Application.UseCases.Notifications.GetMyNotifications;
using BriefMe.Application.UseCases.Notifications.GetUnreadNotificationCount;
using BriefMe.Application.UseCases.Notifications.MarkNotificationsAsRead;
using BriefMe.Domain.Common;
using BriefMe.Domain.Enums;
using BriefMe.Infrastructure.Auth;
using BriefMe.Web.Auth;
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
/// Covers the authorization edge of BRF-054 (RF031–RF035): the inbox is the caller's own rows, so holding an
/// account is the whole condition — a user with <b>no</b> permission at all reads their notifications, and
/// an anonymous caller reads nothing.
/// </summary>
public class NotificationsControllerTests
{
    private const string SecretKey = "web-tests-notifications-controller-secret-key!";

    private static JwtSettings CreateJwtSettings() => new()
    {
        Issuer = "BriefMe.Web.Tests",
        Audience = "BriefMe.Web.Tests.Audience",
        SecretKey = SecretKey,
        AccessTokenMinutes = 15
    };

    private static string GenerateToken(JwtSettings settings, Guid userId)
    {
        var keyBytes = Encoding.UTF8.GetBytes(settings.SecretKey);
        var credentials = new SigningCredentials(new SymmetricSecurityKey(keyBytes), SecurityAlgorithms.HmacSha256);

        // No role and no permission claim: the point of these tests is that neither is required.
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, userId.ToString()),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

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
                    services.AddControllers().AddApplicationPart(typeof(NotificationsController).Assembly);
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

    private static NotificationDto NewDto() => new(
        Guid.NewGuid(),
        NotificationEventType.BriefingApproved,
        "Briefing aprovado",
        "Seu briefing \"Campanha de Natal\" foi aprovado.",
        new DateTime(2026, 9, 5, 12, 0, 0, DateTimeKind.Utc),
        null);

    [Fact]
    public async Task GetMine_WhenCallerIsAuthenticatedWithoutAnyPermission_ReturnsTheirNotifications()
    {
        // Arrange
        var page = PagedDto<NotificationDto>.Create([NewDto()], 1, 10, 1);
        var senderMock = new Mock<ISender>();
        senderMock.Setup(s => s.Send(It.IsAny<GetMyNotificationsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(page));

        using var host = await StartHostAsync(senderMock.Object);
        var client = host.GetTestClient();
        Authenticate(client, GenerateToken(CreateJwtSettings(), Guid.NewGuid()));

        // Act
        var response = await client.GetAsync("/notifications?page=1&pageSize=10");

        // Assert
        // An inbox is not permission-gated: the identity that reads it is the identity that scopes it.
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetMine_WhenCallerIsAnonymous_ReturnsUnauthorized()
    {
        // Arrange
        var senderMock = new Mock<ISender>();

        using var host = await StartHostAsync(senderMock.Object);
        var client = host.GetTestClient();

        // Act
        var response = await client.GetAsync("/notifications");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetMine_WhenPagingIsAbsent_UsesTheBellDefaults()
    {
        // Arrange
        GetMyNotificationsQuery? captured = null;
        var senderMock = new Mock<ISender>();
        senderMock.Setup(s => s.Send(It.IsAny<GetMyNotificationsQuery>(), It.IsAny<CancellationToken>()))
            .Callback<IRequest<Result<PagedDto<NotificationDto>>>, CancellationToken>((request, _) => captured = (GetMyNotificationsQuery)request)
            .ReturnsAsync(Result.Success(PagedDto<NotificationDto>.Create([], 1, 10, 0)));

        using var host = await StartHostAsync(senderMock.Object);
        var client = host.GetTestClient();
        Authenticate(client, GenerateToken(CreateJwtSettings(), Guid.NewGuid()));

        // Act
        await client.GetAsync("/notifications");

        // Assert
        captured!.Page.Should().Be(1);
        captured.PageSize.Should().Be(10);
    }

    [Fact]
    public async Task GetUnreadCount_WhenCallerIsAuthenticated_ReturnsTheCount()
    {
        // Arrange
        var senderMock = new Mock<ISender>();
        senderMock.Setup(s => s.Send(It.IsAny<GetUnreadNotificationCountQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(new UnreadNotificationCountDto(3)));

        using var host = await StartHostAsync(senderMock.Object);
        var client = host.GetTestClient();
        Authenticate(client, GenerateToken(CreateJwtSettings(), Guid.NewGuid()));

        // Act
        var response = await client.GetAsync("/notifications/unread-count");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<UnreadNotificationCountDto>();
        body!.Count.Should().Be(3);
    }

    [Fact]
    public async Task GetUnreadCount_WhenCallerIsAnonymous_ReturnsUnauthorized()
    {
        // Arrange
        var senderMock = new Mock<ISender>();

        using var host = await StartHostAsync(senderMock.Object);
        var client = host.GetTestClient();

        // Act
        var response = await client.GetAsync("/notifications/unread-count");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task MarkAllAsRead_WhenCallerIsAuthenticated_ReturnsNoContent()
    {
        // Arrange
        var senderMock = new Mock<ISender>();
        senderMock.Setup(s => s.Send(It.IsAny<MarkNotificationsAsReadCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success());

        using var host = await StartHostAsync(senderMock.Object);
        var client = host.GetTestClient();
        Authenticate(client, GenerateToken(CreateJwtSettings(), Guid.NewGuid()));

        // Act
        var response = await client.PostAsync("/notifications/read", null);

        // Assert
        // Nothing to hand back: the caller that asked for everything to be read knows the count is now zero.
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task MarkAllAsRead_WhenCallerIsAnonymous_ReturnsUnauthorized()
    {
        // Arrange
        var senderMock = new Mock<ISender>();

        using var host = await StartHostAsync(senderMock.Object);
        var client = host.GetTestClient();

        // Act
        var response = await client.PostAsync("/notifications/read", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task MarkAllAsRead_WhenTheHandlerRejectsTheIdentity_ReturnsUnauthorized()
    {
        // Arrange
        var senderMock = new Mock<ISender>();
        senderMock.Setup(s => s.Send(It.IsAny<MarkNotificationsAsReadCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Unauthorized(CommonErrors.UserNotAuthenticated));

        using var host = await StartHostAsync(senderMock.Object);
        var client = host.GetTestClient();
        Authenticate(client, GenerateToken(CreateJwtSettings(), Guid.NewGuid()));

        // Act
        var response = await client.PostAsync("/notifications/read", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
