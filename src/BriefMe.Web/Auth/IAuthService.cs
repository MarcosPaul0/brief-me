using BriefMe.Domain.Common;

namespace BriefMe.Web.Auth;

public interface IAuthService
{
    Task<Result> LoginAsync(string email, string password, CancellationToken cancellationToken = default);

    Task LogoutAsync(bool allDevices = false, CancellationToken cancellationToken = default);

    Task<bool> TryRefreshAsync(CancellationToken cancellationToken = default);
}
