using BriefMe.Application.Common.Dtos;
using BriefMe.Domain.Common;
using BriefMe.Web.ApiClients;

namespace BriefMe.Web.Components.Users;

/// <summary>
/// Circuit-scoped copy of the signed-in user's profile, shared by the header and the profile page so an avatar
/// change shows up in both without a reload (AVT-06). Keyed by user id: sign-out and sign-in happen inside the
/// same circuit, so a profile belonging to someone else is reloaded rather than reused.
/// </summary>
public sealed class CurrentUserProfile(IProfileApiClient profileApi)
{
    private Task<Result<UserDto>>? _loading;
    private Guid? _loadingFor;

    public UserDto? Profile { get; private set; }

    /// <summary>Raised whenever <see cref="Profile"/> changes; subscribers re-render.</summary>
    public event Action? Changed;

    /// <summary>Loads the profile once per user; concurrent callers share the same request.</summary>
    public async Task<Result<UserDto>> EnsureLoadedAsync(Guid userId)
    {
        if (Profile?.UserId == userId)
            return Result.Success(Profile);

        if (_loading is null || _loadingFor != userId)
        {
            _loadingFor = userId;
            _loading = profileApi.GetMyProfileAsync();
        }

        var result = await _loading;
        if (result.IsSuccess && _loadingFor == userId)
            Update(result.Value);
        else if (result.IsFailure)
            _loading = null;

        return result;
    }

    public void Update(UserDto profile)
    {
        Profile = profile;
        Changed?.Invoke();
    }
}
