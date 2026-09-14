namespace BriefMe.Web.Auth;

public interface IAuthTokenStorage
{
    ValueTask<AuthTokens?> GetAsync();

    ValueTask SetAsync(AuthTokens tokens);

    ValueTask RemoveAsync();
}
