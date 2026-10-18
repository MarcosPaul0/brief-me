using System.Security.Claims;
using BriefMe.Application.Abstractions;
using BriefMe.Domain.Enums;

namespace BriefMe.Web.Auth;

public sealed class HttpContextCurrentUser(IHttpContextAccessor httpContextAccessor) : ICurrentUser
{
    public string? UserId => httpContextAccessor.HttpContext?.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

    public UserRole? Role =>
        Enum.TryParse<UserRole>(httpContextAccessor.HttpContext?.User.FindFirst(ClaimTypes.Role)?.Value, out var role)
            ? role
            : null;
}
