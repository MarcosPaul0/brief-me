using BriefMe.Application.Abstractions;
using BriefMe.Application.Abstractions.Messaging;
using BriefMe.Application.Common.Errors;
using BriefMe.Domain.Common;
using BriefMe.Domain.Enums;
using BriefMe.Domain.Repositories;

namespace BriefMe.Application.UseCases.Users.ChangeUserStatus;

public sealed class ChangeUserStatusCommandHandler(
    IUserRepository userRepository,
    IRefreshTokenRepository refreshTokenRepository,
    IUnitOfWork unitOfWork,
    ICurrentUser currentUser) : ICommandHandler<ChangeUserStatusCommand>
{
    public async Task<Result> Handle(ChangeUserStatusCommand request, CancellationToken cancellationToken)
    {
        if (request.Status == UserStatus.Inactive
            && Guid.TryParse(currentUser.UserId, out var currentUserId)
            && currentUserId == request.UserId)
            return Result.Failure(UserErrors.CannotDeactivateSelf);

        var user = await userRepository.GetByIdAsync(request.UserId, cancellationToken);
        if (user is null)
            return Result.NotFound(UserErrors.NotFound);

        if (request.Status == UserStatus.Active)
            user.Activate();
        else
            user.Deactivate();

        if (request.Status == UserStatus.Inactive)
            await refreshTokenRepository.RevokeAllActiveForUserAsync(user.Id, request.IpAddress, cancellationToken);

        await unitOfWork.CommitAsync(cancellationToken);

        return Result.Success();
    }
}
