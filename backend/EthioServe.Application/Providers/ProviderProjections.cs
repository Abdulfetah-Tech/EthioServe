using System.Linq.Expressions;
using EthioServe.Application.Abstractions;
using EthioServe.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace EthioServe.Application.Providers;

/// <summary>Query shapes shared by the admin and public features, so both always show the same data.</summary>
internal static class ProviderProjections
{
    internal sealed class OfferRow
    {
        public ProviderService Offer { get; set; } = null!;

        public string ServiceName { get; set; } = string.Empty;

        public Guid CategoryId { get; set; }

        public string CategoryName { get; set; } = string.Empty;
    }

    public static readonly Expression<Func<OfferRow, ProviderOfferDto>> ToOfferDto = x => new ProviderOfferDto(
        x.Offer.Id,
        x.Offer.ServiceId,
        x.ServiceName,
        x.CategoryId,
        x.CategoryName,
        x.Offer.Price,
        x.Offer.Description,
        x.Offer.IsActive);

    public static IQueryable<OfferRow> OfferRows(IApplicationDbContext db) =>
        from o in db.ProviderServices.AsNoTracking()
        join s in db.Services.AsNoTracking() on o.ServiceId equals s.Id
        join c in db.ServiceCategories.AsNoTracking() on s.CategoryId equals c.Id
        select new OfferRow { Offer = o, ServiceName = s.Name, CategoryId = c.Id, CategoryName = c.Name };

    public static IQueryable<ProviderProfileDto> Profiles(IApplicationDbContext db, IQueryable<ProviderProfile> profiles) =>
        profiles.Select(p => new ProviderProfileDto(
            p.Id,
            p.BusinessName,
            p.Description,
            p.PhoneNumber,
            p.YearsOfExperience,
            p.VerificationStatus,
            p.VerificationNote,
            p.AverageRating,
            p.TotalReviews,
            p.RegionId,
            db.Regions.Where(r => r.Id == p.RegionId).Select(r => r.Name).FirstOrDefault() ?? string.Empty,
            p.CityId,
            db.Cities.Where(c => c.Id == p.CityId).Select(c => c.Name).FirstOrDefault() ?? string.Empty,
            p.SubCityId,
            db.SubCities.Where(s => s.Id == p.SubCityId).Select(s => s.Name).FirstOrDefault(),
            p.Address,
            p.Latitude,
            p.Longitude,
            p.IsAvailable,
            db.ProviderServices.Count(o => o.ProviderId == p.Id && o.IsActive)));
}