using System.Net.Http.Headers;
using BriefMe.Application.Common.Dtos;
using BriefMe.Domain.Common;
using BriefMe.Web.Http;

namespace BriefMe.Web.ApiClients;

public sealed class ProfileApiClient(AuthenticatedApiClient api) : IProfileApiClient
{
    private const string LoadProfileFailureMessage = "Não foi possível carregar o seu perfil. Tente novamente.";
    private const string UploadAvatarFailureMessage = "Não foi possível enviar a foto. Tente novamente.";
    private const string RemoveAvatarFailureMessage = "Não foi possível remover a foto. Tente novamente.";

    public Task<Result<UserDto>> GetMyProfileAsync(CancellationToken cancellationToken = default) =>
        api.GetResultAsync<UserDto, UserDto>("users/me", LoadProfileFailureMessage, cancellationToken);

    // Takes the bytes rather than a stream: the refresh-on-401 retry rebuilds the content, so it must be
    // producible twice (AuthenticatedApiClient.PutAsync).
    public Task<Result<UserDto>> UploadAvatarAsync(byte[] content, string fileName, string contentType, CancellationToken cancellationToken = default) =>
        api.SendForResultAsync<UserDto>(
            client => client.PutAsync("users/me/avatar", () => BuildForm(content, fileName, contentType), cancellationToken),
            UploadAvatarFailureMessage,
            cancellationToken);

    public Task<Result<UserDto>> RemoveAvatarAsync(CancellationToken cancellationToken = default) =>
        api.SendForResultAsync<UserDto>(
            client => client.DeleteAsync("users/me/avatar", cancellationToken),
            RemoveAvatarFailureMessage,
            cancellationToken);

    private static MultipartFormDataContent BuildForm(byte[] content, string fileName, string contentType)
    {
        var file = new ByteArrayContent(content);
        file.Headers.ContentType = MediaTypeHeaderValue.TryParse(contentType, out var mediaType)
            ? mediaType
            : new MediaTypeHeaderValue("application/octet-stream");

        return new MultipartFormDataContent { { file, "file", fileName } };
    }
}
