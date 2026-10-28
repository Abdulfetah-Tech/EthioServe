using System.Text.Json.Serialization;
using EthioServe.Application.Notifications;
using Microsoft.AspNetCore.SignalR;

namespace EthioServe.API.Realtime;

public static class RealtimeExtensions
{
    public const string HubPath = "/hubs/notifications";

    public static IServiceCollection AddRealtime(this IServiceCollection services)
    {
        // Enums travel as readable text, the same as in the REST API.
        services.AddSignalR().AddJsonProtocol(options => options.PayloadSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
        services.AddSingleton<IUserIdProvider, SubUserIdProvider>();
        services.AddSingleton<INotificationPublisher, SignalRNotificationPublisher>();

        return services;
    }

    public static WebApplication MapRealtime(this WebApplication app)
    {
        app.MapHub<NotificationHub>(HubPath);

        return app;
    }
}