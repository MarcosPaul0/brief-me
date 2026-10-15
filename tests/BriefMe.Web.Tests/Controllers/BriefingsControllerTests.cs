using BriefMe.Application.Abstractions.Messaging;
using BriefMe.Application.Abstractions;
using BriefMe.Application.Common.Dtos;
using BriefMe.Application.UseCases.Briefings.AcceptBriefingDelivery;
using BriefMe.Application.UseCases.Briefings.CancelBriefing;
using BriefMe.Application.UseCases.Briefings.ClaimBriefing;
using BriefMe.Application.UseCases.Briefings.CreateBriefing;
using BriefMe.Application.UseCases.Briefings.DeliverBriefing;
using BriefMe.Application.UseCases.Briefings.EditBriefing;
using BriefMe.Application.UseCases.Briefings.GetBriefingAttachmentContent;
using BriefMe.Application.UseCases.Briefings.GetBriefingById;
using BriefMe.Application.UseCases.Briefings.GetBriefingQueue;
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
    // Web-layer authorization pipeline (role gates), not JwtService's claim population.
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

    /// <summary>The multipart body <c>POST /briefings</c> expects: the request fields plus one part per file.</summary>
    private static MultipartFormDataContent CreateForm(params (string FileName, byte[] Bytes)[] files)
    {
        var form = new MultipartFormDataContent
        {
            { new StringContent("Campanha de lançamento"), nameof(CreateBriefingRequest.Title) },
            { new StringContent(nameof(Priority.Medium)), nameof(CreateBriefingRequest.Priority) },
            { new StringContent(Guid.NewGuid().ToString()), nameof(CreateBriefingRequest.CategoryId) },
            { new StringContent("2026-12-01"), nameof(CreateBriefingRequest.DesiredDeliveryDate) },
        };
        AddFiles(form, files);
        return form;
    }

    private static MultipartFormDataContent UpdateForm(IReadOnlyList<Guid>? removedIds = null, params (string FileName, byte[] Bytes)[] files)
    {
        var form = new MultipartFormDataContent
        {
            { new StringContent("Título editado"), nameof(UpdateBriefingRequest.Title) },
            { new StringContent(nameof(Priority.High)), nameof(UpdateBriefingRequest.Priority) },
        };
        foreach (var id in removedIds ?? [])
            form.Add(new StringContent(id.ToString()), "removedAttachmentIds");

        AddFiles(form, files);
        return form;
    }

    private static void AddFiles(MultipartFormDataContent form, IEnumerable<(string FileName, byte[] Bytes)> files)
    {
        foreach (var (fileName, bytes) in files)
            form.Add(new ByteArrayContent(bytes), "attachments", fileName);
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
        CreatedAt: DateTime.UtcNow,
        Attachments: []);

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
        var response = await client.PostAsync("/briefings", CreateForm());

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
        var response = await client.PostAsync("/briefings", CreateForm());

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
        var response = await client.PostAsync("/briefings", CreateForm());

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        problem!.Detail.Should().Be("Categoria não encontrada.");
    }

    [Fact]
    public async Task Create_WhenFilesArePosted_SendsThemAsAttachmentUploads()
    {
        // Arrange
        CreateBriefingCommand? captured = null;
        var received = new List<(string? FileName, long Length)>();
        var senderMock = new Mock<ISender>();
        senderMock.Setup(s => s.Send(It.IsAny<CreateBriefingCommand>(), It.IsAny<CancellationToken>()))
            .Callback<IRequest<Result<BriefingDto>>, CancellationToken>((command, _) =>
            {
                captured = (CreateBriefingCommand)command;
                received.AddRange(captured.Attachments!.Select(a => (a.FileName, a.Length)));
            })
            .ReturnsAsync(Result.Success(SampleDto(Guid.NewGuid())));

        using var host = await StartHostAsync(senderMock.Object);
        var client = host.GetTestClient();
        Authenticate(client, GenerateToken(CreateJwtSettings(), Guid.NewGuid(), roles: [nameof(UserRole.Requester)]));

        // Act
        var response = await client.PostAsync("/briefings", CreateForm(("guide.pdf", new byte[10]), ("clip.mp4", new byte[20])));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        received.Should().Equal(("guide.pdf", 10L), ("clip.mp4", 20L));
        captured!.DesiredDeliveryDate.Should().Be(new DateOnly(2026, 12, 1));
    }

    [Fact]
    public async Task Create_WhenNoFilesArePosted_SendsEmptyAttachmentList()
    {
        // Arrange
        CreateBriefingCommand? captured = null;
        var senderMock = new Mock<ISender>();
        senderMock.Setup(s => s.Send(It.IsAny<CreateBriefingCommand>(), It.IsAny<CancellationToken>()))
            .Callback<IRequest<Result<BriefingDto>>, CancellationToken>((command, _) => captured = (CreateBriefingCommand)command)
            .ReturnsAsync(Result.Success(SampleDto(Guid.NewGuid())));

        using var host = await StartHostAsync(senderMock.Object);
        var client = host.GetTestClient();
        Authenticate(client, GenerateToken(CreateJwtSettings(), Guid.NewGuid(), roles: [nameof(UserRole.Requester)]));

        // Act
        await client.PostAsync("/briefings", CreateForm());

        // Assert
        captured!.Attachments.Should().BeEmpty();
    }

    [Fact]
    public async Task Update_WhenFilesAndRemovedIdsArePosted_SendsBothInTheCommand()
    {
        // Arrange
        EditBriefingCommand? captured = null;
        var receivedFiles = new List<string?>();
        var senderMock = new Mock<ISender>();
        senderMock.Setup(s => s.Send(It.IsAny<EditBriefingCommand>(), It.IsAny<CancellationToken>()))
            .Callback<IRequest<Result<BriefingDto>>, CancellationToken>((command, _) =>
            {
                captured = (EditBriefingCommand)command;
                receivedFiles.AddRange(captured.NewAttachments!.Select(a => a.FileName));
            })
            .ReturnsAsync(Result.Success(SampleDto(Guid.NewGuid())));
        var removed = new[] { Guid.NewGuid(), Guid.NewGuid() };

        using var host = await StartHostAsync(senderMock.Object);
        var client = host.GetTestClient();
        Authenticate(client, GenerateToken(CreateJwtSettings(), Guid.NewGuid(), roles: [nameof(UserRole.Requester)]));

        // Act
        var response = await client.PutAsync($"/briefings/{Guid.NewGuid()}", UpdateForm(removed, ("new.webm", new byte[5])));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        captured!.RemovedAttachmentIds.Should().Equal(removed);
        receivedFiles.Should().Equal("new.webm");
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
        var response = await client.PutAsync($"/briefings/{Guid.NewGuid()}", UpdateForm());

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

    [Fact]
    public async Task GetQueue_WhenCallerIsRequester_ReturnsForbidden()
    {
        // Arrange
        var senderMock = new Mock<ISender>();

        using var host = await StartHostAsync(senderMock.Object);
        var client = host.GetTestClient();
        Authenticate(client, GenerateToken(CreateJwtSettings(), Guid.NewGuid(), roles: [nameof(UserRole.Requester)]));

        // Act
        var response = await client.GetAsync("/briefings/queue");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task GetQueue_WhenCallerIsDesigner_ForwardsPaging()
    {
        // Arrange
        var senderMock = new Mock<ISender>();
        senderMock.Setup(s => s.Send(It.IsAny<GetBriefingQueueQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(PagedDto<BriefingListItemDto>.Create([], 2, 5, 0)));

        using var host = await StartHostAsync(senderMock.Object);
        var client = host.GetTestClient();
        Authenticate(client, GenerateToken(CreateJwtSettings(), Guid.NewGuid(), roles: [nameof(UserRole.Designer)]));

        // Act
        await client.GetAsync("/briefings/queue?page=2&pageSize=5");

        // Assert
        senderMock.Verify(s => s.Send(
            It.Is<GetBriefingQueueQuery>(q => q.Page == 2 && q.PageSize == 5),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Claim_WhenAnotherDesignerWon_ReturnsConflictProblem()
    {
        // Arrange
        var senderMock = new Mock<ISender>();
        senderMock.Setup(s => s.Send(It.IsAny<ClaimBriefingCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Conflict<BriefingDto>("already claimed"));

        using var host = await StartHostAsync(senderMock.Object);
        var client = host.GetTestClient();
        Authenticate(client, GenerateToken(CreateJwtSettings(), Guid.NewGuid(), roles: [nameof(UserRole.Designer)]));

        // Act
        var response = await client.PostAsync($"/briefings/{Guid.NewGuid()}/claim", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Deliver_WhenCallerIsDesigner_ForwardsTheNote()
    {
        // Arrange
        var briefingId = Guid.NewGuid();
        var senderMock = new Mock<ISender>();
        senderMock.Setup(s => s.Send(It.IsAny<DeliverBriefingCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(SampleDto(briefingId, BriefingStatus.Delivered)));

        using var host = await StartHostAsync(senderMock.Object);
        var client = host.GetTestClient();
        Authenticate(client, GenerateToken(CreateJwtSettings(), Guid.NewGuid(), roles: [nameof(UserRole.Designer)]));

        // Act
        await client.PostAsJsonAsync($"/briefings/{briefingId}/deliver", new DeliverBriefingRequest("https://files.example/v1"));

        // Assert
        senderMock.Verify(s => s.Send(
            It.Is<DeliverBriefingCommand>(c => c.BriefingId == briefingId && c.DeliveryNote == "https://files.example/v1"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    private static Mock<ISender> SenderReturningAttachment(byte[] bytes, string contentType = "video/mp4", string fileName = "clipe de referência.mp4")
    {
        var senderMock = new Mock<ISender>();
        senderMock.Setup(s => s.Send(It.IsAny<GetBriefingAttachmentContentQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => Result.Success(new AttachmentFileDto(new MemoryStream(bytes), contentType, fileName)));
        return senderMock;
    }

    [Fact]
    public async Task GetAttachmentContent_WhenTokenIsAcceptedWithoutBearer_StreamsInlineWithSafeHeaders()
    {
        // Arrange
        var senderMock = SenderReturningAttachment([1, 2, 3, 4]);
        using var host = await StartHostAsync(senderMock.Object);
        var client = host.GetTestClient();

        // Act
        var response = await client.GetAsync($"/briefings/{Guid.NewGuid()}/attachments/{Guid.NewGuid()}/content?token=abc");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.Should().Be("video/mp4");
        response.Content.Headers.ContentDisposition!.DispositionType.Should().Be("inline");
        response.Content.Headers.ContentDisposition.FileNameStar.Should().Be("clipe de referência.mp4");
        response.Headers.GetValues("X-Content-Type-Options").Should().Equal("nosniff");
        response.Headers.CacheControl!.Private.Should().BeTrue();
        (await response.Content.ReadAsByteArrayAsync()).Should().Equal(1, 2, 3, 4);
    }

    [Fact]
    public async Task GetAttachmentContent_WhenRangeIsRequested_ReturnsPartialContent()
    {
        // Arrange
        var senderMock = SenderReturningAttachment([.. Enumerable.Range(0, 100).Select(i => (byte)i)]);
        using var host = await StartHostAsync(senderMock.Object);
        var client = host.GetTestClient();
        var request = new HttpRequestMessage(HttpMethod.Get, $"/briefings/{Guid.NewGuid()}/attachments/{Guid.NewGuid()}/content?token=abc");
        request.Headers.Range = new System.Net.Http.Headers.RangeHeaderValue(10, 19);

        // Act
        var response = await client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.PartialContent);
        response.Content.Headers.ContentRange!.ToString().Should().Be("bytes 10-19/100");
        (await response.Content.ReadAsByteArrayAsync()).Should().Equal(Enumerable.Range(10, 10).Select(i => (byte)i));
    }

    [Fact]
    public async Task GetAttachmentContent_WhenQueryAnswersNotFound_ReturnsNotFound()
    {
        // Arrange
        var senderMock = new Mock<ISender>();
        senderMock.Setup(s => s.Send(It.IsAny<GetBriefingAttachmentContentQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.NotFound<AttachmentFileDto>("Attachment not found."));
        using var host = await StartHostAsync(senderMock.Object);
        var client = host.GetTestClient();

        // Act
        var response = await client.GetAsync($"/briefings/{Guid.NewGuid()}/attachments/{Guid.NewGuid()}/content?token=forged");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetAttachmentContent_WhenCalled_ForwardsRouteIdsAndToken()
    {
        // Arrange
        GetBriefingAttachmentContentQuery? captured = null;
        var senderMock = SenderReturningAttachment([1]);
        senderMock.Setup(s => s.Send(It.IsAny<GetBriefingAttachmentContentQuery>(), It.IsAny<CancellationToken>()))
            .Callback<IRequest<Result<AttachmentFileDto>>, CancellationToken>((query, _) => captured = (GetBriefingAttachmentContentQuery)query)
            .ReturnsAsync(Result.Success(new AttachmentFileDto(new MemoryStream([1]), "application/pdf", "a.pdf")));
        using var host = await StartHostAsync(senderMock.Object);
        var client = host.GetTestClient();
        var briefingId = Guid.NewGuid();
        var attachmentId = Guid.NewGuid();

        // Act
        await client.GetAsync($"/briefings/{briefingId}/attachments/{attachmentId}/content?token=a.b-c_d");

        // Assert
        captured.Should().Be(new GetBriefingAttachmentContentQuery(briefingId, attachmentId, "a.b-c_d"));
    }
}
