using EthioServe.Application.Common;
using Microsoft.AspNetCore.SignalR;

namespace EthioServe.API.Realtime;

/// <summary>Identifies a connection by the token's "sub" claim, the same user id the rest of the API uses.</summary>
public sealed class SubUserIdProvider : IUserIdProvider
{
    public string? GetUserId(HubConnectionContext connection) => connection.User.FindFirst(AppClaims.Subject)?.Value;
}