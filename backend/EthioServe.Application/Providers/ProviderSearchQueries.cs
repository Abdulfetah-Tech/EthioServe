using EthioServe.Application.Abstractions;
using EthioServe.Application.Common;
using EthioServe.Application.Common.Exceptions;
using EthioServe.Domain.Entities;
using EthioServe.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace EthioServe.Application.Providers;

public sealed class ProviderSearchQueries(IApplicationDbContext db) : IProviderSearchQueries
{
    private const int MaxSearchLength = 100;
    private const int SummaryLength = 160;

    // An offer that customers can actually see: the offer, its service and its category are all active.
    private sealed class ActiveOffer
    {
        public Guid ProviderId { get; set; }

        public Guid ServiceId { get; set; }

        public string ServiceName { get; set; } = string.Empty;

        public Guid CategoryId { get; set; }

        public string CategoryName { get; set; } = string.Empty;

        public decimal Price { get; set; }

        public string? Description { get; set; }
    }

    private sealed class ProviderRow
    {
        public ProviderProfile Provider { get; set; } = null!;

        public decimal? MinPrice { get; set; }
    }

    public async Task<PagedResult<ProviderSummaryDto>> SearchAsync(ProviderSearchQuery query, CancellationToken ct = default)
    {
        var (page, pageSize) = Paging.Normalize(query.Page, query.PageSize);

        // The offers that count for this search: a service or category filter narrows them, and so the price shown.
        var offers = ActiveOffers();

        if (query.ServiceId is { } serviceId)
        {
            offers = offers.Where(o => o.ServiceId == serviceId);
        }

        if (query.CategoryId is { } categoryId)
        {
            offers = offers.Where(o => o.CategoryId == categoryId);
        }

        // Only verified providers are public. Pending, rejected, unverified and suspended ones never appear.
        var providers = db.ProviderProfiles.AsNoTracking()
            .Where(p => p.VerificationStatus == VerificationStatus.Verified);

        if (query.RegionId is { } regionId)
        {
            providers = providers.Where(p => p.RegionId == regionId);
        }

        if (query.CityId is { } cityId)
        {
            providers = providers.Where(p => p.CityId == cityId);
        }

        if (query.SubCityId is { } subCityId)
        {
            providers = providers.Where(p => p.SubCityId == subCityId);
        }

        if (query.AvailableOnly)
        {
            providers = providers.Where(p => p.IsAvailable);
        }

        if (query.MinRating is { } minRating)
        {
            var floor = Math.Clamp(minRating, 0m, 5m);
            providers = providers.Where(p => p.AverageRating >= floor);
        }

        var term = query.Search?.Trim();
        if (!string.IsNullOrEmpty(term))
        {
            var lowered = (term.Length > MaxSearchLength ? term[..MaxSearchLength] : term).ToLowerInvariant();
            providers = providers.Where(p => p.BusinessName.ToLower().Contains(lowered));
        }

        // Providers without a matching active offer have no price and are left out.
        var rows = providers
            .Select(p => new ProviderRow
            {
                Provider = p,
                MinPrice = offers.Where(o => o.ProviderId == p.Id).Min(o => (decimal?)o.Price)
            })
            .Where(x => x.MinPrice != null);

        var total = await rows.CountAsync(ct);

        var pageRows = await Order(rows, query.Sort)
            .ThenBy(x => x.Provider.BusinessName)
            .ThenBy(x => x.Provider.Id) // identical ordering on every request, so pages never overlap or skip
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new
            {
                x.Provider.Id,
                x.Provider.BusinessName,
                x.Provider.Description,
                x.Provider.YearsOfExperience,
                x.Provider.AverageRating,
                x.Provider.TotalReviews,
                CityName = db.Cities.Where(c => c.Id == x.Provider.CityId).Select(c => c.Name).FirstOrDefault() ?? string.Empty,
                SubCityName = db.SubCities.Where(s => s.Id == x.Provider.SubCityId).Select(s => s.Name).FirstOrDefault(),
                x.Provider.IsAvailable,
                StartingPrice = x.MinPrice!.Value
            })
            .ToListAsync(ct);

