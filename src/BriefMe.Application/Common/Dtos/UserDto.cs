using BriefMe.Domain.Entities;
using BriefMe.Domain.Enums;

namespace BriefMe.Application.Common.Dtos;

/// <summary>
/// A user's profile. <c>AvatarVersion</c> is null when the user has not uploaded an avatar (the UI then shows
/// the initial); otherwise it changes on every upload and goes in the image URL to defeat caching.
/// </summary>
public sealed record UserDto(
    Guid UserId,
    string FullName,
    string Email,
    UserRole Role,
    Guid? AvatarVersion)
{
    public static UserDto FromUser(User user) =>
        new(user.Id, user.FullName, user.Email.Value, user.Role, user.AvatarVersion);
}
