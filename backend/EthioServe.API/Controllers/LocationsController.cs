using EthioServe.Application.Locations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EthioServe.API.Controllers;

/// <summary>Public reference data used by sign-up and search forms.</summary>
[ApiController]
[AllowAnonymous]
[Route("api/locations")]
public sealed class LocationsController(ILocationQueries locations) : ControllerBase
{
    [HttpGet("regions")]
    public async Task<ActionResult<IReadOnlyList<RegionDto>>> Regions(CancellationToken ct) =>
        Ok(await locations.GetRegionsAsync(ct));

    [HttpGet("regions/{regionId:int}/cities")]
    public async Task<ActionResult<IReadOnlyList<CityDto>>> Cities(int regionId, CancellationToken ct) =>
        Ok(await locations.GetCitiesAsync(regionId, ct));

    [HttpGet("cities/{cityId:int}/sub-cities")]
    public async Task<ActionResult<IReadOnlyList<SubCityDto>>> SubCities(int cityId, CancellationToken ct) =>
        Ok(await locations.GetSubCitiesAsync(cityId, ct));
}