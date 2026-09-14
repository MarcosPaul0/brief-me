namespace BriefMe.Application.Common.Errors;

/// <summary>
/// Error messages that do not belong to a single feature. Everything here is used by more than one
/// use-case folder; a message used by a single feature belongs in that feature's catalog
/// (<see cref="BriefingErrors"/>, <see cref="UserErrors"/>, …).
/// </summary>
public static class CommonErrors
{
    /// <summary>
    /// Returned when <c>ICurrentUser.UserId</c> is absent or not a <see cref="Guid"/>. Distinct from
    /// <see cref="AuthErrors.UserNotAuthenticated"/>, which is the pt-BR wording the Auth endpoints return.
    /// </summary>
    public const string UserNotAuthenticated = "User is not authenticated.";

    /// <summary>Paging guards shared by the briefing queries. The user queries use pt-BR — see <see cref="UserErrors.Validation"/>.</summary>
    public static class Paging
    {
        public const string PageOutOfRange = "Page must be greater than or equal to 1.";

        public static string PageSizeOutOfRange(int maxPageSize) => $"PageSize must be between 1 and {maxPageSize}.";
    }
}
