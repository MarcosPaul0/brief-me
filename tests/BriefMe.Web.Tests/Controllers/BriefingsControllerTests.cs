using BriefMe.Application.Abstractions.Messaging;
using BriefMe.Application.Abstractions;
using BriefMe.Application.Common.Dtos;
using BriefMe.Application.UseCases.Briefings.AcceptBriefingDelivery;
using BriefMe.Application.UseCases.Briefings.CancelBriefing;
using BriefMe.Application.UseCases.Briefings.CreateBriefing;
using BriefMe.Application.UseCases.Briefings.EditBriefing;
using BriefMe.Application.UseCases.Briefings.GetBriefingById;
using BriefMe.Application.UseCases.Briefings.GetBriefingsByRequester;
using BriefMe.Application.UseCases.Briefings.RequestBriefingChanges;
using BriefMe.Application.UseCases.Briefings.SubmitBriefing;
using BriefMe.Application.UseCases.Briefings;
using BriefMe.Domain.Common;
using BriefMe.Domain.Enums;
using BriefMe.Infrastructure.Auth;
using BriefMe.Web.Auth;
using BriefMe.Web.Contracts.Briefings;
using BriefMe.Web.Controllers;
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
using Microsoft.IdentityModel.Tokens;
using Moq;
using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Json;
using System.Net;
using System.Security.Claims;
using System.Text;

namespace BriefMe.Web.Tests.Controllers;

public class BriefingsControllerTests
{
    private const string SecretKey = "web-tests-briefings-controller-secret-key-32b!";

    private static JwtSettings CreateJwtSettings() => new()
    {
        Issuer = "BriefMe.Web.Tests",
        Audience = "BriefMe.Web.Tests.Audience",
        SecretKey = SecretKey,
        AccessTokenMinutes = 15
    };

    // Mirrors UsersControllerTests: builds a token straight from claims, since these tests target the
    // Web-layer authorization pipeline (permission policies), not JwtService's claim population.
    private static string GenerateToken(JwtSettings settings, Guid userId, string[]? roles = null)
    {
        var keyBytes = Encoding.UTF8.GetBytes(settings.SecretKey);
        var credentials = new SigningCredentials(new SymmetricSecurityKey(keyBytes), SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, userId.ToString()),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };
        claims.AddRange((roles ?? []).Select(role => new Claim(ClaimTypes.Role, role)));

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
                    services.AddControllers().AddApplicationPart(typeof(BriefingsController).Assembly);
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

    private static BriefingDto SampleDto(Guid id, BriefingStatus status = BriefingStatus.Draft) => new(
        Id: id,
        Title: "Campanha de lançamento",
        Description: null,
        Objective: null,
        RequestingArea: null,
        DesiredDeliveryDate: null,
        Priority: Priority.Medium,
        CategoryId: Guid.NewGuid(),
        RequesterId: Guid.NewGuid(),
        DesignerId: null,
        Status: status,
        DeliveryNote: null,
        DeliveredAt: null,
        ChangeRequestReason: null);

    private static BriefingDetailDto SampleDetailDto(Guid id) => new(
        Id: id,
        Title: "Campanha de lançamento",
        Description: null,
        Objective: null,
        RequestingArea: null,
        DesiredDeliveryDate: null,
        Priority: Priority.Medium,
        CategoryId: Guid.NewGuid(),
        CategoryName: "Evento",
        RequesterId: Guid.NewGuid(),
        RequesterName: "Rita",
        DesignerId: null,
        DesignerName: null,
        Status: BriefingStatus.Submitted,
        DeliveryNote: null,
        DeliveredAt: null,
        ChangeRequestReason: null,
        CreatedAt: DateTime.UtcNow);

