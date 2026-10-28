using EthioServe.Application.Common;
using EthioServe.Domain.Enums;

namespace EthioServe.Application.Notifications;

public sealed record NotificationDto(
    Guid Id,
    NotificationType Type,
    string Title,
    string Message,
    bool IsRead,
    string? RelatedEntityType,
    Guid? RelatedEntityId,
    DateTimeOffset CreatedAt);

public sealed record UnreadCountDto(int Count);

public sealed record MarkedReadDto(int Updated);

/// <summary>Bound from the query string. Paging values are clamped by <see cref="Paging.Normalize"/>.</summary>
public sealed class NotificationQuery
{
    public bool UnreadOnly { get; set; }

    public int Page { get; set; } = 1;

    public int PageSize { get; set; } = Paging.DefaultPageSize;
}

/// <summary>A signed-in user's own notifications, whatever their role. Nobody can see anyone else's.</summary>
public interface INotificationService
{
    Task<PagedResult<NotificationDto>> ListAsync(NotificationQuery query, CancellationToken ct = default);

    Task<int> UnreadCountAsync(CancellationToken ct = default);

    Task MarkReadAsync(Guid id, CancellationToken ct = default);

    Task<int> MarkAllReadAsync(CancellationToken ct = default);
}