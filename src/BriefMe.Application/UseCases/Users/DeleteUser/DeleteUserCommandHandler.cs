using BriefMe.Application.Abstractions;
using BriefMe.Application.Abstractions.Messaging;
using BriefMe.Application.Common.Errors;
using BriefMe.Domain.Common;
using BriefMe.Domain.Repositories;

namespace BriefMe.Application.UseCases.Users.DeleteUser;

public sealed class DeleteUserCommandHandler(
    IUserRepository userRepository,
    IRefreshTokenRepository refreshTokenRepository,
    IUnitOfWork unitOfWork,
    ICurrentUser currentUser) : ICommandHandler<DeleteUserCommand>
{
    public async Task<Result> Handle(DeleteUserCommand request, CancellationToken cancellationToken)
    {
        if (Guid.TryParse(currentUser.UserId, out var currentUserId) && currentUserId == request.UserId)
            return Result.Failure(UserErrors.CannotDeleteSelf);

        var user = await userRepository.GetByIdAsync(request.UserId, cancellationToken);
        if (user is null)
            return Result.NotFound(UserErrors.NotFound);

        user.MarkAsDeleted(currentUser.UserId);

        await refreshTokenRepository.RevokeAllActiveForUserAsync(user.Id, request.IpAddress, cancellationToken);

        await unitOfWork.CommitAsync(cancellationToken);

        return Result.Success();
    }
}
