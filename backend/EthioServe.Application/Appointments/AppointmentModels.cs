using EthioServe.Application.Common;
using EthioServe.Domain.Enums;

namespace EthioServe.Application.Appointments;

public enum AppointmentScope
{
    /// <summary>Scheduled, confirmed or in progress, soonest first.</summary>
    Upcoming,

    /// <summary>Completed, cancelled or missed, most recent first.</summary>
    History
}

// The bodies deliberately have no id, status, provider or end time: the server decides those.
public sealed record ScheduleAppointmentRequest(DateTimeOffset StartAt, string? Notes);

public sealed record RescheduleAppointmentRequest(DateTimeOffset StartAt, string? Notes);

/// <summary>An appointment as the provider sees it, with the customer's contact details.</summary>
public sealed record ProviderAppointmentDto(
    Guid Id,
    Guid RequestId,
    string RequestTitle,
    string ServiceName,
    string CityName,
    DateTimeOffset StartAt,
    DateTimeOffset EndAt,
    string Address,
    string? Notes,
    AppointmentStatus Status,
    string CustomerName,
    string CustomerPhone,
    decimal Amount,
    DateTimeOffset CreatedAt);

/// <summary>An appointment as the customer sees it, with the provider's contact details.</summary>
public sealed record CustomerAppointmentDto(
    Guid Id,
    Guid RequestId,
    string RequestTitle,
    string ServiceName,
    DateTimeOffset StartAt,
    DateTimeOffset EndAt,
    string Address,
    string? Notes,
    AppointmentStatus Status,
    Guid ProviderId,
    string ProviderName,
    string ProviderPhone,
    decimal Amount,
    DateTimeOffset CreatedAt);

/// <summary>Bound from the query string. Paging values are clamped by <see cref="Paging.Normalize"/>.</summary>
public sealed class AppointmentQuery
{
    public AppointmentScope Scope { get; set; } = AppointmentScope.Upcoming;

    public Guid? RequestId { get; set; }

    public int Page { get; set; } = 1;

    public int PageSize { get; set; } = Paging.DefaultPageSize;
}

public interface IProviderAppointmentService
{
    Task<ProviderAppointmentDto> ScheduleAsync(Guid requestId, ScheduleAppointmentRequest request, CancellationToken ct = default);

    Task<PagedResult<ProviderAppointmentDto>> ListAsync(AppointmentQuery query, CancellationToken ct = default);

    Task<ProviderAppointmentDto> GetAsync(Guid id, CancellationToken ct = default);

    Task<ProviderAppointmentDto> RescheduleAsync(Guid id, RescheduleAppointmentRequest request, CancellationToken ct = default);

    Task<ProviderAppointmentDto> CancelAsync(Guid id, CancellationToken ct = default);

    Task<ProviderAppointmentDto> StartAsync(Guid id, CancellationToken ct = default);

    Task<ProviderAppointmentDto> CompleteAsync(Guid id, CancellationToken ct = default);
}

public interface ICustomerAppointmentService
{
    Task<PagedResult<CustomerAppointmentDto>> ListAsync(AppointmentQuery query, CancellationToken ct = default);

    Task<CustomerAppointmentDto> GetAsync(Guid id, CancellationToken ct = default);

    Task<CustomerAppointmentDto> ConfirmAsync(Guid id, CancellationToken ct = default);

    Task<CustomerAppointmentDto> CancelAsync(Guid id, CancellationToken ct = default);
}