using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using BriefMe.Domain.Common;
using BriefMe.Domain.Enums;
using BriefMe.Web.ApiClients;
using BriefMe.Web.Tests.TestDoubles;
using FluentAssertions;

namespace BriefMe.Web.Tests.Profile;

public class ProfileApiClientTests
{
    private static readonly JsonSerializerOptions WireJsonOptions = new(JsonSerializerDefaults.Web);

    private sealed class CapturingHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        public HttpRequestMessage? LastRequest { get; private set; }

        public string? LastBody { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequest = request;
            LastBody = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
            return respond(request);
        }
    }

    private static (ProfileApiClient Client, CapturingHandler Handler) CreateSut(Func<HttpRequestMessage, HttpResponseMessage> respond)
    {
        var handler = new CapturingHandler(respond);
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api.briefme.test") };
        return (new ProfileApiClient(AuthTestHelpers.CreateApiClient(httpClient)), handler);
    }

    private static HttpResponseMessage ProfileResponse(Guid? avatarVersion) =>
        new(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(
                new { userId = Guid.NewGuid(), fullName = "Ana", email = "ana@briefme.com", role = (int)UserRole.Requester, avatarVersion },
                options: WireJsonOptions)
        };

    [Fact]
    public async Task UploadAvatarAsync_WhenCalled_PutsMultipartFileToMeAvatar()
    {
        // Arrange
        var (client, handler) = CreateSut(_ => ProfileResponse(Guid.NewGuid()));

        // Act
        await client.UploadAvatarAsync([1, 2, 3], "foto.png", "image/png");

        // Assert
        new
        {
            Method = handler.LastRequest!.Method,
            Path = handler.LastRequest.RequestUri!.AbsolutePath,
            MediaType = handler.LastRequest.Content!.Headers.ContentType!.MediaType,
            HasFileField = handler.LastBody!.Contains("name=file") && handler.LastBody.Contains("filename=foto.png")
        }.Should().BeEquivalentTo(new { Method = HttpMethod.Put, Path = "/users/me/avatar", MediaType = "multipart/form-data", HasFileField = true });
    }

    [Fact]
    public async Task UploadAvatarAsync_WhenApiAccepts_ReturnsProfileWithAvatarVersion()
    {
        // Arrange
        var version = Guid.NewGuid();
        var (client, _) = CreateSut(_ => ProfileResponse(version));

        // Act
        var result = await client.UploadAvatarAsync([1, 2, 3], "foto.png", "image/png");

        // Assert
        result.Value.AvatarVersion.Should().Be(version);
    }

    [Fact]
    public async Task UploadAvatarAsync_WhenApiRejectsFormat_ReturnsProblemDetail()
    {
        // Arrange
        var (client, _) = CreateSut(_ => new HttpResponseMessage(HttpStatusCode.BadRequest)
        {
            Content = JsonContent.Create(new { status = 400, detail = "Formato de imagem inválido. Use PNG, JPEG ou WebP." }, options: WireJsonOptions)
        });

        // Act
        var result = await client.UploadAvatarAsync([1, 2, 3], "foto.gif", "image/gif");

        // Assert
        result.Error.Should().Be("Formato de imagem inválido. Use PNG, JPEG ou WebP.");
    }

    [Fact]
    public async Task RemoveAvatarAsync_WhenApiAccepts_ReturnsProfileWithoutAvatar()
    {
        // Arrange
        var (client, handler) = CreateSut(_ => ProfileResponse(null));

        // Act
        var result = await client.RemoveAvatarAsync();

        // Assert
        new { handler.LastRequest!.Method, result.Value.AvatarVersion }
            .Should().BeEquivalentTo(new { Method = HttpMethod.Delete, AvatarVersion = (Guid?)null });
    }

    [Fact]
    public async Task GetMyProfileAsync_WhenServerFails_ReturnsFailure()
    {
        // Arrange
        var (client, _) = CreateSut(_ => new HttpResponseMessage(HttpStatusCode.InternalServerError));

        // Act
        var result = await client.GetMyProfileAsync();

        // Assert
        result.IsFailure.Should().BeTrue();
    }
}
