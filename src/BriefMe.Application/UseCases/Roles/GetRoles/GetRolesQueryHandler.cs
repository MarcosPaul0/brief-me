using BriefMe.Application.Abstractions.Messaging;
using BriefMe.Application.Common.Dtos;
using BriefMe.Domain.Common;
using BriefMe.Domain.Repositories;

namespace BriefMe.Application.UseCases.Roles.GetRoles;

public sealed class GetRolesQueryHandler(IRoleRepository roleRepository)
    : IQueryHandler<GetRolesQuery, IReadOnlyCollection<RoleListItemDto>>
{
    public async Task<Result<IReadOnlyCollection<RoleListItemDto>>> Handle(GetRolesQuery request, CancellationToken cancellationToken)
    {
        var roles = await roleRepository.GetAllAsync(cancellationToken);

        IReadOnlyCollection<RoleListItemDto> items =
        [
            .. roles.Select(RoleListItemDto.FromRole)
        ];

        return Result.Success(items);
    }
}
