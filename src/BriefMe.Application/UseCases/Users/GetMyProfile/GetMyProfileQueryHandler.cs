using BriefMe.Application.Abstractions;
using BriefMe.Application.Abstractions.Messaging;
using BriefMe.Application.Common.Dtos;
using BriefMe.Application.Common.Errors;
using BriefMe.Domain.Common;
using BriefMe.Domain.Repositories;

namespace BriefMe.Application.UseCases.Users.GetMyProfile;

public sealed class GetMyProfileQueryHandler(
    IUserRepository userRepository,
    ICurrentUser currentUser) : IQueryHandler<GetMyProfileQuery, UserDto>
{
    public async Task<Result<UserDto>> Handle(GetMyProfileQuery request, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(currentUser.UserId, out var userId))
            return Result.Unauthorized<UserDto>(CommonErrors.UserNotAuthenticated);

        var user = await userRepository.GetByIdAsync(userId, cancellationToken);
        if (user is null)
            return Result.NotFound<UserDto>(UserErrors.NotFound);

        return Result.Success(UserDto.FromUser(user));
    }
}
