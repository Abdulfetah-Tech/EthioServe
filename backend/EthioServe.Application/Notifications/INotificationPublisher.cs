namespace EthioServe.Application.Notifications;

/// <summary>A saved notification and the user it belongs to.</summary>
public sealed record NotificationPush(Guid UserId, NotificationDto Notification);

/// <summary>
/// Delivers saved notifications to people who are online right now. It runs after the data is saved and must never
/// throw: a failed push must not fail the request that caused it.
/// </summary>
public interface INotificationPublisher
{
    Task PublishAsync(IReadOnlyList<NotificationPush> items, CancellationToken ct = default);
}