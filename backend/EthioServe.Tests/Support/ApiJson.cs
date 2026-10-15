using System.Net;
using System.Text.Json;

namespace EthioServe.Tests.Support;

public static class ApiJson
{
    public static async Task<(HttpStatusCode Status, JsonElement Body, string? ContentType)> GetAsync(
        HttpClient client, string url)
    {
        using var response = await client.GetAsync(url);
        var text = await response.Content.ReadAsStringAsync();

        if (string.IsNullOrWhiteSpace(text))
        {
            return (response.StatusCode, default, response.Content.Headers.ContentType?.MediaType);
        }

        using var document = JsonDocument.Parse(text);

        return (response.StatusCode, document.RootElement.Clone(), response.Content.Headers.ContentType?.MediaType);
    }

    public static List<string?> Names(JsonElement array) =>
        array.EnumerateArray().Select(item => item.GetProperty("name").GetString()).ToList();
}