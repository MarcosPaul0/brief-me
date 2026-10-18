using BriefMe.Application.Common.Dtos;
using BriefMe.Domain.Common;

namespace BriefMe.Web.ApiClients;

/// <summary>
/// The signed-in user's own profile (<c>/users/me</c>), for every role: the header and the profile page.
/// Kept apart from <see cref="IUsersApiClient"/>, which is the admin's user management.
/// </summary>
public interface IProfileApiClient
{
    Task<Result<UserDto>> GetMyProfileAsync(CancellationToken cancellationToken = default);

    /// <summary>Uploads the avatar via <c>PUT /users/me/avatar</c> (multipart field <c>file</c>).</summary>
    Task<Result<UserDto>> UploadAvatarAsync(byte[] content, string fileName, string contentType, CancellationToken cancellationToken = default);

    Task<Result<UserDto>> RemoveAvatarAsync(CancellationToken cancellationToken = default);
}
