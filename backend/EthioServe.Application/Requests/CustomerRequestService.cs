using EthioServe.Application.Abstractions;
using EthioServe.Application.Common;
using EthioServe.Application.Common.Exceptions;
using EthioServe.Domain.Common;
using EthioServe.Domain.Entities;
using EthioServe.Domain.Enums;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.EntityFrameworkCore;

namespace EthioServe.Application.Requests;

public sealed class CustomerRequestService(
    IApplicationDbContext db,
    ICurrentUser currentUser,
    IAuditTrail audit,
    IClock clock,
    IValidator<CreateJobRequest> createValidator,
    IValidator<UpdateJobRequest> updateValidator,
    IValidator<CancelJobRequest> cancelValidator) : ICustomerRequestService
{
    public const int MaxOpenRequests = 10;

    // Ethiopia is UTC+3 all year (no daylight saving), so "today" for a customer is a fixed offset from UTC.
    private static readonly TimeSpan AddisOffset = TimeSpan.FromHours(3);

    // The customer comes from the signed-in user's token, never from the request.
    private Guid CustomerId =>
        currentUser.CustomerId ?? throw new ForbiddenException("This account has no customer profile.");

    public async Task<RequestDto> CreateAsync(CreateJobRequest request, CancellationToken ct = default)
    {
        var customerId = CustomerId;
        await createValidator.ValidateAndThrowAsync(request, ct);

        await EnsureServiceIsAvailableAsync(request.ServiceId, ct);
        await EnsureLocationIsConsistentAsync(request.RegionId, request.CityId, ct);
        EnsureDateIsNotInThePast(request.PreferredDate);

        var open = await db.ServiceRequests.CountAsync(
            r => r.CustomerId == customerId
                 && (r.Status == ServiceRequestStatus.Pending || r.Status == ServiceRequestStatus.Quoted),
            ct);

        if (open >= MaxOpenRequests)
        {
            throw new DomainException(
                $"You can have at most {MaxOpenRequests} open requests. Cancel one, or wait for providers to respond.");
        }

        var entity = new ServiceRequest(
            customerId, request.ServiceId, request.Title, request.Description, request.Address,
            request.RegionId, request.CityId, request.BudgetMin, request.BudgetMax,
            request.PreferredDate, request.PreferredTime);

        db.ServiceRequests.Add(entity);
        audit.Record("ServiceRequestCreated", nameof(ServiceRequest), entity.Id, null, Snapshot(entity));
        await db.SaveChangesAsync(ct);

        return await GetAsync(entity.Id, ct);
    }

    public async Task<PagedResult<RequestListItemDto>> ListAsync(RequestListQuery query, CancellationToken ct = default)
    {
        var customerId = CustomerId;
        var (page, pageSize) = Paging.Normalize(query.Page, query.PageSize);

        var rows = RequestProjections.Rows(db).Where(x => x.Request.CustomerId == customerId);

        if (query.Status is { } status)
        {
            rows = rows.Where(x => x.Request.Status == status);
        }

        var total = await rows.CountAsync(ct);

        var pageRows = await rows
            .OrderByDescending(x => x.Request.CreatedAt).ThenByDescending(x => x.Request.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return new PagedResult<RequestListItemDto>(pageRows.Select(RequestProjections.ToListItem).ToList(), page, pageSize, total);
    }

    // Looked up by id AND owner: another customer's request is simply "not found".
    public async Task<RequestDto> GetAsync(Guid id, CancellationToken ct = default)
    {
        var customerId = CustomerId;

        var row = await RequestProjections.Rows(db)
                      .Where(x => x.Request.Id == id && x.Request.CustomerId == customerId)
                      .FirstOrDefaultAsync(ct)
                  ?? throw new NotFoundException("Request not found.");

        return RequestProjections.ToRequestDto(row);
    }

    public async Task<RequestDto> UpdateAsync(Guid id, UpdateJobRequest request, CancellationToken ct = default)
    {
        var customerId = CustomerId;
        await updateValidator.ValidateAndThrowAsync(request, ct);

        var entity = await FindAsync(id, customerId, ct);

        await EnsureLocationIsConsistentAsync(request.RegionId, request.CityId, ct);
        EnsureDateIsNotInThePast(request.PreferredDate);

        var before = Snapshot(entity);

        entity.UpdateDetails(
            request.Title, request.Description, request.Address, request.RegionId, request.CityId,
            request.BudgetMin, request.BudgetMax, request.PreferredDate, request.PreferredTime);

        audit.Record("ServiceRequestUpdated", nameof(ServiceRequest), entity.Id, before, Snapshot(entity));
        await db.SaveChangesAsync(ct);

        return await GetAsync(id, ct);
    }

    public async Task<RequestDto> CancelAsync(Guid id, CancelJobRequest request, CancellationToken ct = default)
    {
        var customerId = CustomerId;
        await cancelValidator.ValidateAndThrowAsync(request, ct);

        var entity = await FindAsync(id, customerId, ct);

        var before = Snapshot(entity);
        entity.Cancel(request.Reason);

        audit.Record("ServiceRequestCancelled", nameof(ServiceRequest), entity.Id, before, Snapshot(entity));
        await db.SaveChangesAsync(ct);

        return await GetAsync(id, ct);
    }

    // The audit trail records what changed in the workflow, never the customer's address.
    private static object Snapshot(ServiceRequest r) => new { r.Status, r.Title, r.ServiceId, r.CityId };

    private async Task<ServiceRequest> FindAsync(Guid id, Guid customerId, CancellationToken ct) =>
        await db.ServiceRequests.FirstOrDefaultAsync(r => r.Id == id && r.CustomerId == customerId, ct)
        ?? throw new NotFoundException("Request not found.");

    private async Task EnsureServiceIsAvailableAsync(Guid serviceId, CancellationToken ct)
    {
        var available = await (
            from s in db.Services.AsNoTracking()
            join c in db.ServiceCategories.AsNoTracking() on s.CategoryId equals c.Id
            where s.Id == serviceId && s.IsActive && c.IsActive
            select s.Id).AnyAsync(ct);

        if (!available)
        {
            throw new ValidationException(new[]
            {
                new ValidationFailure("ServiceId", "The selected service does not exist or is not available.")
            });
        }
    }

    private async Task EnsureLocationIsConsistentAsync(int regionId, int cityId, CancellationToken ct)
    {
        if (!await db.Cities.AnyAsync(c => c.Id == cityId && c.RegionId == regionId, ct))
        {
            throw new ValidationException(new[]
            {
                new ValidationFailure("CityId", "The selected city does not belong to the selected region.")
            });
        }
    }

    private void EnsureDateIsNotInThePast(DateOnly? date)
    {
        var today = DateOnly.FromDateTime(clock.UtcNow.UtcDateTime + AddisOffset);

        if (date is { } preferred && preferred < today)
        {
            throw new ValidationException(new[]
            {
                new ValidationFailure("PreferredDate", "The preferred date cannot be in the past.")
            });
        }
    }
}