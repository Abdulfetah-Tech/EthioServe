using EthioServe.Application.Common;
using EthioServe.Application.Notifications;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EthioServe.API.Controllers;

/// <summary>The signed-in user's own notifications, for every role. There is no user id in any route: it comes from the token.</summary>
[ApiController]
[Authorize]
[Route("api/notifications")]
public sealed class NotificationsController(INotificationService notifications) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<PagedResult<NotificationDto>>> List([FromQuery] NotificationQuery query, CancellationToken ct) =>
        Ok(await notifications.ListAsync(query, ct));

    [HttpGet("unread-count")]
    public async Task<ActionResult<UnreadCountDto>> UnreadCount(CancellationToken ct) =>
        Ok(new UnreadCountDto(await notifications.UnreadCountAsync(ct)));

    [HttpPost("{id:guid}/read")]
    public async Task<IActionResult> MarkRead(Guid id, CancellationToken ct)
    {
        await notifications.MarkReadAsync(id, ct);

        return NoContent();
    }

    [HttpPost("read-all")]
    public async Task<ActionResult<MarkedReadDto>> MarkAllRead(CancellationToken ct) =>
        Ok(new MarkedReadDto(await notifications.MarkAllReadAsync(ct)));
}