using BriefMe.Domain.Entities;
using BriefMe.Domain.Enums;

namespace BriefMe.Application.Common.Dtos;

/// <summary>Read model of a user row in the paged user list. <c>AvatarVersion</c> is null when there is no avatar.</summary>
public sealed record UserListItemDto(
    Guid UserId,
    string FullName,
    string Email,
    UserStatus Status,
    UserRole Role,
    Guid? AvatarVersion)
{
    public static UserListItemDto FromUser(User user) =>
        new(user.Id, user.FullName, user.Email.Value, user.Status, user.Role, user.AvatarVersion);
}
