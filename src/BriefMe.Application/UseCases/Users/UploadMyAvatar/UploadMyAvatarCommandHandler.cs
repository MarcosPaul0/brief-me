using BriefMe.Application.Abstractions;
using BriefMe.Application.Abstractions.Messaging;
using BriefMe.Application.Common.Dtos;
using BriefMe.Application.Common.Errors;
using BriefMe.Domain.Common;
using BriefMe.Domain.Repositories;
using BriefMe.Domain.ValueObjects;

namespace BriefMe.Application.UseCases.Users.UploadMyAvatar;

/// <summary>
/// Stores the new image, points the user at it, and only after the commit drops the previous object — so a
/// failed save never leaves the user referencing a key that is gone (AVT-02).
/// </summary>
public sealed class UploadMyAvatarCommandHandler(
    IUserRepository userRepository,
    IUnitOfWork unitOfWork,
    ICurrentUser currentUser,
    IFileStorage fileStorage) : ICommandHandler<UploadMyAvatarCommand, UserDto>
{
    public async Task<Result<UserDto>> Handle(UploadMyAvatarCommand request, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(currentUser.UserId, out var userId))
            return Result.Unauthorized<UserDto>(CommonErrors.UserNotAuthenticated);

        var user = await userRepository.GetByIdAsync(userId, cancellationToken);
        if (user is null)
            return Result.NotFound<UserDto>(UserErrors.NotFound);

        // The declared length was validated, but the stream is client input too: stop reading past the cap.
        await using var buffer = new MemoryStream();
        if (!await TryCopyWithinLimitAsync(request.Content, buffer, AvatarFormat.MaxSizeBytes, cancellationToken))
            return Result.Failure<UserDto>(UserErrors.Validation.AvatarTooLarge(AvatarFormat.MaxSizeBytes));

        var format = AvatarFormat.Detect(buffer.GetBuffer().AsSpan(0, (int)buffer.Length));
        if (format.IsFailure)
            return Result.Failure<UserDto>(format.Error!);

        var key = $"avatars/{user.Id:N}/{Guid.NewGuid():N}{format.Value.Extension}";
        buffer.Position = 0;
        await fileStorage.SaveAsync(key, buffer, format.Value.ContentType, cancellationToken);

        var previousKey = user.AvatarKey;
        user.ChangeAvatar(key, format.Value);
        await unitOfWork.CommitAsync(cancellationToken);

        if (previousKey is not null)
            await fileStorage.DeleteAsync(previousKey, cancellationToken);

        return Result.Success(UserDto.FromUser(user));
    }

    private static async Task<bool> TryCopyWithinLimitAsync(Stream source, Stream destination, long limit, CancellationToken cancellationToken)
    {
        var chunk = new byte[81920];
        long total = 0;
        int read;

        while ((read = await source.ReadAsync(chunk, cancellationToken)) > 0)
        {
            total += read;
            if (total > limit)
                return false;

            await destination.WriteAsync(chunk.AsMemory(0, read), cancellationToken);
        }

        return true;
    }
}
