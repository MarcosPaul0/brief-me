using BriefMe.Domain.Entities;
using BriefMe.Domain.Enums;

namespace BriefMe.Application.Common.Dtos;

public sealed record UserDto(
    Guid UserId,
    string FullName,
    string Email,
    UserRole Role)
{
    public static UserDto FromUser(User user) =>
        new(user.Id, user.FullName, user.Email.Value, user.Role);
}
