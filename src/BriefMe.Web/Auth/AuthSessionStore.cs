namespace BriefMe.Web.Auth;

public sealed class AuthSessionStore(IAuthTokenStorage storage, JwtClaimsPrincipalFactory claimsPrincipalFactory)
{
    private readonly TaskCompletionSource _restorationCompletedSource =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    public string? AccessToken { get; private set; }

    public string? RefreshToken { get; private set; }

    public event Action? TokenChanged;

    /// <summary>
    /// Completes once <see cref="RestoreAsync"/> has run to completion (found a valid token,
    /// found nothing, or failed) — lets an interactive-only consumer (e.g. <c>RedirectToLogin</c>)
    /// wait out the one-frame window where a freshly-connected circuit hasn't restored its session
    /// yet, instead of acting on a still-anonymous initial state. Never awaited during prerendering
    /// (no interactive circuit means <see cref="RestoreAsync"/> never runs, so this would hang).
    /// </summary>
    public Task RestorationCompleted => _restorationCompletedSource.Task;

    public async Task SignInAsync(AuthTokens tokens)
    {
        AccessToken = tokens.AccessToken;
        RefreshToken = tokens.RefreshToken;
        await storage.SetAsync(tokens);
        TokenChanged?.Invoke();
    }

    public async Task SignOutAsync()
    {
        AccessToken = null;
        RefreshToken = null;
        await storage.RemoveAsync();
        TokenChanged?.Invoke();
    }

    public async Task RestoreAsync()
    {
        try
        {
            await RestoreCoreAsync();
        }
        finally
        {
            _restorationCompletedSource.TrySetResult();
        }
    }

    private async Task RestoreCoreAsync()
    {
        AuthTokens? tokens;
        try
        {
            tokens = await storage.GetAsync();
        }
        catch (Exception)
        {
            // Interop failure (e.g. circuit torn down mid-restore) — stay signed out rather than fault the circuit.
            return;
        }

        if (tokens is null)
            return;

        // Restored regardless of access-token validity: TryRefreshAsync can recover the session with this
        // even when the access token below turns out to be expired.
        RefreshToken = tokens.RefreshToken;

        if (!claimsPrincipalFactory.TryCreatePrincipal(tokens.AccessToken, out _))
            return;

        AccessToken = tokens.AccessToken;
        TokenChanged?.Invoke();
    }
}
