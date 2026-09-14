using BriefMe.Application.Abstractions.Messaging;
using BriefMe.Application.Common.Dtos;
using BriefMe.Application.UseCases.Roles.CreateRole;
using BriefMe.Application.UseCases.Roles.GetRoleById;
using BriefMe.Application.UseCases.Roles.GetRoles;
using BriefMe.Application.UseCases.Roles.UpdateRole;
using BriefMe.Application.UseCases.Roles.UpdateRolePermissions;
using BriefMe.Application.UseCases.Roles;
using BriefMe.Domain.Common;
using BriefMe.Domain.Constants;
using BriefMe.Infrastructure.Auth;
using BriefMe.Web.Contracts.Roles;
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
using Microsoft.IdentityModel.Tokens;
using Moq;
using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Json;
using System.Net;
using System.Security.Claims;
using System.Text;

namespace BriefMe.Web.Tests.Controllers;

public class RolesControllerTests
{
    private const string SecretKey = "web-tests-roles-controller-secret-key-32b!";

    private static JwtSettings CreateJwtSettings() => new()
    {
        Issuer = "BriefMe.Web.Tests",
        Audience = "BriefMe.Web.Tests.Audience",
        SecretKey = SecretKey,
        AccessTokenMinutes = 15
    };

    private static string GenerateToken(JwtSettings settings, string[]? roles = null, string[]? permissions = null)
    {
        var keyBytes = Encoding.UTF8.GetBytes(settings.SecretKey);
        var credentials = new SigningCredentials(new SymmetricSecurityKey(keyBytes), SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, Guid.NewGuid().ToString()),
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
                    services.AddControllers().AddApplicationPart(typeof(RolesController).Assembly);
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

    private static void Authenticate(HttpClient client, string token) =>
        client.DefaultRequestHeaders.Authorization = new("Bearer", token);

    [Fact]
    public async Task Create_WhenCallerHasRolesManagePermission_ReturnsCreatedWithLocation()
    {
        // Arrange
        var createdRoleId = Guid.NewGuid();
        var roleResult = new RoleDto(createdRoleId, "Designer", "Perfil de designer", false, []);
        var senderMock = new Mock<ISender>();
        senderMock.Setup(s => s.Send(It.IsAny<CreateRoleCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(roleResult));

        using var host = await StartHostAsync(senderMock.Object);
        var client = host.GetTestClient();
        Authenticate(client, GenerateToken(CreateJwtSettings(), roles: [Roles.Administrador]));

        // Act
        var response = await client.PostAsJsonAsync("/roles", new CreateRoleRequest("Designer", "Perfil de designer"));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        response.Headers.Location!.ToString().Should().Contain($"/roles/{createdRoleId}");
        var body = await response.Content.ReadFromJsonAsync<RoleDto>();
        body!.RoleId.Should().Be(createdRoleId);
    }

    [Fact]
    public async Task Create_WhenCallerLacksRolesManagePermission_ReturnsForbidden()
    {
        // Arrange
        var senderMock = new Mock<ISender>();

        using var host = await StartHostAsync(senderMock.Object);
        var client = host.GetTestClient();
        Authenticate(client, GenerateToken(CreateJwtSettings(), roles: [Roles.Designer]));

        // Act
        var response = await client.PostAsJsonAsync("/roles", new CreateRoleRequest("Designer", null));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        senderMock.Verify(s => s.Send(It.IsAny<CreateRoleCommand>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Create_WhenNameAlreadyExists_ReturnsConflictProblem()
    {
        // Arrange
        var senderMock = new Mock<ISender>();
        senderMock.Setup(s => s.Send(It.IsAny<CreateRoleCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Conflict<RoleDto>("Nome de role já cadastrado."));

        using var host = await StartHostAsync(senderMock.Object);
        var client = host.GetTestClient();
        Authenticate(client, GenerateToken(CreateJwtSettings(), roles: [Roles.Administrador]));

        // Act
        var response = await client.PostAsJsonAsync("/roles", new CreateRoleRequest("Designer", null));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        problem!.Detail.Should().Be("Nome de role já cadastrado.");
    }

    [Fact]
    public async Task Update_WhenCallerHasRolesManagePermission_ReturnsOkWithRoleResult()
    {
        // Arrange
        var targetId = Guid.NewGuid();
        var roleResult = new RoleDto(targetId, "Designer Sênior", "Nova descrição", false, []);
        var senderMock = new Mock<ISender>();
        senderMock.Setup(s => s.Send(It.IsAny<UpdateRoleCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(roleResult));

        using var host = await StartHostAsync(senderMock.Object);
        var client = host.GetTestClient();
        Authenticate(client, GenerateToken(CreateJwtSettings(), roles: [Roles.Administrador]));

        // Act
        var response = await client.PutAsJsonAsync($"/roles/{targetId}", new UpdateRoleRequest("Designer Sênior", "Nova descrição"));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<RoleDto>();
        body!.Name.Should().Be("Designer Sênior");
    }

    [Fact]
    public async Task UpdatePermissions_WhenCallerHasRolesManagePermission_ReturnsOkWithUpdatedPermissionCodes()
    {
        // Arrange
        var targetId = Guid.NewGuid();
        var roleResult = new RoleDto(targetId, "Designer", null, false, ["briefings.view"]);
        var senderMock = new Mock<ISender>();
        senderMock.Setup(s => s.Send(It.IsAny<UpdateRolePermissionsCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(roleResult));

        using var host = await StartHostAsync(senderMock.Object);
        var client = host.GetTestClient();
        Authenticate(client, GenerateToken(CreateJwtSettings(), roles: [Roles.Administrador]));

        // Act
        var response = await client.PutAsJsonAsync(
            $"/roles/{targetId}/permissions",
            new UpdateRolePermissionsRequest([Guid.NewGuid()]));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<RoleDto>();
        body!.PermissionCodes.Should().ContainSingle().Which.Should().Be("briefings.view");
    }

    [Fact]
    public async Task UpdatePermissions_WhenCallerLacksRolesManagePermission_ReturnsForbidden()
    {
        // Arrange
        var senderMock = new Mock<ISender>();

        using var host = await StartHostAsync(senderMock.Object);
        var client = host.GetTestClient();
        Authenticate(client, GenerateToken(CreateJwtSettings(), roles: [Roles.Designer]));

        // Act
        var response = await client.PutAsJsonAsync(
            $"/roles/{Guid.NewGuid()}/permissions",
            new UpdateRolePermissionsRequest([]));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        senderMock.Verify(s => s.Send(It.IsAny<UpdateRolePermissionsCommand>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GetAll_WhenCallerHasRolesManagePermission_ReturnsOkWithRoleList()
    {
        // Arrange
        var roles = new List<RoleListItemDto> { new(Guid.NewGuid(), "Designer", null, false) };
        var senderMock = new Mock<ISender>();
        senderMock.Setup(s => s.Send(It.IsAny<GetRolesQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success<IReadOnlyCollection<RoleListItemDto>>(roles));

        using var host = await StartHostAsync(senderMock.Object);
        var client = host.GetTestClient();
        Authenticate(client, GenerateToken(CreateJwtSettings(), roles: [Roles.Administrador]));

        // Act
        var response = await client.GetAsync("/roles");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<List<RoleListItemDto>>();
        body.Should().ContainSingle();
    }

    [Fact]
    public async Task GetById_WhenRoleDoesNotExist_ReturnsNotFoundProblem()
    {
        // Arrange
        var senderMock = new Mock<ISender>();
        senderMock.Setup(s => s.Send(It.IsAny<GetRoleByIdQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.NotFound<RoleDto>("Role não encontrada."));

        using var host = await StartHostAsync(senderMock.Object);
        var client = host.GetTestClient();
        Authenticate(client, GenerateToken(CreateJwtSettings(), roles: [Roles.Administrador]));

        // Act
        var response = await client.GetAsync($"/roles/{Guid.NewGuid()}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        problem!.Detail.Should().Be("Role não encontrada.");
    }

    [Fact]
    public async Task GetById_WhenCallerLacksRolesManagePermission_ReturnsForbidden()
    {
        // Arrange
        var senderMock = new Mock<ISender>();

        using var host = await StartHostAsync(senderMock.Object);
        var client = host.GetTestClient();
        Authenticate(client, GenerateToken(CreateJwtSettings(), roles: [Roles.Designer]));

        // Act
        var response = await client.GetAsync($"/roles/{Guid.NewGuid()}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
