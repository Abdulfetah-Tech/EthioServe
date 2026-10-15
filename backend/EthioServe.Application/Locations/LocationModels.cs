namespace EthioServe.Application.Locations;

public sealed record RegionDto(int Id, string Name, string? NameAm);

public sealed record CityDto(int Id, int RegionId, string Name, string? NameAm);

public sealed record SubCityDto(int Id, int CityId, string Name, string? NameAm);

public interface ILocationQueries
{
    Task<IReadOnlyList<RegionDto>> GetRegionsAsync(CancellationToken ct = default);

    Task<IReadOnlyList<CityDto>> GetCitiesAsync(int regionId, CancellationToken ct = default);

    Task<IReadOnlyList<SubCityDto>> GetSubCitiesAsync(int cityId, CancellationToken ct = default);
}