using EthioServe.Application.Common;
using EthioServe.Domain.Enums;

namespace EthioServe.Application.Catalog;

public sealed record AdminCategoryDto(
    Guid Id,
    string Name,
    string? Description,
    string? IconKey,
    bool IsActive,
    int ServiceCount,
    int ActiveServiceCount,
    DateTimeOffset UpdatedAt);

public sealed record AdminServiceDto(
    Guid Id,
    Guid CategoryId,
    string CategoryName,
    string Name,
    string? Description,
    decimal? BasePrice,
    PricingType PricingType,
    bool IsActive,
    DateTimeOffset UpdatedAt);

// Request bodies deliberately contain only editable fields: clients cannot send an id, a state or a count.
public sealed record CategoryRequest(string Name, string? Description, string? IconKey);

public sealed record CreateServiceRequest(
    Guid CategoryId, string Name, string? Description, decimal? BasePrice, PricingType PricingType);

public sealed record UpdateServiceRequest(string Name, string? Description, decimal? BasePrice, PricingType PricingType);

public sealed class AdminServiceQuery
{
    public Guid? CategoryId { get; set; }

    public string? Search { get; set; }

    public bool IncludeInactive { get; set; } = true;

    public int Page { get; set; } = 1;

    public int PageSize { get; set; } = Paging.DefaultPageSize;
}

public interface IAdminCatalogService
{
    Task<IReadOnlyList<AdminCategoryDto>> GetCategoriesAsync(CancellationToken ct = default);

    Task<AdminCategoryDto> GetCategoryAsync(Guid id, CancellationToken ct = default);

    Task<AdminCategoryDto> CreateCategoryAsync(CategoryRequest request, CancellationToken ct = default);

    Task<AdminCategoryDto> UpdateCategoryAsync(Guid id, CategoryRequest request, CancellationToken ct = default);

    Task<AdminCategoryDto> SetCategoryActiveAsync(Guid id, bool active, CancellationToken ct = default);

    Task<PagedResult<AdminServiceDto>> SearchServicesAsync(AdminServiceQuery query, CancellationToken ct = default);

    Task<AdminServiceDto> GetServiceAsync(Guid id, CancellationToken ct = default);

    Task<AdminServiceDto> CreateServiceAsync(CreateServiceRequest request, CancellationToken ct = default);

    Task<AdminServiceDto> UpdateServiceAsync(Guid id, UpdateServiceRequest request, CancellationToken ct = default);

    Task<AdminServiceDto> SetServiceActiveAsync(Guid id, bool active, CancellationToken ct = default);
}