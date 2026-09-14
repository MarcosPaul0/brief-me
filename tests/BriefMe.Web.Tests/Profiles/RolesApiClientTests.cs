using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using BriefMe.Domain.Common;
using BriefMe.Web.Contracts.Roles;
using BriefMe.Web.ApiClients;
using BriefMe.Web.Tests.TestDoubles;
using FluentAssertions;

namespace BriefMe.Web.Tests.Profiles;

public class RolesApiClientTests
{
    private static readonly JsonSerializerOptions WireJsonOptions = new(JsonSerializerDefaults.Web);

    private sealed class RoutedStubHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        public List<HttpRequestMessage> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            return Task.FromResult(respond(request));
        }
    }

    private static (RolesApiClient Client, RoutedStubHandler Handler) CreateSut(Func<HttpRequestMessage, HttpResponseMessage> respond)
    {
        var handler = new RoutedStubHandler(respond);
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api.briefme.test") };
        var client = new RolesApiClient(AuthTestHelpers.CreateApiClient(httpClient));
        return (client, handler);
    }

    private static HttpResponseMessage JsonResponse(HttpStatusCode statusCode, object payload) =>
        new(statusCode) { Content = JsonContent.Create(payload, options: WireJsonOptions) };

    private static HttpResponseMessage ProblemResponse(HttpStatusCode statusCode, string detail) =>
        JsonResponse(statusCode, new { status = (int)statusCode, detail });

    private static HttpResponseMessage RoleResultResponse(Guid roleId, params string[] permissionCodes) =>
        JsonResponse(HttpStatusCode.OK, new
        {
            roleId,
            name = "Designer",
            description = "Perfil de execução",
            isSystemRole = true,
            permissionCodes
        });

    [Fact]
    public async Task GetRolesAsync_WhenResponseIsSuccess_ReturnsRoles()
    {
        // Arrange
        var roleId = Guid.NewGuid();
        var (client, handler) = CreateSut(_ => JsonResponse(HttpStatusCode.OK, new[]
        {
            new { roleId, name = "Administrador", description = "Acesso total", isSystemRole = true }
        }));

        // Act
        var result = await client.GetRolesAsync();

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().ContainSingle()
            .Which.Should().BeEquivalentTo(new { RoleId = roleId, Name = "Administrador", IsSystemRole = true });
        handler.Requests.Single().RequestUri!.AbsolutePath.Should().Be("/roles");
    }

    [Fact]
    public async Task GetRolesAsync_WhenForbidden_ReturnsForbiddenFailure()
    {
        // Arrange
        var (client, _) = CreateSut(_ => ProblemResponse(HttpStatusCode.Forbidden, "Sem permissão."));

        // Act
        var result = await client.GetRolesAsync();

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Type.Should().Be(ErrorType.Forbidden);
        result.Error.Should().Be("Sem permissão.");
    }

    [Fact]
    public async Task GetRolesAsync_WhenTransportFails_ReturnsGeneralFailure()
    {
        // Arrange
        var (client, _) = CreateSut(_ => throw new HttpRequestException("network down"));

        // Act
        var result = await client.GetRolesAsync();

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Type.Should().Be(ErrorType.Failure);
        result.Error.Should().Be("Não foi possível carregar os perfis. Tente novamente.");
    }

    [Fact]
    public async Task GetRoleAsync_WhenResponseIsSuccess_ReturnsRoleWithPermissionCodes()
    {
        // Arrange
        var roleId = Guid.NewGuid();
        var (client, handler) = CreateSut(_ => RoleResultResponse(roleId, "briefings.create", "briefings.view"));

        // Act
        var result = await client.GetRoleAsync(roleId);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.PermissionCodes.Should().BeEquivalentTo("briefings.create", "briefings.view");
        handler.Requests.Single().RequestUri!.AbsolutePath.Should().Be($"/roles/{roleId}");
    }

    [Fact]
    public async Task GetRoleAsync_WhenNotFound_ReturnsNotFoundResult()
    {
        // Arrange
        var (client, _) = CreateSut(_ => ProblemResponse(HttpStatusCode.NotFound, "Role não encontrada."));

        // Act
        var result = await client.GetRoleAsync(Guid.NewGuid());

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Type.Should().Be(ErrorType.NotFound);
        result.Error.Should().Be("Role não encontrada.");
    }

    [Fact]
    public async Task UpdateRolePermissionsAsync_WhenResponseIsOk_ReturnsPersistedRole()
    {
        // Arrange
        var roleId = Guid.NewGuid();
        var (client, _) = CreateSut(_ => RoleResultResponse(roleId, "briefings.create"));

        // Act
        var result = await client.UpdateRolePermissionsAsync(roleId, new UpdateRolePermissionsRequest([Guid.NewGuid()]));

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.PermissionCodes.Should().BeEquivalentTo("briefings.create");
    }

    [Fact]
    public async Task UpdateRolePermissionsAsync_WhenCalled_PutsPermissionIdsToRoleRoute()
    {
        // Arrange
        var roleId = Guid.NewGuid();
        var permissionId = Guid.NewGuid();
        var (client, handler) = CreateSut(_ => RoleResultResponse(roleId));

        // Act
        await client.UpdateRolePermissionsAsync(roleId, new UpdateRolePermissionsRequest([permissionId]));

        // Assert
        var request = handler.Requests.Single();
        request.Method.Should().Be(HttpMethod.Put);
        request.RequestUri!.AbsolutePath.Should().Be($"/roles/{roleId}/permissions");
        var body = await request.Content!.ReadFromJsonAsync<UpdateRolePermissionsRequest>(WireJsonOptions);
        body!.PermissionIds.Should().BeEquivalentTo([permissionId]);
    }

    [Fact]
    public async Task UpdateRolePermissionsAsync_WhenBusinessFailure_ReturnsFailureWithDetail()
    {
        // Arrange
        var (client, _) = CreateSut(_ => ProblemResponse(HttpStatusCode.BadRequest, "Uma ou mais permissions informadas não existem."));

        // Act
        var result = await client.UpdateRolePermissionsAsync(Guid.NewGuid(), new UpdateRolePermissionsRequest([]));

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("Uma ou mais permissions informadas não existem.");
    }

    [Fact]
    public async Task UpdateRolePermissionsAsync_WhenValidationProblem_MapsErrorsByField()
    {
        // Arrange
        var (client, _) = CreateSut(_ => JsonResponse(HttpStatusCode.BadRequest, new
        {
            status = 400,
            detail = "Validation failed.",
            errors = new Dictionary<string, string[]> { ["PermissionIds"] = ["Lista inválida."] }
        }));

        // Act
        var result = await client.UpdateRolePermissionsAsync(Guid.NewGuid(), new UpdateRolePermissionsRequest([]));

        // Assert
        result.Errors.Should().ContainSingle()
            .Which.Should().BeEquivalentTo(new ValidationError("PermissionIds", "Lista inválida."));
    }

    [Fact]
    public async Task UpdateRolePermissionsAsync_WhenTransportFails_ReturnsGeneralFailure()
    {
        // Arrange
        var (client, _) = CreateSut(_ => throw new HttpRequestException("network down"));

        // Act
        var result = await client.UpdateRolePermissionsAsync(Guid.NewGuid(), new UpdateRolePermissionsRequest([]));

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("Não foi possível salvar as permissões do perfil. Tente novamente.");
    }
}
