using BriefMe.Domain.Common;
using BriefMe.Web.Auth;

namespace BriefMe.Web.Tests.TestDoubles;

public sealed class FakeAuthService : IAuthService
{
    public int TryRefreshCallCount { get; private set; }

    public bool RefreshResult { get; set; } = true;

    /// <summary>Runs inside <see cref="TryRefreshAsync"/> before it returns — lets a test simulate the
    /// real service storing a fresh token in the session on a successful refresh.</summary>
    public Func<Task>? OnRefreshAsync { get; set; }

    public Task<Result> LoginAsync(string email, string password, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("Not exercised by AuthenticatedApiClient tests.");

    public Task LogoutAsync(bool allDevices = false, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("Not exercised by AuthenticatedApiClient tests.");

    public async Task<bool> TryRefreshAsync(CancellationToken cancellationToken = default)
    {
        TryRefreshCallCount++;

        if (OnRefreshAsync is not null)
            await OnRefreshAsync();

        return RefreshResult;
    }
}
