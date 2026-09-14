namespace BriefMe.Web.Contracts.Common;

/// <summary>
/// Client-side deserialization envelope for the API's paged responses. Mirrors the serialized shape of
/// <c>BriefMe.Application.Common.Pagination.PagedResult&lt;T&gt;</c>, which cannot be deserialized directly by
/// <c>System.Text.Json</c> (private constructor, get-only properties). Positional record so the
/// camelCase JSON binds onto it.
/// </summary>
public sealed record PagedResponse<T>(
    IReadOnlyList<T> Items,
    int Page,
    int PageSize,
    int TotalCount)
{
    public int TotalPages => PageSize == 0 ? 0 : (int)Math.Ceiling(TotalCount / (double)PageSize);
}
