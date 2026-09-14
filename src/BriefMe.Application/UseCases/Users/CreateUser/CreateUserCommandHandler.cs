using BriefMe.Application.Abstractions.Messaging;
using BriefMe.Application.Abstractions;
using BriefMe.Application.Common.Dtos;
using BriefMe.Application.Common.Errors;
using BriefMe.Application.UseCases.Users;
using BriefMe.Domain.Common;
using BriefMe.Domain.Entities;
using BriefMe.Domain.Repositories;
using BriefMe.Domain.ValueObjects;

namespace BriefMe.Application.UseCases.Users.CreateUser;

public sealed class CreateUserCommandHandler(
    IUserRepository userRepository,
    IRoleRepository roleRepository,
    IPasswordHasher passwordHasher,
    IUnitOfWork unitOfWork) : ICommandHandler<CreateUserCommand, UserDto>
{
    public async Task<Result<UserDto>> Handle(CreateUserCommand request, CancellationToken cancellationToken)
    {
        var emailResult = Email.Create(request.Email);
        if (emailResult.IsFailure)
            return Result.Failure<UserDto>(emailResult.Error!);

        var passwordResult = Password.Create(request.Password);
        if (passwordResult.IsFailure)
            return Result.Failure<UserDto>(passwordResult.Error!);

        if (await userRepository.EmailExistsAsync(emailResult.Value, cancellationToken: cancellationToken))
            return Result.Conflict<UserDto>(UserErrors.DuplicateEmail);

        var distinctRoleIds = request.RoleIds.Distinct().ToList();

        IReadOnlyList<Role> roles = distinctRoleIds.Count == 0
            ? []
            : await roleRepository.GetByIdsAsync(distinctRoleIds, cancellationToken);

        if (roles.Count != distinctRoleIds.Count)
            return Result.Failure<UserDto>(UserErrors.InvalidRoles);

        var passwordHash = passwordHasher.Hash(passwordResult.Value.Value);
        var user = User.Create(request.FullName, emailResult.Value, passwordHash);

        foreach (var role in roles)
            user.AssignRole(role.Id);

        await userRepository.AddAsync(user, cancellationToken);
        await unitOfWork.CommitAsync(cancellationToken);

        return Result.Success(UserDto.FromUserAndRoles(user, roles));
    }
}
