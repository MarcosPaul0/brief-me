using BriefMe.Application.Abstractions.Messaging;
using BriefMe.Application.Abstractions;
using BriefMe.Application.Common.Dtos;
using BriefMe.Application.UseCases.Users.ChangePassword;
using BriefMe.Application.UseCases.Users.ChangeUserStatus;
using BriefMe.Application.UseCases.Users.CreateUser;
using BriefMe.Application.UseCases.Users.DeleteUser;
using BriefMe.Application.UseCases.Users.GetUserById;
using BriefMe.Application.UseCases.Users.GetUsersPaged;
using BriefMe.Application.UseCases.Users.UpdateUser;
using BriefMe.Application.UseCases.Users.UpdateUserRoles;
using BriefMe.Application.UseCases.Users;
using BriefMe.Domain.Common;
using BriefMe.Domain.Constants;
using BriefMe.Domain.Enums;
using BriefMe.Infrastructure.Auth;
using BriefMe.Web.Auth;
using BriefMe.Web.Contracts.Users;
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

public class UsersControllerTests
{
    private const string SecretKey = "web-tests-users-controller-secret-key-32b!!";

    private static JwtSettings CreateJwtSettings() => new()
    {
        Issuer = "BriefMe.Web.Tests",
        Audience = "BriefMe.Web.Tests.Audience",
        SecretKey = SecretKey,
        AccessTokenMinutes = 15
    };

