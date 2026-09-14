using BriefMe.Application.Abstractions.Messaging;
using BriefMe.Application.Abstractions;
using BriefMe.Application.Common.Dtos;
using BriefMe.Application.UseCases.Briefings.ApproveBriefing;
using BriefMe.Application.UseCases.Briefings.CancelBriefing;
using BriefMe.Application.UseCases.Briefings.CreateBriefing;
using BriefMe.Application.UseCases.Briefings.EditBriefing;
using BriefMe.Application.UseCases.Briefings.GetBriefingById;
using BriefMe.Application.UseCases.Briefings.GetBriefingsByRequester;
using BriefMe.Application.UseCases.Briefings.GetBriefingsPendingApproval;
using BriefMe.Application.UseCases.Briefings.GetBriefingTracking;
using BriefMe.Application.UseCases.Briefings.RejectBriefing;
using BriefMe.Application.UseCases.Briefings.RemoveBriefingAttachment;
using BriefMe.Application.UseCases.Briefings.RequestChangesBriefing;
using BriefMe.Application.UseCases.Briefings.SubmitBriefingForApproval;
using BriefMe.Application.UseCases.Briefings.UploadBriefingAttachment;
using BriefMe.Application.UseCases.Briefings;
using BriefMe.Application.UseCases.Sla.GetOverdueBriefings;
using BriefMe.Domain.Common;
using BriefMe.Domain.Constants;
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

    private static BriefingDto SampleDto(Guid id) => new(
        id,
        "Campanha de lançamento",
        Description: null,
        Objective: null,
        RequestingArea: null,
        DesiredDeliveryDate: null,
        Priority.Medium,
        CategoryId: Guid.NewGuid(),
        RequesterId: Guid.NewGuid(),
        BriefingStatus.Draft);

    private static BriefingDetailDto SampleDetailDto(Guid id, params AttachmentDto[] attachments) => new(
        id,
        "Campanha de lançamento",
        Description: null,
        Objective: null,
        RequestingArea: null,
        DesiredDeliveryDate: null,
        Priority.Medium,
        CategoryId: Guid.NewGuid(),
        RequesterId: Guid.NewGuid(),
        BriefingStatus.Draft,
        attachments);

    private static AttachmentDto SampleAttachmentDto(Guid briefingId) => new(
        Guid.NewGuid(),
        briefingId,
        TaskId: null,
        "referencia.pdf",
        SizeBytes: 1024,
        UploadedById: Guid.NewGuid(),
        UploadedAt: DateTime.UtcNow);

    private static BriefingTrackingDto SampleTrackingDto(Guid id) => new(
        id,
        "Campanha de lançamento",
        Objective: null,
        CategoryId: Guid.NewGuid(),
        DesiredDeliveryDate: null,
        Priority.Medium,
        BriefingStatus.InProgress,
        RequesterId: Guid.NewGuid(),
        RequesterName: "Clara Solicitante",
        new BriefingProgressDto(TotalTasks: 4, ApprovedTasks: 2, Percentage: 50m, IsComplete: false),
        []);

    [Fact]
    public async Task Create_WhenCallerHasBriefingsCreatePermission_ReturnsCreatedWithLocation()
    {
        // Arrange
        var createdId = Guid.NewGuid();
        var senderMock = new Mock<ISender>();
        senderMock.Setup(s => s.Send(It.IsAny<CreateBriefingCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(SampleDto(createdId)));

        using var host = await StartHostAsync(senderMock.Object);
        var client = host.GetTestClient();
        Authenticate(client, GenerateToken(CreateJwtSettings(), Guid.NewGuid(), permissions: [Permissions.Briefings.Create]));

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
    public async Task Create_WhenCallerLacksBriefingsCreatePermission_ReturnsForbidden()
    {
        // Arrange
        var senderMock = new Mock<ISender>();

        using var host = await StartHostAsync(senderMock.Object);
        var client = host.GetTestClient();
        Authenticate(client, GenerateToken(CreateJwtSettings(), Guid.NewGuid(), roles: [Roles.Designer]));

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
        Authenticate(client, GenerateToken(CreateJwtSettings(), Guid.NewGuid(), permissions: [Permissions.Briefings.Create]));

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
        Authenticate(client, GenerateToken(CreateJwtSettings(), Guid.NewGuid(), permissions: [Permissions.Briefings.Create]));

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
    public async Task Submit_WhenCallerHasBriefingsCreatePermission_ReturnsOkWithBriefingDto()
    {
        // Arrange
        var briefingId = Guid.NewGuid();
        var senderMock = new Mock<ISender>();
        senderMock.Setup(s => s.Send(It.IsAny<SubmitBriefingForApprovalCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(SampleDto(briefingId) with { Status = BriefingStatus.AwaitingApproval }));

        using var host = await StartHostAsync(senderMock.Object);
        var client = host.GetTestClient();
        Authenticate(client, GenerateToken(CreateJwtSettings(), Guid.NewGuid(), permissions: [Permissions.Briefings.Create]));

        // Act
        var response = await client.PostAsync($"/briefings/{briefingId}/submit", content: null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<BriefingDto>();
        body!.Status.Should().Be(BriefingStatus.AwaitingApproval);
    }

    [Fact]
    public async Task Cancel_WhenCallerLacksBriefingsCreatePermission_ReturnsForbidden()
    {
        // Arrange
        var senderMock = new Mock<ISender>();

        using var host = await StartHostAsync(senderMock.Object);
        var client = host.GetTestClient();
        Authenticate(client, GenerateToken(CreateJwtSettings(), Guid.NewGuid(), roles: [Roles.Designer]));

        // Act
        var response = await client.PostAsync($"/briefings/{Guid.NewGuid()}/cancel", content: null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        senderMock.Verify(s => s.Send(It.IsAny<CancelBriefingCommand>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Approve_WhenCallerHasBriefingsApprovePermission_ReturnsOkWithApprovedBriefing()
    {
        // Arrange
        var briefingId = Guid.NewGuid();
        var senderMock = new Mock<ISender>();
        senderMock.Setup(s => s.Send(It.IsAny<ApproveBriefingCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(SampleDto(briefingId) with { Status = BriefingStatus.Approved }));

        using var host = await StartHostAsync(senderMock.Object);
        var client = host.GetTestClient();
        Authenticate(client, GenerateToken(CreateJwtSettings(), Guid.NewGuid(), permissions: [Permissions.Briefings.Approve]));

        // Act
        var response = await client.PostAsync($"/briefings/{briefingId}/approve", content: null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<BriefingDto>();
        body!.Status.Should().Be(BriefingStatus.Approved);
    }

    [Fact]
    public async Task Approve_WhenCallerIsDesignerWithoutApprovePermission_ReturnsForbidden()
    {
        // Arrange
        var senderMock = new Mock<ISender>();

        using var host = await StartHostAsync(senderMock.Object);
        var client = host.GetTestClient();
        Authenticate(client, GenerateToken(CreateJwtSettings(), Guid.NewGuid(), roles: [Roles.Designer]));

        // Act
        var response = await client.PostAsync($"/briefings/{Guid.NewGuid()}/approve", content: null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        senderMock.Verify(s => s.Send(It.IsAny<ApproveBriefingCommand>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Approve_WhenBriefingLeftAwaitingApproval_ReturnsConflictProblem()
    {
        // Arrange
        var senderMock = new Mock<ISender>();
        senderMock.Setup(s => s.Send(It.IsAny<ApproveBriefingCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Conflict<BriefingDto>("Briefing não está aguardando aprovação."));

        using var host = await StartHostAsync(senderMock.Object);
        var client = host.GetTestClient();
        Authenticate(client, GenerateToken(CreateJwtSettings(), Guid.NewGuid(), permissions: [Permissions.Briefings.Approve]));

        // Act
        var response = await client.PostAsync($"/briefings/{Guid.NewGuid()}/approve", content: null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        problem!.Detail.Should().Be("Briefing não está aguardando aprovação.");
    }

    [Fact]
    public async Task Reject_WhenCallerHasBriefingsRejectPermission_ForwardsJustificationAndReturnsOk()
    {
        // Arrange
        var briefingId = Guid.NewGuid();
        RejectBriefingCommand? capturedCommand = null;
        var senderMock = new Mock<ISender>();
        senderMock.Setup(s => s.Send(It.IsAny<RejectBriefingCommand>(), It.IsAny<CancellationToken>()))
            .Callback<IRequest<Result<BriefingDto>>, CancellationToken>((c, _) => capturedCommand = (RejectBriefingCommand)c)
            .ReturnsAsync(Result.Success(SampleDto(briefingId) with { Status = BriefingStatus.Rejected }));

        using var host = await StartHostAsync(senderMock.Object);
        var client = host.GetTestClient();
        Authenticate(client, GenerateToken(CreateJwtSettings(), Guid.NewGuid(), permissions: [Permissions.Briefings.Reject]));

        // Act
        var response = await client.PostAsJsonAsync(
            $"/briefings/{briefingId}/reject",
            new RejectBriefingRequest("Escopo fora da capacidade do time."));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        capturedCommand.Should().NotBeNull();
        capturedCommand!.BriefingId.Should().Be(briefingId);
        capturedCommand.Justification.Should().Be("Escopo fora da capacidade do time.");
    }

    [Fact]
    public async Task Reject_WhenCallerLacksBriefingsRejectPermission_ReturnsForbidden()
    {
        // Arrange
        var senderMock = new Mock<ISender>();

        using var host = await StartHostAsync(senderMock.Object);
        var client = host.GetTestClient();
        Authenticate(client, GenerateToken(CreateJwtSettings(), Guid.NewGuid(), permissions: [Permissions.Briefings.Approve]));

        // Act
        var response = await client.PostAsJsonAsync(
            $"/briefings/{Guid.NewGuid()}/reject",
            new RejectBriefingRequest("Escopo fora da capacidade do time."));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        senderMock.Verify(s => s.Send(It.IsAny<RejectBriefingCommand>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Reject_WhenJustificationIsEmpty_ReturnsValidationProblem()
    {
        // Arrange
        var senderMock = new Mock<ISender>();
        senderMock.Setup(s => s.Send(It.IsAny<RejectBriefingCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure<BriefingDto>([new ValidationError("Justification", "Justificativa é obrigatória.")]));

        using var host = await StartHostAsync(senderMock.Object);
        var client = host.GetTestClient();
        Authenticate(client, GenerateToken(CreateJwtSettings(), Guid.NewGuid(), permissions: [Permissions.Briefings.Reject]));

        // Act
        var response = await client.PostAsJsonAsync($"/briefings/{Guid.NewGuid()}/reject", new RejectBriefingRequest(string.Empty));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>();
        problem!.Errors.Should().ContainKey("Justification");
    }

    [Fact]
    public async Task RequestChanges_WhenCallerHasRequestAdjustmentsPermission_ReturnsOkWithChangesRequested()
    {
        // Arrange
        var briefingId = Guid.NewGuid();
        var senderMock = new Mock<ISender>();
        senderMock.Setup(s => s.Send(It.IsAny<RequestChangesBriefingCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(SampleDto(briefingId) with { Status = BriefingStatus.ChangesRequested }));

        using var host = await StartHostAsync(senderMock.Object);
        var client = host.GetTestClient();
        Authenticate(client, GenerateToken(CreateJwtSettings(), Guid.NewGuid(), permissions: [Permissions.Briefings.RequestAdjustments]));

        // Act
        var response = await client.PostAsJsonAsync(
            $"/briefings/{briefingId}/request-changes",
            new RequestChangesBriefingRequest("Detalhar o público-alvo da campanha."));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<BriefingDto>();
        body!.Status.Should().Be(BriefingStatus.ChangesRequested);
    }

    [Fact]
    public async Task RequestChanges_WhenCallerLacksRequestAdjustmentsPermission_ReturnsForbidden()
    {
        // Arrange
        var senderMock = new Mock<ISender>();

        using var host = await StartHostAsync(senderMock.Object);
        var client = host.GetTestClient();
        Authenticate(client, GenerateToken(CreateJwtSettings(), Guid.NewGuid(), permissions: [Permissions.Briefings.Approve]));

        // Act
        var response = await client.PostAsJsonAsync(
            $"/briefings/{Guid.NewGuid()}/request-changes",
            new RequestChangesBriefingRequest("Detalhar o público-alvo da campanha."));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        senderMock.Verify(s => s.Send(It.IsAny<RequestChangesBriefingCommand>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RequestChanges_WhenBriefingDoesNotExist_ReturnsNotFoundProblem()
    {
        // Arrange
        var senderMock = new Mock<ISender>();
        senderMock.Setup(s => s.Send(It.IsAny<RequestChangesBriefingCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.NotFound<BriefingDto>("Briefing não encontrado."));

        using var host = await StartHostAsync(senderMock.Object);
        var client = host.GetTestClient();
        Authenticate(client, GenerateToken(CreateJwtSettings(), Guid.NewGuid(), permissions: [Permissions.Briefings.RequestAdjustments]));

        // Act
        var response = await client.PostAsJsonAsync(
            $"/briefings/{Guid.NewGuid()}/request-changes",
            new RequestChangesBriefingRequest("Detalhar o público-alvo da campanha."));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        problem!.Detail.Should().Be("Briefing não encontrado.");
    }

    [Fact]
    public async Task GetById_WhenCallerHasViewOwnPermission_ReturnsOkWithBriefingDetailDto()
    {
        // Arrange
        var briefingId = Guid.NewGuid();
        var senderMock = new Mock<ISender>();
        senderMock.Setup(s => s.Send(It.IsAny<GetBriefingByIdQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(SampleDetailDto(briefingId, SampleAttachmentDto(briefingId))));

        using var host = await StartHostAsync(senderMock.Object);
        var client = host.GetTestClient();
        Authenticate(client, GenerateToken(CreateJwtSettings(), Guid.NewGuid(), permissions: [Permissions.Briefings.ViewOwn]));

        // Act
        var response = await client.GetAsync($"/briefings/{briefingId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<BriefingDetailDto>();
        body!.Id.Should().Be(briefingId);
        body.Attachments.Should().ContainSingle();
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
        Authenticate(client, GenerateToken(CreateJwtSettings(), Guid.NewGuid(), permissions: [Permissions.Briefings.ViewOwn]));

        // Act
        var response = await client.GetAsync($"/briefings/{Guid.NewGuid()}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task GetById_WhenCallerLacksViewOwnPermission_ReturnsForbidden()
    {
        // Arrange
        var senderMock = new Mock<ISender>();

        using var host = await StartHostAsync(senderMock.Object);
        var client = host.GetTestClient();
        Authenticate(client, GenerateToken(CreateJwtSettings(), Guid.NewGuid(), roles: [Roles.Designer]));

        // Act
        var response = await client.GetAsync($"/briefings/{Guid.NewGuid()}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        senderMock.Verify(s => s.Send(It.IsAny<GetBriefingByIdQuery>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GetTracking_WhenCallerHasViewOwnPermission_ReturnsOkWithProgress()
    {
        // Arrange
        var briefingId = Guid.NewGuid();
        var senderMock = new Mock<ISender>();
        senderMock.Setup(s => s.Send(It.IsAny<GetBriefingTrackingQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(SampleTrackingDto(briefingId)));

        using var host = await StartHostAsync(senderMock.Object);
        var client = host.GetTestClient();
        Authenticate(client, GenerateToken(CreateJwtSettings(), Guid.NewGuid(), permissions: [Permissions.Briefings.ViewOwn]));

        // Act
        var response = await client.GetAsync($"/briefings/{briefingId}/tracking");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<BriefingTrackingDto>();
        body!.Id.Should().Be(briefingId);
        body.Progress.Percentage.Should().Be(50m);
    }

    /// <summary>
    /// The Design Manager holds <c>briefings.view-all</c> and not <c>briefings.view-own</c> (USR-047), so
    /// the "any of" gate is what makes tracking reachable for the profile that runs the pipeline (BRF-039).
    /// </summary>
    [Fact]
    public async Task GetTracking_WhenCallerOnlyHasViewAllPermission_ReturnsOk()
    {
        // Arrange
        var briefingId = Guid.NewGuid();
        var senderMock = new Mock<ISender>();
        senderMock.Setup(s => s.Send(It.IsAny<GetBriefingTrackingQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(SampleTrackingDto(briefingId)));

        using var host = await StartHostAsync(senderMock.Object);
        var client = host.GetTestClient();
        Authenticate(client, GenerateToken(CreateJwtSettings(), Guid.NewGuid(), permissions: [Permissions.Briefings.ViewAll]));

        // Act
        var response = await client.GetAsync($"/briefings/{briefingId}/tracking");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetTracking_WhenCallerLacksBothViewPermissions_ReturnsForbidden()
    {
        // Arrange
        var senderMock = new Mock<ISender>();

        using var host = await StartHostAsync(senderMock.Object);
        var client = host.GetTestClient();
        Authenticate(client, GenerateToken(CreateJwtSettings(), Guid.NewGuid(), roles: [Roles.Designer]));

        // Act
        var response = await client.GetAsync($"/briefings/{Guid.NewGuid()}/tracking");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        senderMock.Verify(s => s.Send(It.IsAny<GetBriefingTrackingQuery>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GetMyBriefings_WhenCallerHasViewOwnPermission_MapsQueryParamsAndReturnsOk()
    {
        // Arrange
        GetBriefingsByRequesterQuery? capturedQuery = null;
        var categoryId = Guid.NewGuid();
        var pagedResult = PagedDto<BriefingDto>.Create([], page: 2, pageSize: 5, totalCount: 0);
        var senderMock = new Mock<ISender>();
        senderMock.Setup(s => s.Send(It.IsAny<GetBriefingsByRequesterQuery>(), It.IsAny<CancellationToken>()))
            .Callback<IRequest<Result<PagedDto<BriefingDto>>>, CancellationToken>((q, _) => capturedQuery = (GetBriefingsByRequesterQuery)q)
            .ReturnsAsync(Result.Success(pagedResult));

        using var host = await StartHostAsync(senderMock.Object);
        var client = host.GetTestClient();
        Authenticate(client, GenerateToken(CreateJwtSettings(), Guid.NewGuid(), permissions: [Permissions.Briefings.ViewOwn]));

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
    public async Task GetMyBriefings_WhenCallerLacksViewOwnPermission_ReturnsForbidden()
    {
        // Arrange
        var senderMock = new Mock<ISender>();

        using var host = await StartHostAsync(senderMock.Object);
        var client = host.GetTestClient();
        Authenticate(client, GenerateToken(CreateJwtSettings(), Guid.NewGuid(), roles: [Roles.Designer]));

        // Act
        var response = await client.GetAsync("/briefings");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        senderMock.Verify(s => s.Send(It.IsAny<GetBriefingsByRequesterQuery>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GetPendingApproval_WhenCallerHasViewAllPermission_MapsPaginationAndReturnsOk()
    {
        // Arrange
        GetBriefingsPendingApprovalQuery? capturedQuery = null;
        var pagedResult = PagedDto<PendingApprovalBriefingDto>.Create([], page: 2, pageSize: 5, totalCount: 0);
        var senderMock = new Mock<ISender>();
        senderMock.Setup(s => s.Send(It.IsAny<GetBriefingsPendingApprovalQuery>(), It.IsAny<CancellationToken>()))
            .Callback<IRequest<Result<PagedDto<PendingApprovalBriefingDto>>>, CancellationToken>((q, _) => capturedQuery = (GetBriefingsPendingApprovalQuery)q)
            .ReturnsAsync(Result.Success(pagedResult));

        using var host = await StartHostAsync(senderMock.Object);
        var client = host.GetTestClient();
        Authenticate(client, GenerateToken(CreateJwtSettings(), Guid.NewGuid(), permissions: [Permissions.Briefings.ViewAll]));

        // Act
        var response = await client.GetAsync("/briefings/pending-approval?page=2&pageSize=5");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        capturedQuery.Should().NotBeNull();
        capturedQuery!.Page.Should().Be(2);
        capturedQuery.PageSize.Should().Be(5);
    }

    [Fact]
    public async Task GetPendingApproval_WhenCallerOnlyHasViewOwnPermission_ReturnsForbidden()
    {
        // Arrange
        var senderMock = new Mock<ISender>();

        using var host = await StartHostAsync(senderMock.Object);
        var client = host.GetTestClient();
        Authenticate(client, GenerateToken(CreateJwtSettings(), Guid.NewGuid(), permissions: [Permissions.Briefings.ViewOwn]));

        // Act
        var response = await client.GetAsync("/briefings/pending-approval");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        senderMock.Verify(s => s.Send(It.IsAny<GetBriefingsPendingApprovalQuery>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GetOverdue_WhenCallerHasViewAllPermission_ForwardsQueryWithDefaultPagingAndReturnsOk()
    {
        // Arrange
        GetOverdueBriefingsQuery? capturedQuery = null;
        var senderMock = new Mock<ISender>();
        senderMock.Setup(s => s.Send(It.IsAny<GetOverdueBriefingsQuery>(), It.IsAny<CancellationToken>()))
            .Callback<IRequest<Result<PagedDto<OverdueBriefingDto>>>, CancellationToken>((q, _) => capturedQuery = (GetOverdueBriefingsQuery)q)
            .ReturnsAsync(Result.Success(PagedDto<OverdueBriefingDto>.Create([], 1, 20, 0)));

        using var host = await StartHostAsync(senderMock.Object);
        var client = host.GetTestClient();
        Authenticate(client, GenerateToken(CreateJwtSettings(), Guid.NewGuid(), permissions: [Permissions.Briefings.ViewAll]));

        // Act
        var response = await client.GetAsync("/briefings/overdue");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        capturedQuery.Should().NotBeNull();
        capturedQuery!.Page.Should().Be(1);
        capturedQuery.PageSize.Should().Be(20);
    }

    [Fact]
    public async Task GetOverdue_WhenPagingIsGiven_ForwardsIt()
    {
        // Arrange
        GetOverdueBriefingsQuery? capturedQuery = null;
        var senderMock = new Mock<ISender>();
        senderMock.Setup(s => s.Send(It.IsAny<GetOverdueBriefingsQuery>(), It.IsAny<CancellationToken>()))
            .Callback<IRequest<Result<PagedDto<OverdueBriefingDto>>>, CancellationToken>((q, _) => capturedQuery = (GetOverdueBriefingsQuery)q)
            .ReturnsAsync(Result.Success(PagedDto<OverdueBriefingDto>.Create([], 3, 50, 0)));

        using var host = await StartHostAsync(senderMock.Object);
        var client = host.GetTestClient();
        Authenticate(client, GenerateToken(CreateJwtSettings(), Guid.NewGuid(), permissions: [Permissions.Briefings.ViewAll]));

        // Act
        var response = await client.GetAsync("/briefings/overdue?page=3&pageSize=50");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        capturedQuery!.Page.Should().Be(3);
        capturedQuery.PageSize.Should().Be(50);
    }

    [Fact]
    public async Task GetOverdue_WhenCallerOnlyHasViewOwnPermission_ReturnsForbidden()
    {
        // Arrange — the delay list crosses every requester's demands, which is exactly what the
        // ownership-scoped permission does not open.
        var senderMock = new Mock<ISender>();

        using var host = await StartHostAsync(senderMock.Object);
        var client = host.GetTestClient();
        Authenticate(client, GenerateToken(CreateJwtSettings(), Guid.NewGuid(), permissions: [Permissions.Briefings.ViewOwn]));

        // Act
        var response = await client.GetAsync("/briefings/overdue");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        senderMock.Verify(s => s.Send(It.IsAny<GetOverdueBriefingsQuery>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task UploadAttachment_WhenFilePartIsPresent_ForwardsFileMetadataAndReturnsOk()
    {
        // Arrange
        var briefingId = Guid.NewGuid();
        var bytes = "conteudo-de-referencia"u8.ToArray();
        string? capturedFileName = null;
        long capturedSize = 0;
        var capturedHasContent = false;
        var senderMock = new Mock<ISender>();
        senderMock.Setup(s => s.Send(It.IsAny<UploadBriefingAttachmentCommand>(), It.IsAny<CancellationToken>()))
            // Read inside the callback: the request stream is disposed once the response completes.
            .Callback<IRequest<Result<AttachmentDto>>, CancellationToken>((c, _) =>
            {
                var command = (UploadBriefingAttachmentCommand)c;
                capturedFileName = command.FileName;
                capturedSize = command.SizeBytes;
                capturedHasContent = command.Content is not null;
            })
            .ReturnsAsync(Result.Success(SampleAttachmentDto(briefingId)));

        using var host = await StartHostAsync(senderMock.Object);
        var client = host.GetTestClient();
        Authenticate(client, GenerateToken(CreateJwtSettings(), Guid.NewGuid(), permissions: [Permissions.Briefings.Create]));

        // Act
        using var upload = new MultipartFormDataContent { { new ByteArrayContent(bytes), "file", "referencia.pdf" } };
        var response = await client.PostAsync($"/briefings/{briefingId}/attachments", upload);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        capturedFileName.Should().Be("referencia.pdf");
        capturedSize.Should().Be(bytes.Length);
        capturedHasContent.Should().BeTrue();
    }

    [Fact]
    public async Task UploadAttachment_WhenFilePartIsMissing_DispatchesEmptyCommandAndReturnsValidationProblem()
    {
        // Arrange
        var capturedHasContent = true;
        var senderMock = new Mock<ISender>();
        senderMock.Setup(s => s.Send(It.IsAny<UploadBriefingAttachmentCommand>(), It.IsAny<CancellationToken>()))
            .Callback<IRequest<Result<AttachmentDto>>, CancellationToken>((c, _) =>
                capturedHasContent = ((UploadBriefingAttachmentCommand)c).Content is not null)
            .ReturnsAsync(Result.Failure<AttachmentDto>([new ValidationError("Content", "File content is required.")]));

        using var host = await StartHostAsync(senderMock.Object);
        var client = host.GetTestClient();
        Authenticate(client, GenerateToken(CreateJwtSettings(), Guid.NewGuid(), permissions: [Permissions.Briefings.Create]));

        // Act — a multipart body carrying everything but the file part.
        using var upload = new MultipartFormDataContent { { new StringContent("referencia"), "kind" } };
        var response = await client.PostAsync($"/briefings/{Guid.NewGuid()}/attachments", upload);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        capturedHasContent.Should().BeFalse();
    }

    [Fact]
    public async Task UploadAttachment_WhenCallerLacksCreatePermission_ReturnsForbidden()
    {
        // Arrange
        var senderMock = new Mock<ISender>();

        using var host = await StartHostAsync(senderMock.Object);
        var client = host.GetTestClient();
        Authenticate(client, GenerateToken(CreateJwtSettings(), Guid.NewGuid(), permissions: [Permissions.Briefings.ViewOwn]));

        // Act
        using var upload = new MultipartFormDataContent { { new ByteArrayContent("x"u8.ToArray()), "file", "referencia.pdf" } };
        var response = await client.PostAsync($"/briefings/{Guid.NewGuid()}/attachments", upload);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        senderMock.Verify(s => s.Send(It.IsAny<UploadBriefingAttachmentCommand>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RemoveAttachment_WhenCallerHasCreatePermission_ReturnsOkWithRemainingAttachments()
    {
        // Arrange
        var briefingId = Guid.NewGuid();
        var senderMock = new Mock<ISender>();
        senderMock.Setup(s => s.Send(It.IsAny<RemoveBriefingAttachmentCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(SampleDetailDto(briefingId)));

        using var host = await StartHostAsync(senderMock.Object);
        var client = host.GetTestClient();
        Authenticate(client, GenerateToken(CreateJwtSettings(), Guid.NewGuid(), permissions: [Permissions.Briefings.Create]));

        // Act
        var response = await client.DeleteAsync($"/briefings/{briefingId}/attachments/{Guid.NewGuid()}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<BriefingDetailDto>();
        body!.Attachments.Should().BeEmpty();
    }

    [Fact]
    public async Task RemoveAttachment_WhenAttachmentDoesNotExist_ReturnsNotFoundProblem()
    {
        // Arrange
        var senderMock = new Mock<ISender>();
        senderMock.Setup(s => s.Send(It.IsAny<RemoveBriefingAttachmentCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.NotFound<BriefingDetailDto>("Anexo não encontrado."));

        using var host = await StartHostAsync(senderMock.Object);
        var client = host.GetTestClient();
        Authenticate(client, GenerateToken(CreateJwtSettings(), Guid.NewGuid(), permissions: [Permissions.Briefings.Create]));

        // Act
        var response = await client.DeleteAsync($"/briefings/{Guid.NewGuid()}/attachments/{Guid.NewGuid()}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        problem!.Detail.Should().Be("Anexo não encontrado.");
    }

    [Fact]
    public async Task RemoveAttachment_WhenCallerLacksCreatePermission_ReturnsForbidden()
    {
        // Arrange
        var senderMock = new Mock<ISender>();

        using var host = await StartHostAsync(senderMock.Object);
        var client = host.GetTestClient();
        Authenticate(client, GenerateToken(CreateJwtSettings(), Guid.NewGuid(), permissions: [Permissions.Briefings.ViewOwn]));

        // Act
        var response = await client.DeleteAsync($"/briefings/{Guid.NewGuid()}/attachments/{Guid.NewGuid()}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        senderMock.Verify(s => s.Send(It.IsAny<RemoveBriefingAttachmentCommand>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
