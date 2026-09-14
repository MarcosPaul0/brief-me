using BriefMe.Application.Abstractions.Messaging;
using BriefMe.Application.Common.Dtos;
using BriefMe.Application.UseCases.Users;
using BriefMe.Domain.Common;
using BriefMe.Domain.Repositories;

namespace BriefMe.Application.UseCases.Users.GetUsersPaged;

public sealed class GetUsersPagedQueryHandler(
    IUserRepository userRepository,
    IRoleRepository roleRepository) : IQueryHandler<GetUsersPagedQuery, PagedDto<UserListItemDto>>
{
    public async Task<Result<PagedDto<UserListItemDto>>> Handle(GetUsersPagedQuery request, CancellationToken cancellationToken)
    {
        var filter = new UserFilter(request.Search, request.Status, request.RoleId);
        var sort = new UserSort(request.SortField, request.SortDescending);

        var users = await userRepository.GetPagedAsync(filter, request.Page, request.PageSize, sort, cancellationToken);
        var totalCount = await userRepository.CountAsync(filter, cancellationToken);

        var roleIds = users.SelectMany(u => u.UserRoles.Select(ur => ur.RoleId)).Distinct().ToList();
        var roles = roleIds.Count == 0
            ? []
            : await roleRepository.GetByIdsAsync(roleIds, cancellationToken);
        var roleNamesById = roles.ToDictionary(r => r.Id, r => r.Name);

        var items = users
            .Select(user => UserListItemDto.FromUserWithRoleNames(user, roleNamesById))
            .ToList();

        return Result.Success(PagedDto<UserListItemDto>.Create(
            items,
            request.Page,
            request.PageSize,
            totalCount));
    }
}
