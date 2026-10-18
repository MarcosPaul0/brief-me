using BriefMe.Application.Abstractions.Messaging;
using BriefMe.Application.Common.Dtos;
using BriefMe.Application.UseCases.Users;
using BriefMe.Domain.Common;
using BriefMe.Domain.Repositories;

namespace BriefMe.Application.UseCases.Users.GetUsersPaged;

public sealed class GetUsersPagedQueryHandler(
    IUserRepository userRepository) : IQueryHandler<GetUsersPagedQuery, PagedDto<UserListItemDto>>
{
    public async Task<Result<PagedDto<UserListItemDto>>> Handle(GetUsersPagedQuery request, CancellationToken cancellationToken)
    {
        var filter = new UserFilter(request.Search, request.Status, request.Role);
        var sort = new UserSort(request.SortField, request.SortDescending);

        var users = await userRepository.GetPagedAsync(filter, request.Page, request.PageSize, sort, cancellationToken);
        var totalCount = await userRepository.CountAsync(filter, cancellationToken);

        var items = users
            .Select(UserListItemDto.FromUser)
            .ToList();

        return Result.Success(PagedDto<UserListItemDto>.Create(
            items,
            request.Page,
            request.PageSize,
            totalCount));
    }
}
