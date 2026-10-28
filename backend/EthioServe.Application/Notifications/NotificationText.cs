namespace EthioServe.Application.Notifications;

/// <summary>Keeps notification text inside the database limits, so a long request title can never make a notification fail.</summary>
public static class NotificationText
{
    public const int MaxTitle = 150;

    public const int MaxMessage = 1000;

    public static string Title(string value) => Clip(value, MaxTitle);

    public static string Message(string value) => Clip(value, MaxMessage);

    public static string Clip(string value, int max)
    {
        var text = value.Trim();

        return text.Length <= max ? text : text[..(max - 3)].TrimEnd() + "...";
    }
}