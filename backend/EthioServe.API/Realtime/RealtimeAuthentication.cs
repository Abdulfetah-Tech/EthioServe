using Microsoft.AspNetCore.Authentication.JwtBearer;

namespace EthioServe.API.Realtime;

public static class RealtimeAuthentication
{
    /// <summary>
    /// A browser cannot set headers on a WebSocket, so the token arrives as ?access_token=. It is accepted only for the
    /// notification hub: on every other route a token in the address is ignored.
    /// </summary>
    public static JwtBearerEvents CreateEvents() => new()
    {
        OnMessageReceived = context =>
        {
            var token = context.Request.Query["access_token"];

            if (!string.IsNullOrEmpty(token) && context.HttpContext.Request.Path.StartsWithSegments(RealtimeExtensions.HubPath))
            {
                context.Token = token;
            }

            return Task.CompletedTask;
        }
    };
}