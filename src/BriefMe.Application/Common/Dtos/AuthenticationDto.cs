using BriefMe.Domain.Entities;
using BriefMe.Domain.Enums;

namespace BriefMe.Application.Common.Dtos;

public sealed record AuthenticationDto(
    string AccessToken,
    string RefreshToken,
    int ExpiresIn,
    Guid UserId,
    string FullName,
    string Email,
    UserRole Role)
{
    public static AuthenticationDto FromUserAndToken(User user, string accessToken, DateTime accessTokenExpiresAt, string refreshToken)
    {
        var expiresIn = (int)Math.Max(0, (accessTokenExpiresAt - DateTime.UtcNow).TotalSeconds);

        return new AuthenticationDto(accessToken, refreshToken, expiresIn, user.Id, user.FullName, user.Email.Value, user.Role);
    }
}
