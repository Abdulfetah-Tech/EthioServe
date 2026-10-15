using EthioServe.Application.Abstractions;
using EthioServe.Application.Common.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace EthioServe.Application.Locations;

public sealed class LocationQueries(IApplicationDbContext db) : ILocationQueries
{
    public async Task<IReadOnlyList<RegionDto>> GetRegionsAsync(CancellationToken ct = default) =>
        await db.Regions.AsNoTracking()
            .OrderBy(r => r.Name)
            .Select(r => new RegionDto(r.Id, r.Name, r.NameAm))
            .ToListAsync(ct);

    public async Task<IReadOnlyList<CityDto>> GetCitiesAsync(int regionId, CancellationToken ct = default)
    {
        if (!await db.Regions.AnyAsync(r => r.Id == regionId, ct))
        {
            throw new NotFoundException("Region not found.");
        }

        return await db.Cities.AsNoTracking()
            .Where(c => c.RegionId == regionId)
            .OrderBy(c => c.Name)
            .Select(c => new CityDto(c.Id, c.RegionId, c.Name, c.NameAm))
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<SubCityDto>> GetSubCitiesAsync(int cityId, CancellationToken ct = default)
    {
        if (!await db.Cities.AnyAsync(c => c.Id == cityId, ct))
        {
            throw new NotFoundException("City not found.");
        }

        return await db.SubCities.AsNoTracking()
            .Where(s => s.CityId == cityId)
            .OrderBy(s => s.Name)
            .Select(s => new SubCityDto(s.Id, s.CityId, s.Name, s.NameAm))
            .ToListAsync(ct);
    }
}