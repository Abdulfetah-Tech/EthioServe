using EthioServe.Application.Abstractions;
using EthioServe.Application.Common;
using EthioServe.Application.Common.Exceptions;
using EthioServe.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace EthioServe.Application.Favorites;

public sealed class FavoriteService(
    IApplicationDbContext db,
    ICurrentUser currentUser,
    IAuditTrail audit) : IFavoriteService
{
    private Guid CustomerId =>
        currentUser.CustomerId ?? throw new ForbiddenException("This account has no customer profile.");

    public async Task<PagedResult<FavoriteProviderDto>> ListAsync(
        FavoriteQuery query, CancellationToken ct = default)
    {
        var customerId = CustomerId;
        var (page, pageSize) = Paging.Normalize(query.Page, query.PageSize);

        var rows = db.FavoriteProviders.AsNoTracking()
            .Where(f => f.CustomerId == customerId)
            .OrderByDescending(f => f.CreatedAt).ThenByDescending(f => f.Id);

        var total = await rows.CountAsync(ct);

        var items = await rows
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(f => new FavoriteProviderDto(f.Id, f.ProviderId, string.Empty, f.CreatedAt))
            .ToListAsync(ct);

        return new PagedResult<FavoriteProviderDto>(items, page, pageSize, total);
    }

    public async Task AddAsync(Guid providerId, CancellationToken ct = default)
    {
        var customerId = CustomerId;

        var providerExists = await db.ProviderProfiles.AsNoTracking()
            .AnyAsync(p => p.Id == providerId, ct);

        if (!providerExists)
        {
            throw new NotFoundException("Provider not found.");
        }

        if (await db.FavoriteProviders.AnyAsync(f => f.CustomerId == customerId && f.ProviderId == providerId, ct))
        {
            throw new ConflictException("This provider is already saved.");
        }

        var favorite = new FavoriteProvider(customerId, providerId);
        db.FavoriteProviders.Add(favorite);
        audit.Record(
            "FavoriteAdded", nameof(FavoriteProvider), favorite.Id, null,
            new { favorite.CustomerId, favorite.ProviderId });
        await db.SaveChangesAsync(ct);
    }

    public async Task RemoveAsync(Guid providerId, CancellationToken ct = default)
    {
        var customerId = CustomerId;

        var favorite = await db.FavoriteProviders
            .FirstOrDefaultAsync(f => f.CustomerId == customerId && f.ProviderId == providerId, ct)
            ?? throw new NotFoundException("Favorite not found.");

        db.FavoriteProviders.Remove(favorite);
        audit.Record(
            "FavoriteRemoved", nameof(FavoriteProvider), favorite.Id,
            new { favorite.CustomerId, favorite.ProviderId }, null);
        await db.SaveChangesAsync(ct);
    }
}
