using BriefMe.Domain.Entities;

namespace BriefMe.Application.Common.Dtos;

public sealed record UserDto(
    Guid UserId,
    string FullName,
    string Email,
    IReadOnlyCollection<string> Roles)
{
    public static UserDto FromUserAndRoles(User user, IReadOnlyList<Role> roles)
    {
        return new UserDto(
            user.Id,
            user.FullName,
            user.Email.Value,
            [.. roles.Select(r => r.Name)]);
    }
}
