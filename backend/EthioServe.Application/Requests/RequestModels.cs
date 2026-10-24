using EthioServe.Application.Common;
using EthioServe.Domain.Enums;

namespace EthioServe.Application.Requests;

/// <summary>The fields a customer can set, shared by creating and editing so both follow the same rules.</summary>
public interface IJobRequestFields
{
    string Title { get; }

    string Description { get; }

    string Address { get; }

    int RegionId { get; }

    int CityId { get; }

    decimal? BudgetMin { get; }

    decimal? BudgetMax { get; }

    DateOnly? PreferredDate { get; }

    TimeOnly? PreferredTime { get; }
}

// Request bodies deliberately have no id, owner, status or reason fields: clients cannot send them.
public sealed record CreateJobRequest(
    Guid ServiceId,
    string Title,
    string Description,
    string Address,
    int RegionId,
    int CityId,
    decimal? BudgetMin,
    decimal? BudgetMax,
    DateOnly? PreferredDate,
    TimeOnly? PreferredTime) : IJobRequestFields;

/// <summary>The service cannot be changed after creation: cancel the request and create a new one instead.</summary>
public sealed record UpdateJobRequest(
    string Title,
    string Description,
    string Address,
    int RegionId,
    int CityId,
    decimal? BudgetMin,
    decimal? BudgetMax,
    DateOnly? PreferredDate,
    TimeOnly? PreferredTime) : IJobRequestFields;

public sealed record CancelJobRequest(string? Reason);

/// <summary>The customer's own view of a request, including the exact address.</summary>
public sealed record RequestDto(
    Guid Id,
    Guid ServiceId,
    string ServiceName,
    Guid CategoryId,
    string CategoryName,
    string Title,
    string Description,
    string Address,
    int RegionId,
    string RegionName,
    int CityId,
    string CityName,
    decimal? BudgetMin,
    decimal? BudgetMax,
    DateOnly? PreferredDate,
    TimeOnly? PreferredTime,
    ServiceRequestStatus Status,
    string? CancelledReason,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record RequestListItemDto(
    Guid Id,
    string Title,
    Guid ServiceId,
    string ServiceName,
    string CategoryName,
    string CityName,
    decimal? BudgetMin,
    decimal? BudgetMax,
    DateOnly? PreferredDate,
    ServiceRequestStatus Status,
    DateTimeOffset CreatedAt);

/// <summary>Bound from the query string. Paging values are clamped by <see cref="Paging.Normalize"/>.</summary>
public sealed class RequestListQuery
{
    public ServiceRequestStatus? Status { get; set; }

    public int Page { get; set; } = 1;

    public int PageSize { get; set; } = Paging.DefaultPageSize;
}

public interface ICustomerRequestService
{
    Task<RequestDto> CreateAsync(CreateJobRequest request, CancellationToken ct = default);

    Task<PagedResult<RequestListItemDto>> ListAsync(RequestListQuery query, CancellationToken ct = default);

    Task<RequestDto> GetAsync(Guid id, CancellationToken ct = default);

    Task<RequestDto> UpdateAsync(Guid id, UpdateJobRequest request, CancellationToken ct = default);

    Task<RequestDto> CancelAsync(Guid id, CancelJobRequest request, CancellationToken ct = default);
}

public sealed class FeedQuery
{
    public Guid? ServiceId { get; set; }

    public int Page { get; set; } = 1;

    public int PageSize { get; set; } = Paging.DefaultPageSize;
}

// What a provider sees: the job, never who asked. No address, customer id or contact details.
public sealed record FeedItemDto(
    Guid Id,
    string Title,
    string Summary,
    Guid ServiceId,
    string ServiceName,
    string CategoryName,
    string CityName,
    decimal? BudgetMin,
    decimal? BudgetMax,
    DateOnly? PreferredDate,
    TimeOnly? PreferredTime,
    ServiceRequestStatus Status,
    DateTimeOffset CreatedAt);

public sealed record FeedDetailDto(
    Guid Id,
    string Title,
    string Description,
    Guid ServiceId,
    string ServiceName,
    Guid CategoryId,
    string CategoryName,
    string RegionName,
    string CityName,
    decimal? BudgetMin,
    decimal? BudgetMax,
    DateOnly? PreferredDate,
    TimeOnly? PreferredTime,
    ServiceRequestStatus Status,
    DateTimeOffset CreatedAt);

public interface IProviderFeedService
{
    Task<PagedResult<FeedItemDto>> ListAsync(FeedQuery query, CancellationToken ct = default);

    Task<FeedDetailDto> GetAsync(Guid id, CancellationToken ct = default);
}