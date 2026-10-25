using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace EthioServe.Tests.Support;

public sealed record Downloaded(
    HttpStatusCode Status, byte[] Bytes, string? ContentType, string? ContentSecurityPolicy, string? NoSniff);

/// <summary>Builds small files that start with real image signatures, and talks to the photo endpoints.</summary>
public static class AttachmentScenario
{
    public static byte[] Jpeg(int extra = 64) => Make([0xFF, 0xD8, 0xFF, 0xE0], extra);

    public static byte[] Png(int extra = 64) => Make([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A], extra);

    public static byte[] WebP(int extra = 64)
    {
        var header = new byte[12];
        Encoding.ASCII.GetBytes("RIFF").CopyTo(header, 0);
        Encoding.ASCII.GetBytes("WEBP").CopyTo(header, 8);

        return Make(header, extra);
    }

    private static byte[] Make(byte[] header, int extra)
    {
        var bytes = new byte[header.Length + extra];
        header.CopyTo(bytes, 0);

        for (var i = header.Length; i < bytes.Length; i++)
        {
            bytes[i] = (byte)(i % 251);
        }

        return bytes;
    }

    public static async Task<(HttpStatusCode Status, JsonElement Body)> UploadAsync(
        HttpClient client, Guid requestId, byte[] bytes, string fileName = "photo.png", string contentType = "image/png")
    {
        using var form = new MultipartFormDataContent();
        var file = new ByteArrayContent(bytes);
        file.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        form.Add(file, "file", fileName);

        using var response = await client.PostAsync($"/api/requests/{requestId}/attachments", form);

        return (response.StatusCode, await ReadJsonAsync(response));
    }

    public static async Task<Downloaded> DownloadAsync(HttpClient client, Guid requestId, Guid attachmentId)
    {
        using var response = await client.GetAsync($"/api/requests/{requestId}/attachments/{attachmentId}");
        var bytes = await response.Content.ReadAsByteArrayAsync();

        return new Downloaded(
            response.StatusCode,
            bytes,
            response.Content.Headers.ContentType?.MediaType,
            FirstHeader(response, "Content-Security-Policy"),
            FirstHeader(response, "X-Content-Type-Options"));
    }

    private static string? FirstHeader(HttpResponseMessage response, string name) =>
        response.Headers.TryGetValues(name, out var values) ? values.FirstOrDefault() : null;

    private static async Task<JsonElement> ReadJsonAsync(HttpResponseMessage response)
    {
        var text = await response.Content.ReadAsStringAsync();

        if (string.IsNullOrWhiteSpace(text))
        {
            return default;
        }

        using var document = JsonDocument.Parse(text);

        return document.RootElement.Clone();
    }
}