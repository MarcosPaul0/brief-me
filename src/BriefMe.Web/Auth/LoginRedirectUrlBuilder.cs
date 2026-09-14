namespace BriefMe.Web.Auth;

public static class LoginRedirectUrlBuilder
{
    private const string LoginPath = "/login";

    public static string Build(string relativePath)
    {
        if (string.IsNullOrEmpty(relativePath) || relativePath == "/")
            return LoginPath;

        // NavigationManager.ToBaseRelativePath strips the leading slash ("usuarios", "users/42"), but
        // LoginReturnUrlResolver only accepts a rooted local path — root it here so the returnUrl
        // survives the round-trip back after login instead of being rejected (→ fallback to "/").
        var localPath = relativePath.StartsWith('/') ? relativePath : $"/{relativePath}";
        return $"{LoginPath}?returnUrl={Uri.EscapeDataString(localPath)}";
    }
}