    // Builds a token directly from claims (rather than JwtService + domain entities) because these
    // tests target the Web-layer authorization pipeline (permission policies, self-service bypass),
    // not JwtService's claim-population logic — which depends on EF-fixed-up navigation properties
    // that a bare in-memory User/Role graph never populates.
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
                    services.AddControllers().AddApplicationPart(typeof(UsersController).Assembly);
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
    public async Task Create_WhenCallerHasUsersCreatePermission_ReturnsCreatedWithLocation()
    {
        // Arrange
        var createdUserId = Guid.NewGuid();
        var userResult = new UserDto(createdUserId, "Novo Usuário", "novo@briefme.com", ["Designer"]);
        var senderMock = new Mock<ISender>();
        senderMock.Setup(s => s.Send(It.IsAny<CreateUserCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(userResult));

        using var host = await StartHostAsync(senderMock.Object);
        var client = host.GetTestClient();
        Authenticate(client, GenerateToken(CreateJwtSettings(), Guid.NewGuid(), roles: [Roles.Administrador]));

        // Act
        var response = await client.PostAsJsonAsync(
            "/users",
            new CreateUserRequest("Novo Usuário", "novo@briefme.com", "Senha123!", [Guid.NewGuid()]));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        response.Headers.Location!.ToString().Should().Contain($"/users/{createdUserId}");
        var body = await response.Content.ReadFromJsonAsync<UserDto>();
        body!.UserId.Should().Be(createdUserId);
    }

    [Fact]
    public async Task Create_WhenCallerLacksUsersCreatePermission_ReturnsForbidden()
    {
        // Arrange
        var senderMock = new Mock<ISender>();

        using var host = await StartHostAsync(senderMock.Object);
        var client = host.GetTestClient();
        Authenticate(client, GenerateToken(CreateJwtSettings(), Guid.NewGuid(), roles: [Roles.Designer]));

        // Act
        var response = await client.PostAsJsonAsync(
            "/users",
            new CreateUserRequest("Novo Usuário", "novo@briefme.com", "Senha123!", []));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        senderMock.Verify(s => s.Send(It.IsAny<CreateUserCommand>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Create_WhenEmailAlreadyExists_ReturnsConflictProblem()
    {
        // Arrange
        var senderMock = new Mock<ISender>();
        senderMock.Setup(s => s.Send(It.IsAny<CreateUserCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Conflict<UserDto>("E-mail já cadastrado."));

        using var host = await StartHostAsync(senderMock.Object);
        var client = host.GetTestClient();
        Authenticate(client, GenerateToken(CreateJwtSettings(), Guid.NewGuid(), roles: [Roles.Administrador]));

        // Act
        var response = await client.PostAsJsonAsync(
            "/users",
            new CreateUserRequest("Novo Usuário", "novo@briefme.com", "Senha123!", []));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        problem!.Detail.Should().Be("E-mail já cadastrado.");
    }

    [Fact]
    public async Task Update_WhenCallerHasUsersUpdatePermission_ReturnsOkWithUserResult()
    {
        // Arrange
        var targetId = Guid.NewGuid();
        var userResult = new UserDto(targetId, "Ana Editada", "ana.editada@briefme.com", ["Designer"]);
        var senderMock = new Mock<ISender>();
        senderMock.Setup(s => s.Send(It.IsAny<UpdateUserCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(userResult));

        using var host = await StartHostAsync(senderMock.Object);
        var client = host.GetTestClient();
        Authenticate(client, GenerateToken(CreateJwtSettings(), Guid.NewGuid(), roles: [Roles.Administrador]));

        // Act
        var response = await client.PutAsJsonAsync(
            $"/users/{targetId}",
            new UpdateUserRequest("Ana Editada", "ana.editada@briefme.com", []));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<UserDto>();
        body!.FullName.Should().Be("Ana Editada");
    }

    [Fact]
    public async Task UpdateRoles_WhenCallerHasRolesManagePermission_ReturnsOkWithUpdatedRoles()
    {
        // Arrange
        var targetId = Guid.NewGuid();
        var userResult = new UserDto(targetId, "Ana Silva", "ana@briefme.com", ["Gestor de Design"]);
        var senderMock = new Mock<ISender>();
        senderMock.Setup(s => s.Send(It.IsAny<UpdateUserRolesCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(userResult));

        using var host = await StartHostAsync(senderMock.Object);
        var client = host.GetTestClient();
        Authenticate(client, GenerateToken(CreateJwtSettings(), Guid.NewGuid(), roles: [Roles.Administrador]));

        // Act
        var response = await client.PutAsJsonAsync($"/users/{targetId}/roles", new UpdateUserRolesRequest([Guid.NewGuid()]));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<UserDto>();
        body!.Roles.Should().ContainSingle().Which.Should().Be("Gestor de Design");
    }

    [Fact]
    public async Task UpdateRoles_WhenCallerLacksRolesManagePermission_ReturnsForbidden()
    {
        // Arrange
        var senderMock = new Mock<ISender>();

        using var host = await StartHostAsync(senderMock.Object);
        var client = host.GetTestClient();
        Authenticate(client, GenerateToken(CreateJwtSettings(), Guid.NewGuid(), roles: [Roles.Designer]));

        // Act
        var response = await client.PutAsJsonAsync($"/users/{Guid.NewGuid()}/roles", new UpdateUserRolesRequest([]));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        senderMock.Verify(s => s.Send(It.IsAny<UpdateUserRolesCommand>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task UpdateRoles_WhenUserDoesNotExist_ReturnsNotFoundProblem()
    {
        // Arrange
        var senderMock = new Mock<ISender>();
        senderMock.Setup(s => s.Send(It.IsAny<UpdateUserRolesCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.NotFound<UserDto>("Usuário não encontrado."));

        using var host = await StartHostAsync(senderMock.Object);
        var client = host.GetTestClient();
        Authenticate(client, GenerateToken(CreateJwtSettings(), Guid.NewGuid(), roles: [Roles.Administrador]));

        // Act
        var response = await client.PutAsJsonAsync($"/users/{Guid.NewGuid()}/roles", new UpdateUserRolesRequest([]));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        problem!.Detail.Should().Be("Usuário não encontrado.");
    }

    [Fact]
    public async Task Delete_WhenCallerHasUsersDeletePermission_ReturnsNoContent()
    {
        // Arrange
        var senderMock = new Mock<ISender>();
        senderMock.Setup(s => s.Send(It.IsAny<DeleteUserCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success());

        using var host = await StartHostAsync(senderMock.Object);
        var client = host.GetTestClient();
        Authenticate(client, GenerateToken(CreateJwtSettings(), Guid.NewGuid(), roles: [Roles.Administrador]));

        // Act
        var response = await client.DeleteAsync($"/users/{Guid.NewGuid()}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task GetById_WhenUserDoesNotExist_ReturnsNotFoundProblem()
    {
        // Arrange
        var senderMock = new Mock<ISender>();
        senderMock.Setup(s => s.Send(It.IsAny<GetUserByIdQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.NotFound<UserDto>("Usuário não encontrado."));

        using var host = await StartHostAsync(senderMock.Object);
        var client = host.GetTestClient();
        Authenticate(client, GenerateToken(CreateJwtSettings(), Guid.NewGuid(), roles: [Roles.Administrador]));

        // Act
        var response = await client.GetAsync($"/users/{Guid.NewGuid()}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        problem!.Detail.Should().Be("Usuário não encontrado.");
    }

    [Fact]
    public async Task GetById_WhenCallerLacksUsersViewPermission_ReturnsForbidden()
    {
        // Arrange
        var senderMock = new Mock<ISender>();

        using var host = await StartHostAsync(senderMock.Object);
        var client = host.GetTestClient();
        Authenticate(client, GenerateToken(CreateJwtSettings(), Guid.NewGuid(), roles: [Roles.Designer]));

        // Act
        var response = await client.GetAsync($"/users/{Guid.NewGuid()}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task GetPaged_WhenCallerHasUsersViewPermission_MapsQueryParamsAndReturnsOk()
    {
        // Arrange
        GetUsersPagedQuery? capturedQuery = null;
        var pagedResult = PagedDto<UserListItemDto>.Create([], page: 2, pageSize: 10, totalCount: 0);
        var senderMock = new Mock<ISender>();
        senderMock.Setup(s => s.Send(It.IsAny<GetUsersPagedQuery>(), It.IsAny<CancellationToken>()))
            .Callback<IRequest<Result<PagedDto<UserListItemDto>>>, CancellationToken>((q, _) => capturedQuery = (GetUsersPagedQuery)q)
            .ReturnsAsync(Result.Success(pagedResult));

        using var host = await StartHostAsync(senderMock.Object);
        var client = host.GetTestClient();
        Authenticate(client, GenerateToken(CreateJwtSettings(), Guid.NewGuid(), roles: [Roles.Administrador]));

        // Act
        var response = await client.GetAsync("/users?page=2&pageSize=10&search=ana&status=Active");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        capturedQuery.Should().NotBeNull();
        capturedQuery!.Page.Should().Be(2);
        capturedQuery.PageSize.Should().Be(10);
        capturedQuery.Search.Should().Be("ana");
        capturedQuery.Status.Should().Be(UserStatus.Active);
    }

    [Fact]
    public async Task ChangeStatus_WhenCallerHasUsersManagePermission_ReturnsNoContent()
    {
        // Arrange
        var senderMock = new Mock<ISender>();
        senderMock.Setup(s => s.Send(It.IsAny<ChangeUserStatusCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success());

        using var host = await StartHostAsync(senderMock.Object);
        var client = host.GetTestClient();
        Authenticate(client, GenerateToken(CreateJwtSettings(), Guid.NewGuid(), roles: [Roles.Administrador]));

        // Act
        var response = await client.PatchAsync(
            $"/users/{Guid.NewGuid()}/status",
            JsonContent.Create(new ChangeUserStatusRequest(UserStatus.Inactive)));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task ChangePassword_WhenCallerChangesOwnPasswordWithoutResetPermission_ReturnsNoContent()
    {
        // Arrange
        var selfId = Guid.NewGuid();
        var senderMock = new Mock<ISender>();
        senderMock.Setup(s => s.Send(It.IsAny<ChangePasswordCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success());

        using var host = await StartHostAsync(senderMock.Object);
        var client = host.GetTestClient();
        Authenticate(client, GenerateToken(CreateJwtSettings(), selfId, roles: [Roles.Solicitante]));

        // Act
        var response = await client.PatchAsync(
            $"/users/{selfId}/password",
            JsonContent.Create(new ChangePasswordRequest("SenhaAtual123!", "SenhaNova123!")));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task ChangePassword_WhenCallerChangesAnotherUsersPasswordWithoutResetPermission_ReturnsForbidden()
    {
        // Arrange
        var senderMock = new Mock<ISender>();

        using var host = await StartHostAsync(senderMock.Object);
        var client = host.GetTestClient();
        Authenticate(client, GenerateToken(CreateJwtSettings(), Guid.NewGuid(), roles: [Roles.Solicitante]));

        // Act
        var response = await client.PatchAsync(
            $"/users/{Guid.NewGuid()}/password",
            JsonContent.Create(new ChangePasswordRequest(null, "SenhaNova123!")));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        senderMock.Verify(s => s.Send(It.IsAny<ChangePasswordCommand>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ChangePassword_WhenCallerHasUsersResetPasswordPermissionForAnotherUser_ReturnsNoContent()
    {
        // Arrange
        var senderMock = new Mock<ISender>();
        senderMock.Setup(s => s.Send(It.IsAny<ChangePasswordCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success());

        using var host = await StartHostAsync(senderMock.Object);
        var client = host.GetTestClient();
        Authenticate(client, GenerateToken(CreateJwtSettings(), Guid.NewGuid(), permissions: ["users.reset-password"]));

        // Act
        var response = await client.PatchAsync(
            $"/users/{Guid.NewGuid()}/password",
            JsonContent.Create(new ChangePasswordRequest(null, "SenhaNova123!")));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }
}
