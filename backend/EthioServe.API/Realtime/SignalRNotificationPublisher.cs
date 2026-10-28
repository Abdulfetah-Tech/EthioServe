using EthioServe.Application.Notifications;
using Microsoft.AspNetCore.SignalR;

namespace EthioServe.API.Realtime;

public sealed class SignalRNotificationPublisher(
    IHubContext<NotificationHub> hub,
    ILogger<SignalRNotificationPublisher> logger) : INotificationPublisher
{
    public const string EventName = "notification";

    public async Task PublishAsync(IReadOnlyList<NotificationPush> items, CancellationToken ct = default)
    {
        foreach (var item in items)
        {
            try
            {
                await hub.Clients.User(item.UserId.ToString()).SendAsync(EventName, item.Notification, ct);
            }
            catch (Exception ex)
            {
                // The notification is already saved, so the person sees it the next time the app refreshes.
                logger.LogWarning(ex, "Could not push notification {NotificationId} to a connected user.", item.Notification.Id);
            }
        }
    }
}