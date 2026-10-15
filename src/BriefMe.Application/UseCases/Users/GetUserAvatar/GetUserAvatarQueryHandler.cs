using BriefMe.Application.Abstractions;
using BriefMe.Application.Abstractions.Messaging;
using BriefMe.Application.Common.Dtos;
using BriefMe.Application.Common.Errors;
using BriefMe.Domain.Common;
using BriefMe.Domain.Repositories;

namespace BriefMe.Application.UseCases.Users.GetUserAvatar;

/// <summary>
/// Unknown user, no avatar and a key whose object has vanished all answer NotFound (AVT-10) — the UI falls
/// back to the initial in every case, so the caller never needs to tell them apart.
/// </summary>
public sealed class GetUserAvatarQueryHandler(
    IUserRepository userRepository,
    IFileStorage fileStorage) : IQueryHandler<GetUserAvatarQuery, AvatarFileDto>
{
    public async Task<Result<AvatarFileDto>> Handle(GetUserAvatarQuery request, CancellationToken cancellationToken)
    {
        var user = await userRepository.GetByIdAsync(request.UserId, cancellationToken);
        if (user is null)
            return Result.NotFound<AvatarFileDto>(UserErrors.NotFound);

        if (user.AvatarKey is null || user.AvatarContentType is null)
            return Result.NotFound<AvatarFileDto>(UserErrors.AvatarNotFound);

        var content = await fileStorage.OpenReadAsync(user.AvatarKey, cancellationToken);
        if (content is null)
            return Result.NotFound<AvatarFileDto>(UserErrors.AvatarNotFound);

        return Result.Success(new AvatarFileDto(content, user.AvatarContentType));
    }
}
