using Microsoft.AspNetCore.Components.Server.ProtectedBrowserStorage;

namespace BriefMe.Web.Auth;

public sealed class ProtectedBrowserAuthTokenStorage(ProtectedSessionStorage storage) : IAuthTokenStorage
{
    private const string Key = "auth_tokens";

    public async ValueTask<AuthTokens?> GetAsync()
    {
        var result = await storage.GetAsync<AuthTokens>(Key);
        return result.Success ? result.Value : null;
    }

    public ValueTask SetAsync(AuthTokens tokens) => storage.SetAsync(Key, tokens);

    public ValueTask RemoveAsync() => storage.DeleteAsync(Key);
}
