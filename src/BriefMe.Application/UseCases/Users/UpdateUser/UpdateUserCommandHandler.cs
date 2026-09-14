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
    IRoleRepository roleRepository,
    IUnitOfWork unitOfWork) : ICommandHandler<UpdateUserCommand, UserDto>
{
    public async Task<Result<UserDto>> Handle(UpdateUserCommand request, CancellationToken cancellationToken)
    {
        var emailResult = Email.Create(request.Email);
        if (emailResult.IsFailure)
            return Result.Failure<UserDto>(emailResult.Error!);

        var user = await userRepository.GetByIdWithRolesAsync(request.UserId, cancellationToken);
        if (user is null)
            return Result.NotFound<UserDto>(UserErrors.NotFound);

        if (await userRepository.EmailExistsAsync(emailResult.Value, request.UserId, cancellationToken))
            return Result.Conflict<UserDto>(UserErrors.DuplicateEmail);

        var distinctRoleIds = request.RoleIds.Distinct().ToList();

        IReadOnlyList<Role> roles = distinctRoleIds.Count == 0
            ? []
            : await roleRepository.GetByIdsAsync(distinctRoleIds, cancellationToken);

        if (roles.Count != distinctRoleIds.Count)
            return Result.Failure<UserDto>(UserErrors.InvalidRoles);

        user.UpdateProfile(request.FullName, emailResult.Value);
        ReconcileRoles(user, distinctRoleIds);

        await unitOfWork.CommitAsync(cancellationToken);

        return Result.Success(UserDto.FromUserAndRoles(user, roles));
    }

    private static void ReconcileRoles(User user, IReadOnlyCollection<Guid> targetRoleIds)
    {
        var currentRoleIds = user.UserRoles.Select(ur => ur.RoleId).ToList();

        foreach (var roleId in currentRoleIds.Except(targetRoleIds))
            user.RemoveRole(roleId);

        foreach (var roleId in targetRoleIds.Except(currentRoleIds))
            user.AssignRole(roleId);
    }
}