        var items = pageRows
            .Select(r => new ProviderSummaryDto(
                r.Id, r.BusinessName, Summarize(r.Description), r.YearsOfExperience, r.AverageRating,
                r.TotalReviews, r.CityName, r.SubCityName, r.IsAvailable, r.StartingPrice))
            .ToList();

        return new PagedResult<ProviderSummaryDto>(items, page, pageSize, total);
    }

    public async Task<PublicProviderDto> GetProviderAsync(Guid id, CancellationToken ct = default)
    {
        // Anyone who is not Verified is "not found", so a customer cannot tell a pending or suspended provider from a missing one.
        var provider = await db.ProviderProfiles.AsNoTracking()
            .Where(p => p.Id == id && p.VerificationStatus == VerificationStatus.Verified)
            .Select(p => new
            {
                p.Id,
                p.BusinessName,
                p.Description,
                p.YearsOfExperience,
                p.AverageRating,
                p.TotalReviews,
                p.IsAvailable,
                RegionName = db.Regions.Where(r => r.Id == p.RegionId).Select(r => r.Name).FirstOrDefault() ?? string.Empty,
                CityName = db.Cities.Where(c => c.Id == p.CityId).Select(c => c.Name).FirstOrDefault() ?? string.Empty,
                SubCityName = db.SubCities.Where(s => s.Id == p.SubCityId).Select(s => s.Name).FirstOrDefault()
            })
            .FirstOrDefaultAsync(ct)
            ?? throw new NotFoundException("Provider not found.");

        var services = await ActiveOffers()
            .Where(o => o.ProviderId == id)
            .OrderBy(o => o.CategoryName).ThenBy(o => o.ServiceName)
            .Select(o => new PublicOfferDto(o.ServiceId, o.ServiceName, o.CategoryId, o.CategoryName, o.Price, o.Description))
            .ToListAsync(ct);

        return new PublicProviderDto(
            provider.Id, provider.BusinessName, provider.Description, provider.YearsOfExperience,
            provider.AverageRating, provider.TotalReviews, provider.RegionName, provider.CityName,
            provider.SubCityName, provider.IsAvailable, services);
    }

    private static IOrderedQueryable<ProviderRow> Order(IQueryable<ProviderRow> rows, ProviderSort sort) => sort switch
    {
        ProviderSort.Experience =>
            rows.OrderByDescending(x => x.Provider.YearsOfExperience).ThenByDescending(x => x.Provider.AverageRating),
        ProviderSort.Price =>
            rows.OrderBy(x => x.MinPrice).ThenByDescending(x => x.Provider.AverageRating),
        ProviderSort.Name =>
            rows.OrderBy(x => x.Provider.BusinessName),
        _ =>
            rows.OrderByDescending(x => x.Provider.AverageRating).ThenByDescending(x => x.Provider.TotalReviews)
    };

    private IQueryable<ActiveOffer> ActiveOffers() =>
        from o in db.ProviderServices.AsNoTracking()
        join s in db.Services.AsNoTracking() on o.ServiceId equals s.Id
        join c in db.ServiceCategories.AsNoTracking() on s.CategoryId equals c.Id
        where o.IsActive && s.IsActive && c.IsActive
        select new ActiveOffer
        {
            ProviderId = o.ProviderId,
            ServiceId = s.Id,
            ServiceName = s.Name,
            CategoryId = c.Id,
            CategoryName = c.Name,
            Price = o.Price,
            Description = o.Description
        };

    private static string Summarize(string description)
    {
        var text = description.Trim();

        return text.Length <= SummaryLength ? text : text[..SummaryLength].TrimEnd() + "...";
    }
}