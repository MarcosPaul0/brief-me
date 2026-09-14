using BriefMe.Domain.Entities;
using BriefMe.Domain.Enums;

namespace BriefMe.Application.Common.Dtos;

/// <summary>
/// Read model of a user row in the paged user list. Role names are resolved server-side and passed in by the
/// handler as a lookup — a role that vanished between the two queries drops from that row's badges rather
/// than throwing and killing the whole page.
/// </summary>
public sealed record UserListItemDto(
    Guid UserId,
    string FullName,
    string Email,
    UserStatus Status,
    IReadOnlyCollection<string> Roles)
{
    public static UserListItemDto FromUserWithRoleNames(User user, IReadOnlyDictionary<Guid, string> roleNames)
    {
        return new(
            user.Id,
            user.FullName,
            user.Email.Value,
            user.Status,
            [.. user.UserRoles
                .Select(userRole => roleNames.GetValueOrDefault(userRole.RoleId))
                .OfType<string>()]);
    }
}
