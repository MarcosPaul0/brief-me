namespace BriefMe.Application.Common.Dtos;

/// <summary>
/// Envelope returned by paginated queries: the page's items plus the counters the caller needs to render a
/// pager. Lives in Application, not Domain — paging is a read-model concern with no meaning to any aggregate.
/// Repositories return the slice (<c>IReadOnlyList&lt;T&gt;</c>) and the matching total separately; the query
/// handler assembles them here, over DTOs rather than entities.
/// </summary>
public sealed class PagedDto<T>
{
    private PagedDto(IReadOnlyList<T> items, int page, int pageSize, int totalCount)
    {
        Items = items;
        Page = page;
        PageSize = pageSize;
        TotalCount = totalCount;
    }

    public IReadOnlyList<T> Items { get; }

    public int Page { get; }

    public int PageSize { get; }

    public int TotalCount { get; }

    public int TotalPages => PageSize == 0 ? 0 : (int)Math.Ceiling(TotalCount / (double)PageSize);

    public static PagedDto<T> Create(IReadOnlyList<T> items, int page, int pageSize, int totalCount) =>
        new(items, page, pageSize, totalCount);
}
