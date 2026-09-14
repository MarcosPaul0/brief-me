namespace BriefMe.Application.Common.Errors;

/// <summary>
/// Failure messages for login, logout and refresh (RF001–RF003). The credential message is deliberately
/// generic — it must not reveal whether the e-mail exists.
/// </summary>
public static class AuthErrors
{
    public const string InvalidCredentials = "E-mail ou senha inválidos.";

    public const string InvalidRefreshToken = "Refresh token inválido ou expirado.";

    /// <summary>pt-BR wording returned by the Auth endpoints; see <see cref="CommonErrors.UserNotAuthenticated"/> for the briefing side.</summary>
    public const string UserNotAuthenticated = "Usuário não autenticado.";

    public static class Validation
    {
        public const string RefreshTokenRequired = "Refresh token é obrigatório.";
    }
}
