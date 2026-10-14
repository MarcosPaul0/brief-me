using BriefMe.Application.Common.Dtos;
using BriefMe.Application.UseCases.Users;
using BriefMe.Domain.Common;
using BriefMe.Web.Contracts.Common;
using BriefMe.Web.Contracts.Users;

namespace BriefMe.Web.ApiClients;

/// <summary>
/// Thin typed client the Blazor user screens use to reach the REST API — keeps HTTP concerns out of the
/// Razor components (CLAUDE.md: components stay thin). Runs over the <c>"Api"</c> HttpClient, so
/// bearer-token attachment and refresh-on-401 come for free (USR-039). Write operations surface the
/// API's validation/business failures as a failed <see cref="Result{T}"/> whose <c>Errors</c> are keyed
/// by field, so the form can render them inline.
/// </summary>
public interface IUsersApiClient
{
    /// <summary>Fetches a page of users. Failure (network / non-success status) surfaces as a failed <see cref="Result{T}"/>.</summary>
    Task<Result<PagedResponse<UserListItemDto>>> GetUsersAsync(UserListQuery query, CancellationToken cancellationToken = default);

    /// <summary>Fetches a single user for the edit form. A 404 surfaces as <see cref="Result.NotFound{T}"/>.</summary>
    Task<Result<UserDto>> GetUserAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Creates a user via <c>POST /users</c>. Field-level failures land in <c>Result.Errors</c>.</summary>
    Task<Result<UserDto>> CreateUserAsync(CreateUserRequest request, CancellationToken cancellationToken = default);

    /// <summary>Updates a user via <c>PUT /users/{id}</c>. Field-level failures land in <c>Result.Errors</c>.</summary>
    Task<Result<UserDto>> UpdateUserAsync(Guid id, UpdateUserRequest request, CancellationToken cancellationToken = default);
}
