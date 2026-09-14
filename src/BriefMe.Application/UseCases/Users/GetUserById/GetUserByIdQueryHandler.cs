using BriefMe.Application.Abstractions.Messaging;
using BriefMe.Application.Common.Dtos;
using BriefMe.Application.Common.Errors;
using BriefMe.Domain.Common;
using BriefMe.Domain.Repositories;

namespace BriefMe.Application.UseCases.Users.GetUserById;

public sealed class GetUserByIdQueryHandler(
    IUserRepository userRepository,
    IRoleRepository roleRepository) : IQueryHandler<GetUserByIdQuery, UserDto>
{
    public async Task<Result<UserDto>> Handle(GetUserByIdQuery request, CancellationToken cancellationToken)
    {
        var user = await userRepository.GetByIdWithRolesAsync(request.UserId, cancellationToken);
        if (user is null)
            return Result.NotFound<UserDto>(UserErrors.NotFound);

        var roleIds = user.UserRoles.Select(ur => ur.RoleId).ToList();
        var roles = roleIds.Count == 0
            ? []
            : await roleRepository.GetByIdsAsync(roleIds, cancellationToken);

        return Result.Success(UserDto.FromUserAndRoles(user, roles));
    }
}
