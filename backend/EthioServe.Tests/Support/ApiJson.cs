using System.Net;
using System.Net.Http.Json;
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

    public static async Task<(HttpStatusCode Status, JsonElement Body)> SendAsync(
        HttpClient client, HttpMethod method, string url, object? body = null)
    {
        using var request = new HttpRequestMessage(method, url);

        if (body is not null)
        {
            request.Content = JsonContent.Create(body);
        }

        using var response = await client.SendAsync(request);
        var text = await response.Content.ReadAsStringAsync();

        if (string.IsNullOrWhiteSpace(text))
        {
            return (response.StatusCode, default);
        }

        using var document = JsonDocument.Parse(text);

        return (response.StatusCode, document.RootElement.Clone());
    }

    public static List<string?> Names(JsonElement array) =>
        array.EnumerateArray().Select(item => item.GetProperty("name").GetString()).ToList();
}