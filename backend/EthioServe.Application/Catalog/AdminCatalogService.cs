using System.Linq.Expressions;
using EthioServe.Application.Abstractions;
using EthioServe.Application.Common;
using EthioServe.Application.Common.Exceptions;
using EthioServe.Domain.Entities;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.EntityFrameworkCore;

namespace EthioServe.Application.Catalog;

public sealed class AdminCatalogService(
    IApplicationDbContext db,
    IAuditTrail audit,
    IValidator<CategoryRequest> categoryValidator,
    IValidator<CreateServiceRequest> createServiceValidator,
    IValidator<UpdateServiceRequest> updateServiceValidator) : IAdminCatalogService
{
    private const int MaxSearchLength = 100;

    private sealed class ServiceRow
    {
        public Service Service { get; set; } = null!;

        public string CategoryName { get; set; } = string.Empty;
    }

    private static readonly Expression<Func<ServiceRow, AdminServiceDto>> ToServiceDto = x => new AdminServiceDto(
        x.Service.Id,
        x.Service.CategoryId,
        x.CategoryName,
        x.Service.Name,
        x.Service.Description,
        x.Service.BasePrice,
        x.Service.PricingType,
        x.Service.IsActive,
        x.Service.UpdatedAt);

    // ---------------------------------------------------------------- categories

    public async Task<IReadOnlyList<AdminCategoryDto>> GetCategoriesAsync(CancellationToken ct = default) =>
        await CategoryDtos(db.ServiceCategories.AsNoTracking().OrderBy(c => c.Name)).ToListAsync(ct);

    public async Task<AdminCategoryDto> GetCategoryAsync(Guid id, CancellationToken ct = default) =>
        await CategoryDtos(db.ServiceCategories.AsNoTracking().Where(c => c.Id == id)).FirstOrDefaultAsync(ct)
        ?? throw new NotFoundException("Category not found.");

    public async Task<AdminCategoryDto> CreateCategoryAsync(CategoryRequest request, CancellationToken ct = default)
    {
        await categoryValidator.ValidateAndThrowAsync(request, ct);

        var name = request.Name.Trim();
        await EnsureCategoryNameIsFreeAsync(name, Guid.Empty, ct);

        var category = new ServiceCategory(name, Clean(request.Description), Clean(request.IconKey));
        db.ServiceCategories.Add(category);
        audit.Record("CategoryCreated", nameof(ServiceCategory), category.Id, null, Snapshot(category));
        await db.SaveChangesAsync(ct);

        return await GetCategoryAsync(category.Id, ct);
    }

    public async Task<AdminCategoryDto> UpdateCategoryAsync(Guid id, CategoryRequest request, CancellationToken ct = default)
    {
        await categoryValidator.ValidateAndThrowAsync(request, ct);

        var category = await FindCategoryAsync(id, ct);
        var name = request.Name.Trim();
        await EnsureCategoryNameIsFreeAsync(name, id, ct);

        var before = Snapshot(category);
        category.Update(name, Clean(request.Description), Clean(request.IconKey));
        audit.Record("CategoryUpdated", nameof(ServiceCategory), category.Id, before, Snapshot(category));
        await db.SaveChangesAsync(ct);

        return await GetCategoryAsync(id, ct);
    }

    public async Task<AdminCategoryDto> SetCategoryActiveAsync(Guid id, bool active, CancellationToken ct = default)
    {
        var category = await FindCategoryAsync(id, ct);

        if (category.IsActive != active)
        {
            var before = Snapshot(category);

            if (active)
            {
                category.Activate();
            }
            else
            {
                category.Deactivate();
            }

            audit.Record(
                active ? "CategoryActivated" : "CategoryDeactivated",
                nameof(ServiceCategory), category.Id, before, Snapshot(category));
            await db.SaveChangesAsync(ct);
        }

        return await GetCategoryAsync(id, ct);
    }

    // ---------------------------------------------------------------- services

    public async Task<PagedResult<AdminServiceDto>> SearchServicesAsync(AdminServiceQuery query, CancellationToken ct = default)
    {
        var (page, pageSize) = Paging.Normalize(query.Page, query.PageSize);

        var rows = ServiceRows();

        if (query.CategoryId is { } categoryId)
        {
            rows = rows.Where(x => x.Service.CategoryId == categoryId);
        }

        if (!query.IncludeInactive)
        {
            rows = rows.Where(x => x.Service.IsActive);
        }

        var term = query.Search?.Trim();
        if (!string.IsNullOrEmpty(term))
        {
            var lowered = (term.Length > MaxSearchLength ? term[..MaxSearchLength] : term).ToLowerInvariant();
            rows = rows.Where(x => x.Service.Name.ToLower().Contains(lowered));
        }

        var total = await rows.CountAsync(ct);

        var items = await rows
            .OrderBy(x => x.CategoryName).ThenBy(x => x.Service.Name).ThenBy(x => x.Service.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(ToServiceDto)
            .ToListAsync(ct);

        return new PagedResult<AdminServiceDto>(items, page, pageSize, total);
    }

    public async Task<AdminServiceDto> GetServiceAsync(Guid id, CancellationToken ct = default) =>
        await ServiceRows().Where(x => x.Service.Id == id).Select(ToServiceDto).FirstOrDefaultAsync(ct)
        ?? throw new NotFoundException("Service not found.");

    public async Task<AdminServiceDto> CreateServiceAsync(CreateServiceRequest request, CancellationToken ct = default)
    {
        await createServiceValidator.ValidateAndThrowAsync(request, ct);

        if (!await db.ServiceCategories.AnyAsync(c => c.Id == request.CategoryId, ct))
        {
            throw new ValidationException(new[]
            {
                new ValidationFailure(nameof(request.CategoryId), "The selected category does not exist.")
            });
        }

        var name = request.Name.Trim();
        await EnsureServiceNameIsFreeAsync(request.CategoryId, name, Guid.Empty, ct);

        var service = new Service(request.CategoryId, name, request.PricingType, request.BasePrice, Clean(request.Description));
        db.Services.Add(service);
        audit.Record("ServiceCreated", nameof(Service), service.Id, null, Snapshot(service));
        await db.SaveChangesAsync(ct);

        return await GetServiceAsync(service.Id, ct);
    }

    public async Task<AdminServiceDto> UpdateServiceAsync(Guid id, UpdateServiceRequest request, CancellationToken ct = default)
    {
        await updateServiceValidator.ValidateAndThrowAsync(request, ct);

        var service = await FindServiceAsync(id, ct);
        var name = request.Name.Trim();
        await EnsureServiceNameIsFreeAsync(service.CategoryId, name, id, ct);

        var before = Snapshot(service);
        service.Update(name, Clean(request.Description), request.BasePrice, request.PricingType);
        audit.Record("ServiceUpdated", nameof(Service), service.Id, before, Snapshot(service));
        await db.SaveChangesAsync(ct);

        return await GetServiceAsync(id, ct);
    }

    public async Task<AdminServiceDto> SetServiceActiveAsync(Guid id, bool active, CancellationToken ct = default)
    {
        var service = await FindServiceAsync(id, ct);

        if (service.IsActive != active)
        {
            var before = Snapshot(service);

            if (active)
            {
                service.Activate();
            }
            else
            {
                service.Deactivate();
            }

            audit.Record(
                active ? "ServiceActivated" : "ServiceDeactivated",
                nameof(Service), service.Id, before, Snapshot(service));
            await db.SaveChangesAsync(ct);
        }

        return await GetServiceAsync(id, ct);
    }

    // ---------------------------------------------------------------- helpers

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static object Snapshot(ServiceCategory c) => new { c.Name, c.Description, c.IconKey, c.IsActive };

    private static object Snapshot(Service s) => new { s.CategoryId, s.Name, s.Description, s.BasePrice, s.PricingType, s.IsActive };

    private async Task<ServiceCategory> FindCategoryAsync(Guid id, CancellationToken ct) =>
        await db.ServiceCategories.FirstOrDefaultAsync(c => c.Id == id, ct)
        ?? throw new NotFoundException("Category not found.");

    private async Task<Service> FindServiceAsync(Guid id, CancellationToken ct) =>
        await db.Services.FirstOrDefaultAsync(s => s.Id == id, ct)
        ?? throw new NotFoundException("Service not found.");

    private async Task EnsureCategoryNameIsFreeAsync(string name, Guid exceptId, CancellationToken ct)
    {
        var lowered = name.ToLower();

        if (await db.ServiceCategories.AnyAsync(c => c.Id != exceptId && c.Name.ToLower() == lowered, ct))
        {
            throw new ConflictException("A category with this name already exists.");
        }
    }

    private async Task EnsureServiceNameIsFreeAsync(Guid categoryId, string name, Guid exceptId, CancellationToken ct)
    {
        var lowered = name.ToLower();

        if (await db.Services.AnyAsync(
                s => s.CategoryId == categoryId && s.Id != exceptId && s.Name.ToLower() == lowered, ct))
        {
            throw new ConflictException("A service with this name already exists in this category.");
        }
    }

    private IQueryable<ServiceRow> ServiceRows() =>
        from s in db.Services.AsNoTracking()
        join c in db.ServiceCategories.AsNoTracking() on s.CategoryId equals c.Id
        select new ServiceRow { Service = s, CategoryName = c.Name };

    private IQueryable<AdminCategoryDto> CategoryDtos(IQueryable<ServiceCategory> categories) =>
        categories.Select(c => new AdminCategoryDto(
            c.Id,
            c.Name,
            c.Description,
            c.IconKey,
            c.IsActive,
            db.Services.Count(s => s.CategoryId == c.Id),
            db.Services.Count(s => s.CategoryId == c.Id && s.IsActive),
            c.UpdatedAt));
}