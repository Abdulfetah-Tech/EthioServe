using EthioServe.Application.Abstractions;
using EthioServe.Application.Common;
using EthioServe.Application.Common.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace EthioServe.Application.Notifications;

public sealed class NotificationService(IApplicationDbContext db, ICurrentUser currentUser) : INotificationService
{
    /// <summary>"Mark all read" handles this many at a time, so one request can never touch an unbounded number of rows.</summary>
    private const int MaxBulk = 500;

    // The user comes from the signed-in user's token, never from the request.
    private Guid UserId => currentUser.UserId ?? throw new ForbiddenException("You must be signed in.");

    public async Task<PagedResult<NotificationDto>> ListAsync(NotificationQuery query, CancellationToken ct = default)
    {
        var userId = UserId;
        var (page, pageSize) = Paging.Normalize(query.Page, query.PageSize);

        var rows = db.Notifications.AsNoTracking().Where(n => n.UserId == userId);

        if (query.UnreadOnly)
        {
            rows = rows.Where(n => !n.IsRead);
        }

        var total = await rows.CountAsync(ct);

        var items = await rows
            .OrderByDescending(n => n.CreatedAt).ThenByDescending(n => n.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(n => new NotificationDto(
                n.Id, n.Type, n.Title, n.Message, n.IsRead, n.RelatedEntityType, n.RelatedEntityId, n.CreatedAt))
            .ToListAsync(ct);

        return new PagedResult<NotificationDto>(items, page, pageSize, total);
    }

    public async Task<int> UnreadCountAsync(CancellationToken ct = default)
    {
        var userId = UserId;

        return await db.Notifications.CountAsync(n => n.UserId == userId && !n.IsRead, ct);
    }

    // Looked up by id AND owner: someone else's notification is simply "not found".
    public async Task MarkReadAsync(Guid id, CancellationToken ct = default)
    {
        var userId = UserId;

        var notification = await db.Notifications.FirstOrDefaultAsync(n => n.Id == id && n.UserId == userId, ct)
                           ?? throw new NotFoundException("Notification not found.");

        if (!notification.IsRead)
        {
            notification.MarkRead();
            await db.SaveChangesAsync(ct);
        }
    }

    public async Task<int> MarkAllReadAsync(CancellationToken ct = default)
    {
        var userId = UserId;

        var unread = await db.Notifications
            .Where(n => n.UserId == userId && !n.IsRead)
            .OrderBy(n => n.CreatedAt)
            .Take(MaxBulk)
            .ToListAsync(ct);

        foreach (var notification in unread)
        {
            notification.MarkRead();
        }

        if (unread.Count > 0)
        {
            await db.SaveChangesAsync(ct);
        }

        return unread.Count;
    }
}