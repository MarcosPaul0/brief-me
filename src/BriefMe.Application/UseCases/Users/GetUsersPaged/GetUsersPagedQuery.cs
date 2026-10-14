using BriefMe.Application.Abstractions.Messaging;
using BriefMe.Application.Common.Dtos;
using BriefMe.Application.UseCases.Users;
using BriefMe.Domain.Common;
using BriefMe.Domain.Enums;
using BriefMe.Domain.Repositories;

namespace BriefMe.Application.UseCases.Users.GetUsersPaged;

public sealed record GetUsersPagedQuery(
    string? Search,
    UserStatus? Status,
    UserRole? Role,
    int Page,
    int PageSize,
    UserSortField SortField = UserSortField.FullName,
    bool SortDescending = false) : IQuery<PagedDto<UserListItemDto>>;
