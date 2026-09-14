using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using BriefMe.Domain.Common;
using BriefMe.Domain.Enums;
using BriefMe.Domain.Repositories;
using BriefMe.Web.Contracts.Users;
using BriefMe.Web.Tests.TestDoubles;
using BriefMe.Web.ApiClients;
using FluentAssertions;

namespace BriefMe.Web.Tests.Users;

public class UsersApiClientTests
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

    private static (UsersApiClient Client, RoutedStubHandler Handler) CreateSut(Func<HttpRequestMessage, HttpResponseMessage> respond)
    {
        var handler = new RoutedStubHandler(respond);
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api.briefme.test") };
        var client = new UsersApiClient(AuthTestHelpers.CreateApiClient(httpClient));
        return (client, handler);
    }

    private static HttpResponseMessage UsersPageResponse() =>
        new(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(
                new
                {
                    items = new[]
                    {
                        new { userId = Guid.NewGuid(), fullName = "Ana Designer", email = "ana@briefme.test", status = 1, roles = new[] { "Designer" } }
                    },
                    page = 2,
                    pageSize = 10,
                    totalCount = 25
                },
                options: WireJsonOptions)
        };

    private static IReadOnlyDictionary<string, string> ParseQuery(HttpRequestMessage request)
    {
        var query = request.RequestUri!.Query.TrimStart('?');
        if (query.Length == 0)
            return new Dictionary<string, string>();

        return query
            .Split('&', StringSplitOptions.RemoveEmptyEntries)
            .Select(pair => pair.Split('=', 2))
            .ToDictionary(
                parts => Uri.UnescapeDataString(parts[0]),
                parts => parts.Length > 1 ? Uri.UnescapeDataString(parts[1]) : string.Empty);
    }

    private static string QueryValue(HttpRequestMessage request, string key) =>
        ParseQuery(request).TryGetValue(key, out var value) ? value : string.Empty;

    [Fact]
    public async Task GetUsersAsync_WhenAllFiltersProvided_BuildsExpectedQueryString()
    {
        // Arrange
        var roleId = Guid.NewGuid();
        var (client, handler) = CreateSut(_ => UsersPageResponse());
        var query = new UserListQuery(
            Page: 3,
            PageSize: 20,
            Search: "  ana  ",
            Status: UserStatus.Inactive,
            RoleId: roleId,
            SortField: UserSortField.Email,
            SortDescending: true);

        // Act
        await client.GetUsersAsync(query);

        // Assert
        var request = handler.Requests.Single();
        request.RequestUri!.AbsolutePath.Should().Be("/users");
        QueryValue(request, "page").Should().Be("3");
        QueryValue(request, "pageSize").Should().Be("20");
        QueryValue(request, "search").Should().Be("ana");
        QueryValue(request, "status").Should().Be(nameof(UserStatus.Inactive));
        QueryValue(request, "roleId").Should().Be(roleId.ToString());
        QueryValue(request, "sortField").Should().Be(nameof(UserSortField.Email));
        QueryValue(request, "sortDescending").Should().Be("true");
    }

    [Fact]
    public async Task GetUsersAsync_WhenOptionalFiltersOmitted_ExcludesThemFromQueryString()
    {
        // Arrange
        var (client, handler) = CreateSut(_ => UsersPageResponse());
        var query = new UserListQuery(Page: 1, PageSize: 20);

        // Act
        await client.GetUsersAsync(query);

        // Assert
        var request = handler.Requests.Single();
        var parsed = ParseQuery(request);
        parsed.Keys.Should().NotContain(["search", "status", "roleId"]);
        QueryValue(request, "page").Should().Be("1");
        QueryValue(request, "sortDescending").Should().Be("false");
    }

    [Fact]
    public async Task GetUsersAsync_WhenSearchIsWhitespace_OmitsSearchParameter()
    {
        // Arrange
        var (client, handler) = CreateSut(_ => UsersPageResponse());
        var query = new UserListQuery(Page: 1, PageSize: 20, Search: "   ");

        // Act
        await client.GetUsersAsync(query);

        // Assert
        var request = handler.Requests.Single();
        ParseQuery(request).Keys.Should().NotContain("search");
    }

    [Fact]
    public async Task GetUsersAsync_WhenResponseIsSuccess_ReturnsDeserializedPage()
    {
        // Arrange
        var (client, _) = CreateSut(_ => UsersPageResponse());

        // Act
        var result = await client.GetUsersAsync(new UserListQuery());

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Page.Should().Be(2);
        result.Value.PageSize.Should().Be(10);
        result.Value.TotalCount.Should().Be(25);
        result.Value.TotalPages.Should().Be(3);
        result.Value.Items.Should().ContainSingle()
            .Which.Status.Should().Be(UserStatus.Active);
    }

    [Fact]
    public async Task GetUsersAsync_WhenResponseIsNotSuccess_ReturnsFailure()
    {
        // Arrange
        var (client, _) = CreateSut(_ => new HttpResponseMessage(HttpStatusCode.InternalServerError));

        // Act
        var result = await client.GetUsersAsync(new UserListQuery());

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task GetUsersAsync_WhenTransportFails_ReturnsFailure()
    {
        // Arrange
        var (client, _) = CreateSut(_ => throw new HttpRequestException("boom"));

        // Act
        var result = await client.GetUsersAsync(new UserListQuery());

        // Assert
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task GetRolesAsync_WhenResponseIsSuccess_ReturnsRoles()
    {
        // Arrange
        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(
                new[]
                {
                    new { roleId = Guid.NewGuid(), name = "Administrador", description = (string?)null, isSystemRole = true }
                },
                options: WireJsonOptions)
        };
        var (client, handler) = CreateSut(_ => response);

        // Act
        var roles = await client.GetRolesAsync();

        // Assert
        roles.Should().ContainSingle().Which.Name.Should().Be("Administrador");
        handler.Requests.Single().RequestUri!.AbsolutePath.Should().Be("/roles");
    }

    [Fact]
    public async Task GetRolesAsync_WhenForbidden_ReturnsEmptyList()
    {
        // Arrange
        var (client, _) = CreateSut(_ => new HttpResponseMessage(HttpStatusCode.Forbidden));

        // Act
        var roles = await client.GetRolesAsync();

        // Assert
        roles.Should().BeEmpty();
    }

    [Fact]
    public async Task GetRolesAsync_WhenTransportFails_ReturnsEmptyList()
    {
        // Arrange
        var (client, _) = CreateSut(_ => throw new HttpRequestException("boom"));

        // Act
        var roles = await client.GetRolesAsync();

        // Assert
        roles.Should().BeEmpty();
    }

    private static HttpResponseMessage UserResultResponse(HttpStatusCode status, string fullName = "Ana Designer", string email = "ana@briefme.test") =>
        new(status)
        {
            Content = JsonContent.Create(
                new { userId = Guid.NewGuid(), fullName, email, roles = new[] { "Designer" } },
                options: WireJsonOptions)
        };

    private static HttpResponseMessage ValidationProblemResponse(IDictionary<string, string[]> errors) =>
        new(HttpStatusCode.BadRequest)
        {
            Content = JsonContent.Create(
                new { title = "One or more validation errors occurred.", status = 400, errors },
                options: WireJsonOptions)
        };

    private static HttpResponseMessage ProblemResponse(HttpStatusCode status, string detail) =>
        new(status)
        {
            Content = JsonContent.Create(new { status = (int)status, detail }, options: WireJsonOptions)
        };

    [Fact]
    public async Task GetUserAsync_WhenResponseIsSuccess_ReturnsUser()
    {
        // Arrange
        var (client, handler) = CreateSut(_ => UserResultResponse(HttpStatusCode.OK, "Bruno Gestor"));
        var id = Guid.NewGuid();

        // Act
        var result = await client.GetUserAsync(id);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.FullName.Should().Be("Bruno Gestor");
        handler.Requests.Single().RequestUri!.AbsolutePath.Should().Be($"/users/{id}");
    }

    [Fact]
    public async Task GetUserAsync_WhenNotFound_ReturnsNotFoundResult()
    {
        // Arrange
        var (client, _) = CreateSut(_ => ProblemResponse(HttpStatusCode.NotFound, "Usuário não encontrado."));

        // Act
        var result = await client.GetUserAsync(Guid.NewGuid());

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Type.Should().Be(ErrorType.NotFound);
        result.Error.Should().Be("Usuário não encontrado.");
    }

    [Fact]
    public async Task CreateUserAsync_WhenResponseIsCreated_ReturnsSuccess()
    {
        // Arrange
        var (client, handler) = CreateSut(_ => UserResultResponse(HttpStatusCode.Created));
        var request = new CreateUserRequest("Ana Designer", "ana@briefme.test", "Senha123", [Guid.NewGuid()]);

        // Act
        var result = await client.CreateUserAsync(request);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var sent = handler.Requests.Single();
        sent.Method.Should().Be(HttpMethod.Post);
        sent.RequestUri!.AbsolutePath.Should().Be("/users");
    }

    [Fact]
    public async Task CreateUserAsync_WhenValidationProblem_MapsErrorsByField()
    {
        // Arrange
        var errors = new Dictionary<string, string[]>
        {
            ["FullName"] = ["Nome é obrigatório."],
            ["Email"] = ["E-mail em formato inválido."],
            ["Password"] = ["Senha deve conter ao menos um número."]
        };
        var (client, _) = CreateSut(_ => ValidationProblemResponse(errors));
        var request = new CreateUserRequest("", "invalid", "senha", [Guid.NewGuid()]);

        // Act
        var result = await client.CreateUserAsync(request);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Errors.Should().Contain(e => e.PropertyName == "FullName" && e.ErrorMessage == "Nome é obrigatório.");
        result.Errors.Should().Contain(e => e.PropertyName == "Email");
        result.Errors.Should().Contain(e => e.PropertyName == "Password");
    }

    [Fact]
    public async Task CreateUserAsync_WhenEmailConflict_MapsErrorToEmailField()
    {
        // Arrange
        var (client, _) = CreateSut(_ => ProblemResponse(HttpStatusCode.Conflict, "E-mail já cadastrado."));
        var request = new CreateUserRequest("Ana", "ana@briefme.test", "Senha123", [Guid.NewGuid()]);

        // Act
        var result = await client.CreateUserAsync(request);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Errors.Should().ContainSingle()
            .Which.Should().Match<ValidationError>(e => e.PropertyName == "Email" && e.ErrorMessage == "E-mail já cadastrado.");
    }

    [Fact]
    public async Task CreateUserAsync_WhenTransportFails_ReturnsGeneralFailure()
    {
        // Arrange
        var (client, _) = CreateSut(_ => throw new HttpRequestException("boom"));
        var request = new CreateUserRequest("Ana", "ana@briefme.test", "Senha123", [Guid.NewGuid()]);

        // Act
        var result = await client.CreateUserAsync(request);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Errors.Should().BeEmpty();
        result.Error.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task UpdateUserAsync_WhenResponseIsOk_ReturnsSuccess()
    {
        // Arrange
        var (client, handler) = CreateSut(_ => UserResultResponse(HttpStatusCode.OK));
        var id = Guid.NewGuid();
        var request = new UpdateUserRequest("Ana Editada", "ana@briefme.test", [Guid.NewGuid()]);

        // Act
        var result = await client.UpdateUserAsync(id, request);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var sent = handler.Requests.Single();
        sent.Method.Should().Be(HttpMethod.Put);
        sent.RequestUri!.AbsolutePath.Should().Be($"/users/{id}");
    }

    [Fact]
    public async Task UpdateUserAsync_WhenEmailConflict_MapsErrorToEmailField()
    {
        // Arrange
        var (client, _) = CreateSut(_ => ProblemResponse(HttpStatusCode.Conflict, "E-mail já cadastrado."));
        var request = new UpdateUserRequest("Ana", "ana@briefme.test", [Guid.NewGuid()]);

        // Act
        var result = await client.UpdateUserAsync(Guid.NewGuid(), request);

        // Assert
        result.Errors.Should().ContainSingle().Which.PropertyName.Should().Be("Email");
    }

    [Fact]
    public async Task UpdateUserRolesAsync_WhenResponseIsOk_ReturnsPersistedUser()
    {
        // Arrange
        var (client, _) = CreateSut(_ => UserResultResponse(HttpStatusCode.OK, "Ana Designer"));

        // Act
        var result = await client.UpdateUserRolesAsync(Guid.NewGuid(), new UpdateUserRolesRequest([Guid.NewGuid()]));

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Roles.Should().BeEquivalentTo("Designer");
    }

    [Fact]
    public async Task UpdateUserRolesAsync_WhenCalled_PutsRoleIdsToUserRolesRoute()
    {
        // Arrange
        var id = Guid.NewGuid();
        var roleId = Guid.NewGuid();
        var (client, handler) = CreateSut(_ => UserResultResponse(HttpStatusCode.OK));

        // Act
        await client.UpdateUserRolesAsync(id, new UpdateUserRolesRequest([roleId]));

        // Assert
        var sent = handler.Requests.Single();
        sent.Method.Should().Be(HttpMethod.Put);
        sent.RequestUri!.AbsolutePath.Should().Be($"/users/{id}/roles");
        var body = await sent.Content!.ReadFromJsonAsync<UpdateUserRolesRequest>(WireJsonOptions);
        body!.RoleIds.Should().BeEquivalentTo([roleId]);
    }

    [Fact]
    public async Task UpdateUserRolesAsync_WhenNotFound_ReturnsNotFoundResult()
    {
        // Arrange
        var (client, _) = CreateSut(_ => ProblemResponse(HttpStatusCode.NotFound, "Usuário não encontrado."));

        // Act
        var result = await client.UpdateUserRolesAsync(Guid.NewGuid(), new UpdateUserRolesRequest([]));

        // Assert
        result.Type.Should().Be(ErrorType.NotFound);
        result.Error.Should().Be("Usuário não encontrado.");
    }

    [Fact]
    public async Task UpdateUserRolesAsync_WhenBusinessFailure_ReturnsFailureWithDetail()
    {
        // Arrange
        var (client, _) = CreateSut(_ => ProblemResponse(HttpStatusCode.BadRequest, "Uma ou mais roles informadas não existem."));

        // Act
        var result = await client.UpdateUserRolesAsync(Guid.NewGuid(), new UpdateUserRolesRequest([Guid.NewGuid()]));

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("Uma ou mais roles informadas não existem.");
    }

    [Fact]
    public async Task UpdateUserRolesAsync_WhenTransportFails_ReturnsGeneralFailure()
    {
        // Arrange
        var (client, _) = CreateSut(_ => throw new HttpRequestException("network down"));

        // Act
        var result = await client.UpdateUserRolesAsync(Guid.NewGuid(), new UpdateUserRolesRequest([]));

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("Não foi possível salvar os perfis do usuário. Tente novamente.");
    }
}
