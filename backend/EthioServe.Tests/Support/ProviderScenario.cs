using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using EthioServe.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace EthioServe.Tests.Support;

/// <summary>Builds marketplace data through the public API, exactly the way a real client would.</summary>
public sealed class ProviderScenario(ApiFactory factory, ApiSession session)
{
    public static string Unique(string prefix) => $"{prefix}-{Guid.NewGuid():N}"[..28];

    public static async Task<(int RegionId, int CityId)> LocationAsync(HttpClient client, string regionName, string cityName)
    {
        var regions = await client.GetFromJsonAsync<JsonElement>("/api/locations/regions");
        var regionId = regions.EnumerateArray()
            .Single(r => r.GetProperty("name").GetString() == regionName).GetProperty("id").GetInt32();

        var cities = await client.GetFromJsonAsync<JsonElement>($"/api/locations/regions/{regionId}/cities");
        var cityId = cities.EnumerateArray()
            .Single(c => c.GetProperty("name").GetString() == cityName).GetProperty("id").GetInt32();

        return (regionId, cityId);
    }

    /// <summary>A brand-new category with one active service, so every test works on its own isolated catalog.</summary>
    public async Task<(Guid CategoryId, Guid ServiceId)> NewServiceAsync(HttpClient admin)
    {
        var (status, category) = await ApiJson.SendAsync(
            admin, HttpMethod.Post, "/api/admin/categories", new { name = Unique("Cat") });
        Assert.Equal(HttpStatusCode.Created, status);

        var categoryId = category.GetProperty("id").GetGuid();

        return (categoryId, await AddServiceAsync(admin, categoryId));
    }

    public async Task<Guid> AddServiceAsync(HttpClient admin, Guid categoryId)
    {
        var (status, body) = await ApiJson.SendAsync(admin, HttpMethod.Post, "/api/admin/services",
            new { categoryId, name = Unique("Svc"), basePrice = 100m, pricingType = "Fixed" });
        Assert.Equal(HttpStatusCode.Created, status);

        return body.GetProperty("id").GetGuid();
    }

    public async Task CompleteProfileAsync(
        SignedInClient provider, int years = 10, string region = "Addis Ababa", string city = "Addis Ababa")
    {
        var (regionId, cityId) = await LocationAsync(provider.Http, region, city);
        var (_, profile) = await ApiJson.SendAsync(provider.Http, HttpMethod.Get, "/api/providers/me");

        var (status, _) = await ApiJson.SendAsync(provider.Http, HttpMethod.Put, "/api/providers/me", new
        {
            businessName = profile.GetProperty("businessName").GetString(),
            description = "Experienced professional serving customers across the city.",
            phoneNumber = profile.GetProperty("phoneNumber").GetString(),
            yearsOfExperience = years,
            regionId,
            cityId,
            subCityId = (int?)null,
            address = "Bole, near the main road",
            latitude = (decimal?)null,
            longitude = (decimal?)null
        });
        Assert.Equal(HttpStatusCode.OK, status);
    }

    public async Task OfferAsync(SignedInClient provider, Guid serviceId, decimal price)
    {
        var (status, _) = await ApiJson.SendAsync(
            provider.Http, HttpMethod.Post, "/api/providers/me/services", new { serviceId, price, description = "Offer" });
        Assert.Equal(HttpStatusCode.Created, status);
    }

    public async Task DeactivateOffersAsync(SignedInClient provider)
    {
        var (_, offers) = await ApiJson.SendAsync(provider.Http, HttpMethod.Get, "/api/providers/me/services");

        foreach (var offer in offers.EnumerateArray())
        {
            var (status, _) = await ApiJson.SendAsync(
                provider.Http, HttpMethod.Put, $"/api/providers/me/services/{offer.GetProperty("id").GetGuid()}",
                new { price = offer.GetProperty("price").GetDecimal(), description = "Paused", isActive = false });
            Assert.Equal(HttpStatusCode.OK, status);
        }
    }

    /// <summary>Registered, profile completed, one offer, verification requested: waiting for an admin.</summary>
    public async Task<SignedInClient> PendingProviderAsync(
        string businessName, Guid serviceId, decimal price, int years = 10,
        string region = "Addis Ababa", string city = "Addis Ababa")
    {
        var provider = await session.RegisteredProviderAsync(businessName);
        await CompleteProfileAsync(provider, years, region, city);
        await OfferAsync(provider, serviceId, price);

        var (status, _) = await ApiJson.SendAsync(provider.Http, HttpMethod.Post, "/api/providers/me/verification-request");
        Assert.Equal(HttpStatusCode.OK, status);

        return provider;
    }

    public async Task<SignedInClient> VerifiedProviderAsync(
        HttpClient admin, string businessName, Guid serviceId, decimal price, int years = 10,
        string region = "Addis Ababa", string city = "Addis Ababa")
    {
        var provider = await PendingProviderAsync(businessName, serviceId, price, years, region, city);
        await ApproveAsync(admin, provider);

        return provider;
    }

    public static async Task ApproveAsync(HttpClient admin, SignedInClient provider)
    {
        var (status, _) = await ApiJson.SendAsync(admin, HttpMethod.Post, $"/api/admin/providers/{provider.ProfileId}/approve");
        Assert.Equal(HttpStatusCode.OK, status);
    }

    public static async Task RejectAsync(HttpClient admin, SignedInClient provider, string reason = "Details are unclear")
    {
        var (status, _) = await ApiJson.SendAsync(
            admin, HttpMethod.Post, $"/api/admin/providers/{provider.ProfileId}/reject", new { reason });
        Assert.Equal(HttpStatusCode.OK, status);
    }

    public static async Task SuspendAsync(HttpClient admin, SignedInClient provider, string reason = "Customer complaints")
    {
        var (status, _) = await ApiJson.SendAsync(
            admin, HttpMethod.Post, $"/api/admin/providers/{provider.ProfileId}/suspend", new { reason });
        Assert.Equal(HttpStatusCode.OK, status);
    }

    /// <summary>Ratings come from customer reviews (a later phase), so tests set them directly in the database.</summary>
    public async Task SetRatingAsync(SignedInClient provider, decimal rating, int reviews)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        await db.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE provider_profiles SET average_rating = {rating}, total_reviews = {reviews} WHERE id = {provider.ProfileId}");
    }
}