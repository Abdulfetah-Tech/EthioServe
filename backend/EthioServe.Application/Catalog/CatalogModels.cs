using EthioServe.Application.Common;
using EthioServe.Domain.Enums;

namespace EthioServe.Application.Catalog;

public sealed record CategoryDto(Guid Id, string Name, string? Description, string? IconKey, int ServiceCount);

public sealed record ServiceDto(
    Guid Id,
    Guid CategoryId,
    string CategoryName,
    string Name,
    string? Description,
    decimal? BasePrice,
    PricingType PricingType);

/// <summary>Bound from the query string. Paging values are clamped by <see cref="Paging.Normalize"/>.</summary>
public sealed class ServiceQuery
{
    public Guid? CategoryId { get; set; }

    public string? Search { get; set; }

    public int Page { get; set; } = 1;

    public int PageSize { get; set; } = Paging.DefaultPageSize;
}

public interface ICatalogQueries
{
    Task<IReadOnlyList<CategoryDto>> GetCategoriesAsync(CancellationToken ct = default);

    Task<CategoryDto> GetCategoryAsync(Guid id, CancellationToken ct = default);

    Task<PagedResult<ServiceDto>> SearchServicesAsync(ServiceQuery query, CancellationToken ct = default);

    Task<ServiceDto> GetServiceAsync(Guid id, CancellationToken ct = default);
}