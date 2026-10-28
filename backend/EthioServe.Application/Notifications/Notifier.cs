using EthioServe.Application.Abstractions;
using EthioServe.Domain.Entities;
using EthioServe.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace EthioServe.Application.Notifications;

public sealed class Notifier(IApplicationDbContext db) : INotifier
{
    /// <summary>In a very busy city only the best-rated providers are alerted. Everyone can still see the request in their feed.</summary>
    public const int MaxMatchingProviders = 50;

    public Task ToUserAsync(
        Guid userId, NotificationType type, string title, string message,
        string? relatedEntityType, Guid? relatedEntityId, CancellationToken ct = default)
    {
        db.Notifications.Add(new Notification(
            userId, type, NotificationText.Title(title), NotificationText.Message(message), relatedEntityType, relatedEntityId));

        return Task.CompletedTask;
    }

    public async Task ToCustomerAsync(
        Guid customerId, NotificationType type, string title, string message,
        string? relatedEntityType, Guid? relatedEntityId, CancellationToken ct = default)
    {
        var userId = await db.CustomerProfiles.AsNoTracking()
            .Where(c => c.Id == customerId)
            .Select(c => (Guid?)c.UserId)
            .FirstOrDefaultAsync(ct);

        // A missing profile must never break the action that caused the notification.
        if (userId is { } id)
        {
            await ToUserAsync(id, type, title, message, relatedEntityType, relatedEntityId, ct);
        }
    }

    public async Task ToProviderAsync(
        Guid providerId, NotificationType type, string title, string message,
        string? relatedEntityType, Guid? relatedEntityId, CancellationToken ct = default)
    {
        var userId = await db.ProviderProfiles.AsNoTracking()
            .Where(p => p.Id == providerId)
            .Select(p => (Guid?)p.UserId)
            .FirstOrDefaultAsync(ct);

        if (userId is { } id)
        {
            await ToUserAsync(id, type, title, message, relatedEntityType, relatedEntityId, ct);
        }
    }

    public async Task ToMatchingProvidersAsync(
        Guid serviceId, int cityId, string title, string message, Guid requestId, CancellationToken ct = default)
    {
        var userIds = await (
                from p in db.ProviderProfiles.AsNoTracking()
                where p.VerificationStatus == VerificationStatus.Verified
                      && p.CityId == cityId
                      && db.ProviderServices.Any(o => o.ProviderId == p.Id && o.ServiceId == serviceId && o.IsActive)
                orderby p.AverageRating descending, p.Id
                select p.UserId)
            .Take(MaxMatchingProviders)
            .ToListAsync(ct);

        foreach (var userId in userIds)
        {
            await ToUserAsync(userId, NotificationType.NewServiceRequest, title, message, "ServiceRequest", requestId, ct);
        }
    }
}