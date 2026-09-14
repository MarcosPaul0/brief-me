using BriefMe.Application.Abstractions;
using BriefMe.Application.Abstractions.Messaging;
using BriefMe.Application.Common.Errors;
using BriefMe.Domain.Common;
using BriefMe.Domain.Repositories;

namespace BriefMe.Application.UseCases.Auth.Logout;

public sealed class LogoutCommandHandler(
    IRefreshTokenService refreshTokenService,
    IRefreshTokenRepository refreshTokenRepository,
    IUnitOfWork unitOfWork,
    ICurrentUser currentUser) : ICommandHandler<LogoutCommand>
{
    public async Task<Result> Handle(LogoutCommand request, CancellationToken cancellationToken)
    {
        if (request.AllDevices)
        {
            if (!Guid.TryParse(currentUser.UserId, out var userId))
                return Result.Unauthorized(AuthErrors.UserNotAuthenticated);

            await refreshTokenRepository.RevokeAllActiveForUserAsync(userId, request.IpAddress, cancellationToken);
            await unitOfWork.CommitAsync(cancellationToken);

            return Result.Success();
        }

        await refreshTokenService.RevokeAsync(request.RefreshToken, request.IpAddress, cancellationToken);

        return Result.Success();
    }
}
