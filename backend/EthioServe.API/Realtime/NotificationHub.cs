using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace EthioServe.API.Realtime;

/// <summary>
/// The server sends "notification" events to the signed-in user they belong to. Clients can call nothing here, so
/// there is nothing to abuse.
/// </summary>
[Authorize]
public sealed class NotificationHub : Hub
{
}