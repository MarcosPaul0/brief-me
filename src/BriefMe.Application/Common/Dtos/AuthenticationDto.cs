using BriefMe.Domain.Entities;

namespace BriefMe.Application.Common.Dtos;

public sealed record AuthenticationDto(
    string AccessToken,
    string RefreshToken,
    int ExpiresIn,
    Guid UserId,
    string FullName,
    string Email,
    IReadOnlyCollection<string> Roles)
{
    public static AuthenticationDto FromUserAndToken(User user, string accessToken, DateTime accessTokenExpiresAt, string refreshToken)
    {
        var roles = user.UserRoles
            .Where(ur => ur.Role is not null)
            .Select(ur => ur.Role!.Name)
            .ToList();

        var expiresIn = (int)Math.Max(0, (accessTokenExpiresAt - DateTime.UtcNow).TotalSeconds);

        return new AuthenticationDto(accessToken, refreshToken, expiresIn, user.Id, user.FullName, user.Email.Value, roles);
    }
}
