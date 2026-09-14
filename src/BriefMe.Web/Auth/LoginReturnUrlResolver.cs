namespace BriefMe.Web.Auth;

/// <summary>
/// Inverse of <see cref="LoginRedirectUrlBuilder"/>: turns the "returnUrl" query value collected
/// after a successful login back into a safe navigation target, rejecting anything that isn't a
/// same-origin relative path (open-redirect protection).
/// </summary>
public static class LoginReturnUrlResolver
{
    private const string DefaultPath = "/";

    public static string Resolve(string? returnUrl)
    {
        if (string.IsNullOrEmpty(returnUrl))
            return DefaultPath;

        // A leading "//" or "/\" is parsed by browsers as a protocol-relative absolute URL
        // (e.g. "//evil.com"), not a same-origin path — reject before the relative-URI check below,
        // which would otherwise accept it.
        if (!returnUrl.StartsWith('/') || returnUrl.StartsWith("//") || returnUrl.StartsWith("/\\"))
            return DefaultPath;

        return Uri.TryCreate(returnUrl, UriKind.Relative, out _) ? returnUrl : DefaultPath;
    }
}
