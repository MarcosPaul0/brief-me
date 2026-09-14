using BriefMe.Web.Auth;

namespace BriefMe.Web.Tests.TestDoubles;

public class FakeAuthTokenStorage : IAuthTokenStorage
{
    private AuthTokens? _tokens;

    public Exception? ThrowOnGet { get; set; }

    public ValueTask<AuthTokens?> GetAsync()
    {
        if (ThrowOnGet is not null)
            throw ThrowOnGet;

        return ValueTask.FromResult(_tokens);
    }

    public ValueTask SetAsync(AuthTokens tokens)
    {
        _tokens = tokens;
        return ValueTask.CompletedTask;
    }

    public ValueTask RemoveAsync()
    {
        _tokens = null;
        return ValueTask.CompletedTask;
    }
}
