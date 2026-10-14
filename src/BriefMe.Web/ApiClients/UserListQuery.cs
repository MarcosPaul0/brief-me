using BriefMe.Domain.Enums;
using BriefMe.Domain.Repositories;

namespace BriefMe.Web.ApiClients;

/// <summary>
/// Parameters for a page of the user listing. Field names mirror
/// <c>UsersController.GetPaged</c>'s query string so <see cref="UsersApiClient"/> can map them 1:1.
/// </summary>
public sealed record UserListQuery(
    int Page = 1,
    int PageSize = 20,
    string? Search = null,
    UserStatus? Status = null,
    UserRole? Role = null,
    UserSortField SortField = UserSortField.FullName,
    bool SortDescending = false);
