using BriefMe.Application.Abstractions;
using BriefMe.Application.Abstractions.Messaging;
using BriefMe.Application.Common.Dtos;
using BriefMe.Application.Common.Errors;
using BriefMe.Domain.Common;
using BriefMe.Domain.Repositories;

namespace BriefMe.Application.UseCases.Users.RemoveMyAvatar;

public sealed class RemoveMyAvatarCommandHandler(
    IUserRepository userRepository,
    IUnitOfWork unitOfWork,
    ICurrentUser currentUser,
    IFileStorage fileStorage) : ICommandHandler<RemoveMyAvatarCommand, UserDto>
{
    public async Task<Result<UserDto>> Handle(RemoveMyAvatarCommand request, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(currentUser.UserId, out var userId))
            return Result.Unauthorized<UserDto>(CommonErrors.UserNotAuthenticated);

        var user = await userRepository.GetByIdAsync(userId, cancellationToken);
        if (user is null)
            return Result.NotFound<UserDto>(UserErrors.NotFound);

        if (user.AvatarKey is not { } previousKey)
            return Result.Success(UserDto.FromUser(user));

        user.RemoveAvatar();
        await unitOfWork.CommitAsync(cancellationToken);

        await fileStorage.DeleteAsync(previousKey, cancellationToken);

        return Result.Success(UserDto.FromUser(user));
    }
}
