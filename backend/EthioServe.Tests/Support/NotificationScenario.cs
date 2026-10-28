using System.Net;
using System.Text.Json;

namespace EthioServe.Tests.Support;

public static class NotificationScenario
{
    public static async Task<List<JsonElement>> ListAsync(HttpClient client, string query = "")
    {
        var (status, body) = await ApiJson.SendAsync(client, HttpMethod.Get, $"/api/notifications{query}");
        Assert.Equal(HttpStatusCode.OK, status);

        return body.GetProperty("items").EnumerateArray().ToList();
    }

    public static async Task<int> UnreadAsync(HttpClient client)
    {
        var (status, body) = await ApiJson.SendAsync(client, HttpMethod.Get, "/api/notifications/unread-count");
        Assert.Equal(HttpStatusCode.OK, status);

        return body.GetProperty("count").GetInt32();
    }

    public static List<JsonElement> OfType(IEnumerable<JsonElement> items, string type) =>
        items.Where(i => i.GetProperty("type").GetString() == type).ToList();

    public static List<string> Titles(IEnumerable<JsonElement> items) =>
        items.Select(i => i.GetProperty("title").GetString()!).ToList();

    /// <summary>Every title and message a user has, so a test can check that nothing private leaked into them.</summary>
    public static async Task<string> AllTextAsync(HttpClient client)
    {
        var items = await ListAsync(client, "?pageSize=50");

        return string.Concat(items.Select(i => i.GetProperty("title").GetString() + " " + i.GetProperty("message").GetString() + " "));
    }
}