using System.Net;
using System.Text.Json;
using EthioServe.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace EthioServe.Tests.Support;

public static class QuoteScenario
{
    public static object Body(decimal amount = 500m, string? description = null, int minutes = 120, int days = 7) =>
        new
        {
            amount,
            description = description ?? "Parts and labour included, finished in one visit.",
            estimatedDurationMinutes = minutes,
            validForDays = days
        };

    public static Task<(HttpStatusCode Status, JsonElement Body)> SubmitRawAsync(HttpClient provider, Guid requestId, object body) =>
        ApiJson.SendAsync(provider, HttpMethod.Post, $"/api/providers/me/requests/{requestId}/quotes", body);

    public static async Task<Guid> SubmitAsync(HttpClient provider, Guid requestId, decimal amount = 500m)
    {
        var (status, body) = await SubmitRawAsync(provider, requestId, Body(amount));
        Assert.Equal(HttpStatusCode.Created, status);

        return body.GetProperty("id").GetGuid();
    }

    public static async Task<string> RequestStatusAsync(HttpClient customer, Guid requestId)
    {
        var (status, body) = await ApiJson.SendAsync(customer, HttpMethod.Get, $"/api/requests/{requestId}");
        Assert.Equal(HttpStatusCode.OK, status);

        return body.GetProperty("status").GetString()!;
    }

    /// <summary>Moves a quote's deadline into the past, as if time had passed.</summary>
    public static async Task ExpireAsync(ApiFactory factory, Guid quoteId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var past = DateTimeOffset.UtcNow.AddDays(-1);

        await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE quotes SET valid_until = {past} WHERE id = {quoteId}");
    }
}