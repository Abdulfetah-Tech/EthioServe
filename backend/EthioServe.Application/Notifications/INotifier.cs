using EthioServe.Domain.Enums;

namespace EthioServe.Application.Notifications;

/// <summary>
/// Queues notifications. Nothing is saved here: the caller's own SaveChanges saves them, so a change and its
/// notification succeed or fail together. Notifications must never contain phone numbers or addresses.
/// </summary>
public interface INotifier
{
    Task ToUserAsync(
        Guid userId, NotificationType type, string title, string message,
        string? relatedEntityType, Guid? relatedEntityId, CancellationToken ct = default);

    Task ToCustomerAsync(
        Guid customerId, NotificationType type, string title, string message,
        string? relatedEntityType, Guid? relatedEntityId, CancellationToken ct = default);

    Task ToProviderAsync(
        Guid providerId, NotificationType type, string title, string message,
        string? relatedEntityType, Guid? relatedEntityId, CancellationToken ct = default);

    /// <summary>The verified providers who offer this service in this city, best rated first (up to a limit).</summary>
    Task ToMatchingProvidersAsync(
        Guid serviceId, int cityId, string title, string message, Guid requestId, CancellationToken ct = default);
}