    [Fact]
    public async Task Create_WhenCallerIsRequester_ReturnsCreatedWithLocation()
    {
        // Arrange
        var createdId = Guid.NewGuid();
        var senderMock = new Mock<ISender>();
        senderMock.Setup(s => s.Send(It.IsAny<CreateBriefingCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(SampleDto(createdId)));

        using var host = await StartHostAsync(senderMock.Object);
        var client = host.GetTestClient();
        Authenticate(client, GenerateToken(CreateJwtSettings(), Guid.NewGuid(), roles: [nameof(UserRole.Requester)]));

        // Act
        var response = await client.PostAsJsonAsync(
            "/briefings",
            new CreateBriefingRequest("Campanha de lançamento", Priority.Medium, Guid.NewGuid()));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        response.Headers.Location!.ToString().Should().Contain($"/briefings/{createdId}");
        var body = await response.Content.ReadFromJsonAsync<BriefingDto>();
        body!.Id.Should().Be(createdId);
    }

    [Fact]
    public async Task Create_WhenCallerIsNotRequester_ReturnsForbidden()
    {
        // Arrange
        var senderMock = new Mock<ISender>();

        using var host = await StartHostAsync(senderMock.Object);
        var client = host.GetTestClient();
        Authenticate(client, GenerateToken(CreateJwtSettings(), Guid.NewGuid(), roles: [nameof(UserRole.Designer)]));

        // Act
        var response = await client.PostAsJsonAsync(
            "/briefings",
            new CreateBriefingRequest("Campanha de lançamento", Priority.Medium, Guid.NewGuid()));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        senderMock.Verify(s => s.Send(It.IsAny<CreateBriefingCommand>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Create_WhenCategoryDoesNotExist_ReturnsNotFoundProblem()
    {
        // Arrange
        var senderMock = new Mock<ISender>();
        senderMock.Setup(s => s.Send(It.IsAny<CreateBriefingCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.NotFound<BriefingDto>("Categoria não encontrada."));

        using var host = await StartHostAsync(senderMock.Object);
        var client = host.GetTestClient();
        Authenticate(client, GenerateToken(CreateJwtSettings(), Guid.NewGuid(), roles: [nameof(UserRole.Requester)]));

        // Act
        var response = await client.PostAsJsonAsync(
            "/briefings",
            new CreateBriefingRequest("Campanha de lançamento", Priority.Medium, Guid.NewGuid()));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        problem!.Detail.Should().Be("Categoria não encontrada.");
    }

    [Fact]
    public async Task Update_WhenBriefingNotEditable_ReturnsConflictProblem()
    {
        // Arrange
        var senderMock = new Mock<ISender>();
        senderMock.Setup(s => s.Send(It.IsAny<EditBriefingCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Conflict<BriefingDto>("Briefing não pode ser editado no status atual."));

        using var host = await StartHostAsync(senderMock.Object);
        var client = host.GetTestClient();
        Authenticate(client, GenerateToken(CreateJwtSettings(), Guid.NewGuid(), roles: [nameof(UserRole.Requester)]));

        // Act
        var response = await client.PutAsJsonAsync(
            $"/briefings/{Guid.NewGuid()}",
            new UpdateBriefingRequest("Título editado", Priority.High));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        problem!.Detail.Should().Be("Briefing não pode ser editado no status atual.");
    }

    [Fact]
    public async Task Submit_WhenCallerIsRequester_ReturnsOkWithBriefingDto()
    {
        // Arrange
        var briefingId = Guid.NewGuid();
        var senderMock = new Mock<ISender>();
        senderMock.Setup(s => s.Send(It.IsAny<SubmitBriefingCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(SampleDto(briefingId) with { Status = BriefingStatus.Submitted }));

        using var host = await StartHostAsync(senderMock.Object);
        var client = host.GetTestClient();
        Authenticate(client, GenerateToken(CreateJwtSettings(), Guid.NewGuid(), roles: [nameof(UserRole.Requester)]));

        // Act
        var response = await client.PostAsync($"/briefings/{briefingId}/submit", content: null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<BriefingDto>();
        body!.Status.Should().Be(BriefingStatus.Submitted);
    }

    [Fact]
    public async Task Cancel_WhenCallerIsNotRequester_ReturnsForbidden()
    {
        // Arrange
        var senderMock = new Mock<ISender>();

        using var host = await StartHostAsync(senderMock.Object);
        var client = host.GetTestClient();
        Authenticate(client, GenerateToken(CreateJwtSettings(), Guid.NewGuid(), roles: [nameof(UserRole.Designer)]));

        // Act
        var response = await client.PostAsync($"/briefings/{Guid.NewGuid()}/cancel", content: null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        senderMock.Verify(s => s.Send(It.IsAny<CancelBriefingCommand>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GetById_WhenCallerIsRequester_ReturnsOkWithBriefingDetailDto()
    {
        // Arrange
        var briefingId = Guid.NewGuid();
        var senderMock = new Mock<ISender>();
        senderMock.Setup(s => s.Send(It.IsAny<GetBriefingByIdQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(SampleDetailDto(briefingId)));

        using var host = await StartHostAsync(senderMock.Object);
        var client = host.GetTestClient();
        Authenticate(client, GenerateToken(CreateJwtSettings(), Guid.NewGuid(), roles: [nameof(UserRole.Requester)]));

        // Act
        var response = await client.GetAsync($"/briefings/{briefingId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<BriefingDetailDto>();
        body!.Id.Should().Be(briefingId);
    }

    [Fact]
    public async Task GetById_WhenBriefingOwnedByAnotherUser_ReturnsForbiddenProblem()
    {
        // Arrange
        var senderMock = new Mock<ISender>();
        senderMock.Setup(s => s.Send(It.IsAny<GetBriefingByIdQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Forbidden<BriefingDetailDto>("Somente o solicitante dono pode visualizar."));

        using var host = await StartHostAsync(senderMock.Object);
        var client = host.GetTestClient();
        Authenticate(client, GenerateToken(CreateJwtSettings(), Guid.NewGuid(), roles: [nameof(UserRole.Requester)]));

        // Act
        var response = await client.GetAsync($"/briefings/{Guid.NewGuid()}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task GetById_WhenCallerIsAdmin_ReturnsForbidden()
    {
        // Arrange
        var senderMock = new Mock<ISender>();

        using var host = await StartHostAsync(senderMock.Object);
        var client = host.GetTestClient();
        Authenticate(client, GenerateToken(CreateJwtSettings(), Guid.NewGuid(), roles: [nameof(UserRole.Admin)]));

        // Act
        var response = await client.GetAsync($"/briefings/{Guid.NewGuid()}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        senderMock.Verify(s => s.Send(It.IsAny<GetBriefingByIdQuery>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GetMyBriefings_WhenCallerIsRequester_MapsQueryParamsAndReturnsOk()
    {
        // Arrange
        GetBriefingsByRequesterQuery? capturedQuery = null;
        var categoryId = Guid.NewGuid();
        var pagedResult = PagedDto<BriefingListItemDto>.Create([], page: 2, pageSize: 5, totalCount: 0);
        var senderMock = new Mock<ISender>();
        senderMock.Setup(s => s.Send(It.IsAny<GetBriefingsByRequesterQuery>(), It.IsAny<CancellationToken>()))
            .Callback<IRequest<Result<PagedDto<BriefingListItemDto>>>, CancellationToken>((q, _) => capturedQuery = (GetBriefingsByRequesterQuery)q)
            .ReturnsAsync(Result.Success(pagedResult));

        using var host = await StartHostAsync(senderMock.Object);
        var client = host.GetTestClient();
        Authenticate(client, GenerateToken(CreateJwtSettings(), Guid.NewGuid(), roles: [nameof(UserRole.Requester)]));

        // Act
        var response = await client.GetAsync($"/briefings?page=2&pageSize=5&status=Draft&categoryId={categoryId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        capturedQuery.Should().NotBeNull();
        capturedQuery!.Page.Should().Be(2);
        capturedQuery.PageSize.Should().Be(5);
        capturedQuery.Status.Should().Be(BriefingStatus.Draft);
        capturedQuery.CategoryId.Should().Be(categoryId);
    }

    [Fact]
    public async Task GetMyBriefings_WhenCallerIsNotRequester_ReturnsForbidden()
    {
        // Arrange
        var senderMock = new Mock<ISender>();

        using var host = await StartHostAsync(senderMock.Object);
        var client = host.GetTestClient();
        Authenticate(client, GenerateToken(CreateJwtSettings(), Guid.NewGuid(), roles: [nameof(UserRole.Designer)]));

        // Act
        var response = await client.GetAsync("/briefings");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        senderMock.Verify(s => s.Send(It.IsAny<GetBriefingsByRequesterQuery>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GetById_WhenCallerIsDesigner_ReachesTheHandler()
    {
        // Arrange
        var briefingId = Guid.NewGuid();
        var senderMock = new Mock<ISender>();
        senderMock.Setup(s => s.Send(It.IsAny<GetBriefingByIdQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(SampleDetailDto(briefingId)));

        using var host = await StartHostAsync(senderMock.Object);
        var client = host.GetTestClient();
        Authenticate(client, GenerateToken(CreateJwtSettings(), Guid.NewGuid(), roles: [nameof(UserRole.Designer)]));

        // Act
        var response = await client.GetAsync($"/briefings/{briefingId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Accept_WhenCallerIsRequester_ReturnsOkWithCompletedBriefing()
    {
        // Arrange
        var briefingId = Guid.NewGuid();
        var senderMock = new Mock<ISender>();
        senderMock.Setup(s => s.Send(It.Is<AcceptBriefingDeliveryCommand>(c => c.BriefingId == briefingId), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(SampleDto(briefingId, BriefingStatus.Completed)));

        using var host = await StartHostAsync(senderMock.Object);
        var client = host.GetTestClient();
        Authenticate(client, GenerateToken(CreateJwtSettings(), Guid.NewGuid(), roles: [nameof(UserRole.Requester)]));

        // Act
        var response = await client.PostAsync($"/briefings/{briefingId}/accept", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Accept_WhenCallerIsDesigner_ReturnsForbidden()
    {
        // Arrange
        var senderMock = new Mock<ISender>();

        using var host = await StartHostAsync(senderMock.Object);
        var client = host.GetTestClient();
        Authenticate(client, GenerateToken(CreateJwtSettings(), Guid.NewGuid(), roles: [nameof(UserRole.Designer)]));

        // Act
        var response = await client.PostAsync($"/briefings/{Guid.NewGuid()}/accept", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task RequestChanges_WhenCallerIsRequester_ForwardsTheReason()
    {
        // Arrange
        var briefingId = Guid.NewGuid();
        var senderMock = new Mock<ISender>();
        senderMock.Setup(s => s.Send(It.IsAny<RequestBriefingChangesCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(SampleDto(briefingId, BriefingStatus.InProgress)));

        using var host = await StartHostAsync(senderMock.Object);
        var client = host.GetTestClient();
        Authenticate(client, GenerateToken(CreateJwtSettings(), Guid.NewGuid(), roles: [nameof(UserRole.Requester)]));

        // Act
        await client.PostAsJsonAsync($"/briefings/{briefingId}/request-changes", new RequestBriefingChangesRequest("Use the new logo"));

        // Assert
        senderMock.Verify(s => s.Send(
            It.Is<RequestBriefingChangesCommand>(c => c.BriefingId == briefingId && c.Reason == "Use the new logo"),
            It.IsAny<CancellationToken>()), Times.Once);
    }
}
