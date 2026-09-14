using BriefMe.Application.Abstractions.Messaging;
using BriefMe.Application.Common.Dtos;
using BriefMe.Domain.Common;
using BriefMe.Domain.Repositories;

namespace BriefMe.Application.UseCases.Permissions.GetPermissions;

public sealed class GetPermissionsQueryHandler(IPermissionRepository permissionRepository)
    : IQueryHandler<GetPermissionsQuery, IReadOnlyCollection<PermissionGroupDto>>
{
    public async Task<Result<IReadOnlyCollection<PermissionGroupDto>>> Handle(GetPermissionsQuery request, CancellationToken cancellationToken)
    {
        var permissions = await permissionRepository.GetAllAsync(cancellationToken);

        IReadOnlyCollection<PermissionGroupDto> groups =
        [
            .. permissions
                .GroupBy(p => p.Group)
                .OrderBy(g => g.Key)
                .Select(g => new PermissionGroupDto(
                    g.Key,
                    [
                        .. g.Select(PermissionItemDto.FromPermission)
                            .OrderBy(p => p.Code)
                    ]))
        ];

        return Result.Success(groups);
    }
}
