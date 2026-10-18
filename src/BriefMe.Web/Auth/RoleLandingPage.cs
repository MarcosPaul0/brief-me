using System.Security.Claims;
using BriefMe.Domain.Enums;

namespace BriefMe.Web.Auth;

/// <summary>
/// Where each role lands after login (and whenever "/" is requested): there is no shared home screen —
/// each role starts on its own main page. The header's wordmark points here too, through "/".
/// </summary>
public static class RoleLandingPage
{
    public const string Requester = "/briefings/meus";
    public const string Designer = "/briefings/meu-trabalho";
    public const string Admin = "/usuarios";

    /// <summary>The landing path for <paramref name="user"/>, or <c>null</c> when they carry none of the three roles.</summary>
    public static string? For(ClaimsPrincipal user)
    {
        if (user.IsInRole(nameof(UserRole.Requester)))
            return Requester;

        if (user.IsInRole(nameof(UserRole.Designer)))
            return Designer;

        if (user.IsInRole(nameof(UserRole.Admin)))
            return Admin;

        return null;
    }
}
