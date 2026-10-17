using EthioServe.Application.Common;

namespace EthioServe.Application.Providers;

public enum ProviderSort
{
    Rating,
    Experience,
    Price,
    Name
}

/// <summary>Bound from the query string. Paging values are clamped by <see cref="Paging.Normalize"/>.</summary>
public sealed class ProviderSearchQuery
{
    public Guid? CategoryId { get; set; }

    public Guid? ServiceId { get; set; }

    public int? RegionId { get; set; }

    public int? CityId { get; set; }

    public int? SubCityId { get; set; }

    public decimal? MinRating { get; set; }

    public bool AvailableOnly { get; set; }

    public string? Search { get; set; }

    public ProviderSort Sort { get; set; } = ProviderSort.Rating;

    public int Page { get; set; } = 1;

    public int PageSize { get; set; } = Paging.DefaultPageSize;
}

// Public shapes deliberately contain NO phone number, street address, coordinates, owner id or review notes.
public sealed record ProviderSummaryDto(
    Guid Id,
    string BusinessName,
    string Summary,
    int YearsOfExperience,
    decimal AverageRating,
    int TotalReviews,
    string CityName,
    string? SubCityName,
    bool IsAvailable,
    decimal StartingPrice);

public sealed record PublicOfferDto(
    Guid ServiceId,
    string ServiceName,
    Guid CategoryId,
    string CategoryName,
    decimal Price,
    string? Description);

public sealed record PublicProviderDto(
    Guid Id,
    string BusinessName,
    string Description,
    int YearsOfExperience,
    decimal AverageRating,
    int TotalReviews,
    string RegionName,
    string CityName,
    string? SubCityName,
    bool IsAvailable,
    IReadOnlyList<PublicOfferDto> Services);

public interface IProviderSearchQueries
{
    Task<PagedResult<ProviderSummaryDto>> SearchAsync(ProviderSearchQuery query, CancellationToken ct = default);

    Task<PublicProviderDto> GetProviderAsync(Guid id, CancellationToken ct = default);
}