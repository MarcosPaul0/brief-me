using BriefMe.Application.Abstractions;
using BriefMe.Application.Abstractions.Messaging;
using BriefMe.Application.Common.Dtos;
using BriefMe.Application.Common.Errors;
using BriefMe.Domain.Common;
using BriefMe.Domain.Entities;
using BriefMe.Domain.Repositories;
using BriefMe.Domain.ValueObjects;

namespace BriefMe.Application.UseCases.Users.UpdateUser;

public sealed class UpdateUserCommandHandler(
    IUserRepository userRepository,
    IUnitOfWork unitOfWork,
    ICurrentUser currentUser) : ICommandHandler<UpdateUserCommand, UserDto>
{
    public async Task<Result<UserDto>> Handle(UpdateUserCommand request, CancellationToken cancellationToken)
    {
        var emailResult = Email.Create(request.Email);
        if (emailResult.IsFailure)
            return Result.Failure<UserDto>(emailResult.Error!);

        var user = await userRepository.GetByIdAsync(request.UserId, cancellationToken);
        if (user is null)
            return Result.NotFound<UserDto>(UserErrors.NotFound);

        if (await userRepository.EmailExistsAsync(emailResult.Value, request.UserId, cancellationToken))
            return Result.Conflict<UserDto>(UserErrors.DuplicateEmail);

        // The admin cannot demote themselves: with three fixed profiles that could leave nobody able to
        // manage users.
        if (request.Role != user.Role
            && Guid.TryParse(currentUser.UserId, out var currentUserId)
            && currentUserId == request.UserId)
            return Result.Failure<UserDto>(UserErrors.CannotChangeOwnRole);

        user.UpdateProfile(request.FullName, emailResult.Value);
        user.ChangeRole(request.Role);

        await unitOfWork.CommitAsync(cancellationToken);

        return Result.Success(UserDto.FromUser(user));
    }
}
