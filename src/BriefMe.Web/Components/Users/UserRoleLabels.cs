using BriefMe.Domain.Enums;

namespace BriefMe.Web.Components.Users;

/// <summary>Display text for <see cref="UserRole"/> — the one place the fixed profiles are named in the UI.</summary>
public static class UserRoleLabels
{
    public static IReadOnlyList<UserRole> All { get; } = [UserRole.Requester, UserRole.Designer, UserRole.Admin];

    public static string Label(UserRole role) => role switch
    {
        UserRole.Admin => "Administrador",
        UserRole.Designer => "Designer",
        UserRole.Requester => "Solicitante",
        _ => role.ToString()
    };
}
