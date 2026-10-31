using EthioServe.Application.Common;

namespace EthioServe.Application.Favorites;

public sealed record FavoriteProviderDto(Guid Id, Guid ProviderId, string BusinessName, DateTimeOffset CreatedAt);

public sealed class FavoriteQuery
{
    public int Page { get; set; } = 1;

    public int PageSize { get; set; } = Paging.DefaultPageSize;
}

/// <summary>A customer's saved providers. Only the owner can list or manage their favorites.</summary>
public interface IFavoriteService
{
    Task<PagedResult<FavoriteProviderDto>> ListAsync(FavoriteQuery query, CancellationToken ct = default);

    Task AddAsync(Guid providerId, CancellationToken ct = default);

    Task RemoveAsync(Guid providerId, CancellationToken ct = default);
}
