using System.Net;

namespace EthioServe.Tests.Support;

public static class RequestScenario
{
    /// <summary>A date relative to today in Addis Ababa time (UTC+3), the same clock the API uses.</summary>
    public static DateOnly InDays(int days) => DateOnly.FromDateTime(DateTime.UtcNow.AddHours(3)).AddDays(days);

    public static object Body(
        Guid serviceId, int regionId, int cityId, string? title = null, string? description = null,
        decimal? budgetMin = null, decimal? budgetMax = null, DateOnly? date = null, TimeOnly? time = null) =>
        new
        {
            serviceId,
            title = title ?? "Leaking kitchen pipe",
            description = description ?? "The pipe under the kitchen sink leaks and needs repair soon.",
            address = "Bole, near the main road",
            regionId,
            cityId,
            budgetMin,
            budgetMax,
            preferredDate = date,
            preferredTime = time
        };

    public static async Task<Guid> CreateAsync(
        HttpClient customer, Guid serviceId, int regionId, int cityId, string? title = null)
    {
        var (status, body) = await ApiJson.SendAsync(
            customer, HttpMethod.Post, "/api/requests", Body(serviceId, regionId, cityId, title));

        Assert.Equal(HttpStatusCode.Created, status);

        return body.GetProperty("id").GetGuid();
    }
}