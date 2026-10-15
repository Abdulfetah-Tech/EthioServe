using System.Linq.Expressions;
using EthioServe.Application.Abstractions;
using EthioServe.Application.Common;
using EthioServe.Application.Common.Exceptions;
using EthioServe.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace EthioServe.Application.Catalog;

/// <summary>Public (anonymous) catalog reads. Only active categories and services are visible here.</summary>
public sealed class CatalogQueries(IApplicationDbContext db) : ICatalogQueries
{
    private const int MaxSearchLength = 100;

    // A service together with its category's name and state, so one query filters on both.
    private sealed class ServiceRow
    {
        public Service Service { get; set; } = null!;

        public string CategoryName { get; set; } = string.Empty;
    }

    private static readonly Expression<Func<ServiceRow, ServiceDto>> ToServiceDto = x => new ServiceDto(
        x.Service.Id,
        x.Service.CategoryId,
        x.CategoryName,
        x.Service.Name,
        x.Service.Description,
        x.Service.BasePrice,
        x.Service.PricingType);

    public async Task<IReadOnlyList<CategoryDto>> GetCategoriesAsync(CancellationToken ct = default) =>
        await ToCategoryDto(db.ServiceCategories.AsNoTracking().Where(c => c.IsActive).OrderBy(c => c.Name))
            .ToListAsync(ct);

    public async Task<CategoryDto> GetCategoryAsync(Guid id, CancellationToken ct = default) =>
        await ToCategoryDto(db.ServiceCategories.AsNoTracking().Where(c => c.IsActive && c.Id == id))
            .FirstOrDefaultAsync(ct)
        ?? throw new NotFoundException("Category not found.");

    public async Task<PagedResult<ServiceDto>> SearchServicesAsync(ServiceQuery query, CancellationToken ct = default)
    {
        var (page, pageSize) = Paging.Normalize(query.Page, query.PageSize);

        var rows = ActiveServiceRows();

        if (query.CategoryId is { } categoryId)
        {
            rows = rows.Where(x => x.Service.CategoryId == categoryId);
        }

        var term = query.Search?.Trim();
        if (!string.IsNullOrEmpty(term))
        {
            var lowered = (term.Length > MaxSearchLength ? term[..MaxSearchLength] : term).ToLowerInvariant();
            rows = rows.Where(x => x.Service.Name.ToLower().Contains(lowered));
        }

        var total = await rows.CountAsync(ct);

        var items = await rows
            .OrderBy(x => x.Service.Name).ThenBy(x => x.Service.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(ToServiceDto)
            .ToListAsync(ct);

        return new PagedResult<ServiceDto>(items, page, pageSize, total);
    }

    public async Task<ServiceDto> GetServiceAsync(Guid id, CancellationToken ct = default) =>
        await ActiveServiceRows().Where(x => x.Service.Id == id).Select(ToServiceDto).FirstOrDefaultAsync(ct)
        ?? throw new NotFoundException("Service not found.");

    private IQueryable<ServiceRow> ActiveServiceRows() =>
        from s in db.Services.AsNoTracking()
        join c in db.ServiceCategories.AsNoTracking() on s.CategoryId equals c.Id
        where s.IsActive && c.IsActive
        select new ServiceRow { Service = s, CategoryName = c.Name };

    private IQueryable<CategoryDto> ToCategoryDto(IQueryable<ServiceCategory> categories) =>
        categories.Select(c => new CategoryDto(
            c.Id,
            c.Name,
            c.Description,
            c.IconKey,
            db.Services.Count(s => s.CategoryId == c.Id && s.IsActive)));
}