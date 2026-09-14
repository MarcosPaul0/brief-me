using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using BriefMe.Domain.Common;
using BriefMe.Web.ApiClients;
using BriefMe.Web.Tests.TestDoubles;
using FluentAssertions;

namespace BriefMe.Web.Tests.PermissionCatalog;

public class PermissionsApiClientTests
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

    private static (PermissionsApiClient Client, RoutedStubHandler Handler) CreateSut(Func<HttpRequestMessage, HttpResponseMessage> respond)
    {
        var handler = new RoutedStubHandler(respond);
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api.briefme.test") };
        var client = new PermissionsApiClient(AuthTestHelpers.CreateApiClient(httpClient));
        return (client, handler);
    }

    private static HttpResponseMessage JsonResponse(HttpStatusCode statusCode, object payload) =>
        new(statusCode) { Content = JsonContent.Create(payload, options: WireJsonOptions) };

    [Fact]
    public async Task GetPermissionGroupsAsync_WhenResponseIsSuccess_ReturnsGroupedCatalog()
    {
        // Arrange
        var permissionId = Guid.NewGuid();
        var (client, handler) = CreateSut(_ => JsonResponse(HttpStatusCode.OK, new[]
        {
            new
            {
                group = "Usuários",
                permissions = new[]
                {
                    new { permissionId, code = "users.create", name = "Criar usuários", description = "Cadastra novos usuários" }
                }
            }
        }));

        // Act
        var result = await client.GetPermissionGroupsAsync();

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().ContainSingle().Which.Permissions.Should().ContainSingle()
            .Which.Should().BeEquivalentTo(new { PermissionId = permissionId, Code = "users.create", Name = "Criar usuários" });
        handler.Requests.Single().RequestUri!.AbsolutePath.Should().Be("/permissions");
    }

    [Fact]
    public async Task GetPermissionGroupsAsync_WhenCatalogIsEmpty_ReturnsEmptySuccess()
    {
        // Arrange
        var (client, _) = CreateSut(_ => JsonResponse(HttpStatusCode.OK, Array.Empty<object>()));

        // Act
        var result = await client.GetPermissionGroupsAsync();

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEmpty();
    }

    [Fact]
    public async Task GetPermissionGroupsAsync_WhenForbidden_ReturnsForbiddenFailure()
    {
        // Arrange
        var (client, _) = CreateSut(_ => JsonResponse(HttpStatusCode.Forbidden, new { status = 403, detail = "Requer permissions.manage." }));

        // Act
        var result = await client.GetPermissionGroupsAsync();

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Type.Should().Be(ErrorType.Forbidden);
        result.Error.Should().Be("Requer permissions.manage.");
    }

    [Fact]
    public async Task GetPermissionGroupsAsync_WhenTransportFails_ReturnsGeneralFailure()
    {
        // Arrange
        var (client, _) = CreateSut(_ => throw new HttpRequestException("network down"));

        // Act
        var result = await client.GetPermissionGroupsAsync();

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Type.Should().Be(ErrorType.Failure);
        result.Error.Should().Be("Não foi possível carregar o catálogo de permissões. Tente novamente.");
    }
}
