namespace EthioServe.Application.Common;

/// <summary>Serialized as { items, page, pageSize, totalCount, totalPages }.</summary>
public sealed record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount)
{
    public int TotalPages => PageSize <= 0 ? 0 : (int)Math.Ceiling(TotalCount / (double)PageSize);
}

public static class Paging
{
    public const int DefaultPageSize = 20;
    public const int MaxPageSize = 50;
    public const int MaxPage = 10_000;

    /// <summary>Clamps client-supplied paging values so no request can ask for an unbounded or overflowing page.</summary>
    public static (int Page, int PageSize) Normalize(int page, int pageSize)
    {
        var size = pageSize <= 0 ? DefaultPageSize : Math.Min(pageSize, MaxPageSize);

        return (Math.Clamp(page, 1, MaxPage), size);
    }
}