using System.Net;
using System.Text.Json;
using EthioServe.Tests.Support;

namespace EthioServe.Tests.Integration;

[Collection(ApiCollection.Name)]
public sealed class ProviderFeedApiTests(ApiFactory factory)
{
    private const string UnknownId = "00000000-0000-0000-0000-000000000001";

    private readonly ApiSession _session = new(factory);
    private readonly ProviderScenario _scenario = new(factory, new ApiSession(factory));

    public static IEnumerable<object[]> FeedRoutes()
    {
        yield return new object[] { "/api/providers/me/requests" };
        yield return new object[] { $"/api/providers/me/requests/{UnknownId}" };
    }

    private static string Unique(string prefix) => ProviderScenario.Unique(prefix);

    private static List<string> Titles(JsonElement page) =>
        page.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("title").GetString()!).ToList();

    private static async Task<JsonElement> FeedAsync(HttpClient provider, string query = "")
    {
        var (status, body) = await ApiJson.SendAsync(provider, HttpMethod.Get, $"/api/providers/me/requests{query}");
        Assert.Equal(HttpStatusCode.OK, status);

        return body;
    }

    // ------------------------------------------------------------- access

    [Theory]
    [MemberData(nameof(FeedRoutes))]
    public async Task Only_providers_can_use_the_feed(string url)
    {
        using var anonymous = _session.CreateAnonymousClient();
        using var customer = await _session.CustomerAsync();
        using var admin = await _session.AdminAsync();
        using var roleOnly = await _session.ProviderAsync(); // the Provider role, but no provider profile

        var (anonymousStatus, _) = await ApiJson.SendAsync(anonymous, HttpMethod.Get, url);
        Assert.Equal(HttpStatusCode.Unauthorized, anonymousStatus);

        foreach (var user in new[] { customer, admin, roleOnly })
        {
            var (status, _) = await ApiJson.SendAsync(user.Http, HttpMethod.Get, url);
            Assert.Equal(HttpStatusCode.Forbidden, status);
        }
    }

    [Fact]
    public async Task Providers_who_are_not_verified_cannot_see_requests()
    {
        using var admin = await _session.AdminAsync();
        var (_, serviceId) = await _scenario.NewServiceAsync(admin.Http);
        using var unverified = await _session.RegisteredProviderAsync();
        using var pending = await _scenario.PendingProviderAsync(Unique("Pending"), serviceId, 100m);
        using var rejected = await _scenario.PendingProviderAsync(Unique("Rejected"), serviceId, 100m);
        await ProviderScenario.RejectAsync(admin.Http, rejected);

        foreach (var provider in new[] { unverified, pending, rejected })
        {
            var (status, _) = await ApiJson.SendAsync(provider.Http, HttpMethod.Get, "/api/providers/me/requests");
            Assert.Equal(HttpStatusCode.Forbidden, status);
        }
    }

    [Fact]
    public async Task A_suspended_provider_loses_access_to_the_feed()
    {
        using var admin = await _session.AdminAsync();
        var (_, serviceId) = await _scenario.NewServiceAsync(admin.Http);
        using var provider = await _scenario.VerifiedProviderAsync(admin.Http, Unique("Susp"), serviceId, 100m);
        await FeedAsync(provider.Http);

        await ProviderScenario.SuspendAsync(admin.Http, provider);
        var (status, _) = await ApiJson.SendAsync(provider.Http, HttpMethod.Get, "/api/providers/me/requests");

        Assert.Equal(HttpStatusCode.Forbidden, status);
    }

    // ------------------------------------------------------------- who sees what

    [Fact]
    public async Task A_provider_sees_only_open_requests_for_their_services_in_their_city()
    {
        using var admin = await _session.AdminAsync();
        using var anonymous = _session.CreateAnonymousClient();
        var (categoryId, serviceA) = await _scenario.NewServiceAsync(admin.Http);
        var serviceB = await _scenario.AddServiceAsync(admin.Http, categoryId);
        var (addisRegion, addisCity) = await ProviderScenario.LocationAsync(anonymous, "Addis Ababa", "Addis Ababa");
        var (oromiaRegion, adamaCity) = await ProviderScenario.LocationAsync(anonymous, "Oromia", "Adama");
        using var provider = await _scenario.VerifiedProviderAsync(admin.Http, Unique("Feed"), serviceA, 300m);
        using var customer = await _session.CustomerAsync();

        var match = Unique("Match");
        await RequestScenario.CreateAsync(customer.Http, serviceA, addisRegion, addisCity, match);
        await RequestScenario.CreateAsync(customer.Http, serviceB, addisRegion, addisCity, Unique("OtherService"));
        await RequestScenario.CreateAsync(customer.Http, serviceA, oromiaRegion, adamaCity, Unique("OtherCity"));
        var cancelled = await RequestScenario.CreateAsync(customer.Http, serviceA, addisRegion, addisCity, Unique("Cancelled"));
        await ApiJson.SendAsync(customer.Http, HttpMethod.Post, $"/api/requests/{cancelled}/cancel", new { });

        var feed = await FeedAsync(provider.Http);

        Assert.Equal(new[] { match }, Titles(feed));
        Assert.Equal(1, feed.GetProperty("totalCount").GetInt32());
    }

    [Fact]
    public async Task Paused_offers_hide_the_requests_for_that_service()
    {
        using var admin = await _session.AdminAsync();
        using var anonymous = _session.CreateAnonymousClient();
        var (_, serviceId) = await _scenario.NewServiceAsync(admin.Http);
        var (regionId, cityId) = await ProviderScenario.LocationAsync(anonymous, "Addis Ababa", "Addis Ababa");
        using var provider = await _scenario.VerifiedProviderAsync(admin.Http, Unique("Pause"), serviceId, 300m);
        using var customer = await _session.CustomerAsync();
        var title = Unique("Paused");
        await RequestScenario.CreateAsync(customer.Http, serviceId, regionId, cityId, title);

        Assert.Equal(new[] { title }, Titles(await FeedAsync(provider.Http)));

        await _scenario.DeactivateOffersAsync(provider);

        Assert.Empty(Titles(await FeedAsync(provider.Http)));
    }

    [Fact]
    public async Task Deactivating_the_service_hides_its_requests_and_reactivating_brings_them_back()
    {
        using var admin = await _session.AdminAsync();
        using var anonymous = _session.CreateAnonymousClient();
        var (_, serviceId) = await _scenario.NewServiceAsync(admin.Http);
        var (regionId, cityId) = await ProviderScenario.LocationAsync(anonymous, "Addis Ababa", "Addis Ababa");
        using var provider = await _scenario.VerifiedProviderAsync(admin.Http, Unique("Svc"), serviceId, 300m);
        using var customer = await _session.CustomerAsync();
        var title = Unique("Hidden");
        await RequestScenario.CreateAsync(customer.Http, serviceId, regionId, cityId, title);

        await ApiJson.SendAsync(admin.Http, HttpMethod.Post, $"/api/admin/services/{serviceId}/deactivate");
        var hidden = Titles(await FeedAsync(provider.Http));
        await ApiJson.SendAsync(admin.Http, HttpMethod.Post, $"/api/admin/services/{serviceId}/activate");
        var restored = Titles(await FeedAsync(provider.Http));

        Assert.Empty(hidden);
        Assert.Equal(new[] { title }, restored);
    }

    // ------------------------------------------------------------- paging and filters

    [Fact]
    public async Task The_feed_is_paged_newest_first_and_can_be_filtered_by_service()
    {
        using var admin = await _session.AdminAsync();
        using var anonymous = _session.CreateAnonymousClient();
        var (categoryId, serviceA) = await _scenario.NewServiceAsync(admin.Http);
        var serviceB = await _scenario.AddServiceAsync(admin.Http, categoryId);
        var (regionId, cityId) = await ProviderScenario.LocationAsync(anonymous, "Addis Ababa", "Addis Ababa");
        using var provider = await _scenario.VerifiedProviderAsync(admin.Http, Unique("Page"), serviceA, 300m);
        await _scenario.OfferAsync(provider, serviceB, 200m);
        using var customer = await _session.CustomerAsync();

        var token = Unique("Pg");
        await RequestScenario.CreateAsync(customer.Http, serviceA, regionId, cityId, $"{token}-1");
        await RequestScenario.CreateAsync(customer.Http, serviceA, regionId, cityId, $"{token}-2");
        await RequestScenario.CreateAsync(customer.Http, serviceA, regionId, cityId, $"{token}-3");
        await RequestScenario.CreateAsync(customer.Http, serviceB, regionId, cityId, $"{token}-4");

        var first = await FeedAsync(provider.Http, "?pageSize=2");
        var second = await FeedAsync(provider.Http, "?pageSize=2&page=2");
        var onlyB = await FeedAsync(provider.Http, $"?serviceId={serviceB}");

        Assert.Equal(new[] { $"{token}-4", $"{token}-3" }, Titles(first));
        Assert.Equal(new[] { $"{token}-2", $"{token}-1" }, Titles(second));
        Assert.Equal(4, first.GetProperty("totalCount").GetInt32());
        Assert.Equal(2, first.GetProperty("totalPages").GetInt32());
        Assert.Equal(new[] { $"{token}-4" }, Titles(onlyB));
    }

    // ------------------------------------------------------------- detail and privacy

    [Fact]
    public async Task The_feed_never_exposes_the_address_or_who_asked()
    {
        using var admin = await _session.AdminAsync();
        using var anonymous = _session.CreateAnonymousClient();
        var (_, serviceId) = await _scenario.NewServiceAsync(admin.Http);
        var (regionId, cityId) = await ProviderScenario.LocationAsync(anonymous, "Addis Ababa", "Addis Ababa");
        using var provider = await _scenario.VerifiedProviderAsync(admin.Http, Unique("Priv"), serviceId, 300m);
        using var customer = await _session.CustomerAsync();
        var id = await RequestScenario.CreateAsync(customer.Http, serviceId, regionId, cityId, Unique("Private"));

        var feed = await FeedAsync(provider.Http);
        var (status, detail) = await ApiJson.SendAsync(provider.Http, HttpMethod.Get, $"/api/providers/me/requests/{id}");
        var item = feed.GetProperty("items").EnumerateArray().Single();

        Assert.Equal(HttpStatusCode.OK, status);
        Assert.Equal("Addis Ababa", detail.GetProperty("cityName").GetString());
        Assert.Contains("repair", detail.GetProperty("description").GetString());

        foreach (var forbidden in new[] { "address", "customerId", "userId", "phoneNumber", "customerName", "email" })
        {
            Assert.False(item.TryGetProperty(forbidden, out _), $"The feed item must not contain '{forbidden}'.");
            Assert.False(detail.TryGetProperty(forbidden, out _), $"The request detail must not contain '{forbidden}'.");
        }
    }

    [Fact]
    public async Task A_request_outside_the_feed_is_not_found()
    {
        using var admin = await _session.AdminAsync();
        using var anonymous = _session.CreateAnonymousClient();
        var (categoryId, serviceA) = await _scenario.NewServiceAsync(admin.Http);
        var serviceB = await _scenario.AddServiceAsync(admin.Http, categoryId);
        var (regionId, cityId) = await ProviderScenario.LocationAsync(anonymous, "Addis Ababa", "Addis Ababa");
        using var provider = await _scenario.VerifiedProviderAsync(admin.Http, Unique("Scope"), serviceA, 300m);
        using var customer = await _session.CustomerAsync();
        var notMine = await RequestScenario.CreateAsync(customer.Http, serviceB, regionId, cityId, Unique("NotMine"));

        var (other, _) = await ApiJson.SendAsync(provider.Http, HttpMethod.Get, $"/api/providers/me/requests/{notMine}");
        var (unknown, _) = await ApiJson.SendAsync(provider.Http, HttpMethod.Get, $"/api/providers/me/requests/{UnknownId}");

        Assert.Equal(HttpStatusCode.NotFound, other);
        Assert.Equal(HttpStatusCode.NotFound, unknown);
    }
}