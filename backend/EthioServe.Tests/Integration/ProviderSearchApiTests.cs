using System.Net;
using System.Text.Json;
using EthioServe.Tests.Support;

namespace EthioServe.Tests.Integration;

[Collection(ApiCollection.Name)]
public sealed class ProviderSearchApiTests(ApiFactory factory)
{
    private readonly ApiSession _session = new(factory);
    private readonly ProviderScenario _scenario = new(factory, new ApiSession(factory));

    private static string Unique(string prefix) => ProviderScenario.Unique(prefix);

    private static List<string> BusinessNames(JsonElement result) =>
        result.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("businessName").GetString()!).ToList();

    private async Task<List<string>> SearchNamesAsync(string query)
    {
        using var anonymous = _session.CreateAnonymousClient();
        var (status, body, _) = await ApiJson.GetAsync(anonymous, $"/api/providers?{query}");
        Assert.Equal(HttpStatusCode.OK, status);

        return BusinessNames(body);
    }

    // ------------------------------------------------------------- who is visible

    [Fact]
    public async Task Only_verified_providers_with_an_active_offer_are_listed()
    {
        using var admin = await _session.AdminAsync();
        var (_, serviceId) = await _scenario.NewServiceAsync(admin.Http);

        var verifiedName = Unique("Verified");
        using var verified = await _scenario.VerifiedProviderAsync(admin.Http, verifiedName, serviceId, 300m);

        using var unverified = await _session.RegisteredProviderAsync(Unique("Unverified"));
        await _scenario.CompleteProfileAsync(unverified);
        await _scenario.OfferAsync(unverified, serviceId, 100m);

        using var pending = await _scenario.PendingProviderAsync(Unique("Pending"), serviceId, 100m);

        using var rejected = await _scenario.PendingProviderAsync(Unique("Rejected"), serviceId, 100m);
        await ProviderScenario.RejectAsync(admin.Http, rejected);

        using var suspended = await _scenario.VerifiedProviderAsync(admin.Http, Unique("Suspended"), serviceId, 100m);
        await ProviderScenario.SuspendAsync(admin.Http, suspended);

        using var paused = await _scenario.VerifiedProviderAsync(admin.Http, Unique("Paused"), serviceId, 100m);
        await _scenario.DeactivateOffersAsync(paused);

        var names = await SearchNamesAsync($"serviceId={serviceId}");

        Assert.Equal(new[] { verifiedName }, names);
    }

    [Fact]
    public async Task Deactivated_services_and_categories_hide_their_providers()
    {
        using var admin = await _session.AdminAsync();
        var (categoryId, serviceId) = await _scenario.NewServiceAsync(admin.Http);
        var name = Unique("Hide");
        using var provider = await _scenario.VerifiedProviderAsync(admin.Http, name, serviceId, 100m);

        Assert.Contains(name, await SearchNamesAsync($"serviceId={serviceId}"));

        await ApiJson.SendAsync(admin.Http, HttpMethod.Post, $"/api/admin/services/{serviceId}/deactivate");
        Assert.Empty(await SearchNamesAsync($"serviceId={serviceId}"));

        await ApiJson.SendAsync(admin.Http, HttpMethod.Post, $"/api/admin/services/{serviceId}/activate");
        Assert.Contains(name, await SearchNamesAsync($"serviceId={serviceId}"));

        await ApiJson.SendAsync(admin.Http, HttpMethod.Post, $"/api/admin/categories/{categoryId}/deactivate");
        Assert.Empty(await SearchNamesAsync($"serviceId={serviceId}"));
    }

    // ------------------------------------------------------------- filters

    [Fact]
    public async Task Providers_can_be_filtered_by_service_and_by_category()
    {
        using var admin = await _session.AdminAsync();
        var (categoryId, serviceA) = await _scenario.NewServiceAsync(admin.Http);
        var serviceB = await _scenario.AddServiceAsync(admin.Http, categoryId);

        var onlyA = Unique("OnlyA");
        var onlyB = Unique("OnlyB");
        var both = Unique("Both");
        using var p1 = await _scenario.VerifiedProviderAsync(admin.Http, onlyA, serviceA, 100m);
        using var p2 = await _scenario.VerifiedProviderAsync(admin.Http, onlyB, serviceB, 100m);
        using var p3 = await _scenario.VerifiedProviderAsync(admin.Http, both, serviceA, 100m);
        await _scenario.OfferAsync(p3, serviceB, 100m);

        var forA = await SearchNamesAsync($"serviceId={serviceA}");
        var forB = await SearchNamesAsync($"serviceId={serviceB}");
        var forCategory = await SearchNamesAsync($"categoryId={categoryId}");

        Assert.Equal(new[] { onlyA, both }.Order(), forA.Order());
        Assert.Equal(new[] { onlyB, both }.Order(), forB.Order());
        Assert.Equal(new[] { onlyA, onlyB, both }.Order(), forCategory.Order());
    }

    [Fact]
    public async Task The_starting_price_is_the_cheapest_matching_offer()
    {
        using var admin = await _session.AdminAsync();
        var (categoryId, serviceA) = await _scenario.NewServiceAsync(admin.Http);
        var serviceB = await _scenario.AddServiceAsync(admin.Http, categoryId);
        using var provider = await _scenario.VerifiedProviderAsync(admin.Http, Unique("Price"), serviceA, 500m);
        await _scenario.OfferAsync(provider, serviceB, 200m);
        using var anonymous = _session.CreateAnonymousClient();

        var (_, byCategory, _) = await ApiJson.GetAsync(anonymous, $"/api/providers?categoryId={categoryId}");
        var (_, byServiceA, _) = await ApiJson.GetAsync(anonymous, $"/api/providers?serviceId={serviceA}");

        Assert.Equal(200m, byCategory.GetProperty("items").EnumerateArray().Single().GetProperty("startingPrice").GetDecimal());
        Assert.Equal(500m, byServiceA.GetProperty("items").EnumerateArray().Single().GetProperty("startingPrice").GetDecimal());
    }

    [Fact]
    public async Task Providers_can_be_filtered_by_city_and_region()
    {
        using var admin = await _session.AdminAsync();
        using var anonymous = _session.CreateAnonymousClient();
        var (_, serviceId) = await _scenario.NewServiceAsync(admin.Http);
        var addisName = Unique("Addis");
        var adamaName = Unique("Adama");
        using var inAddis = await _scenario.VerifiedProviderAsync(admin.Http, addisName, serviceId, 100m);
        using var inAdama = await _scenario.VerifiedProviderAsync(
            admin.Http, adamaName, serviceId, 100m, region: "Oromia", city: "Adama");
        var (addisRegion, addisCity) = await ProviderScenario.LocationAsync(anonymous, "Addis Ababa", "Addis Ababa");
        var (oromiaRegion, adamaCity) = await ProviderScenario.LocationAsync(anonymous, "Oromia", "Adama");

        Assert.Equal(new[] { adamaName }, await SearchNamesAsync($"serviceId={serviceId}&cityId={adamaCity}"));
        Assert.Equal(new[] { addisName }, await SearchNamesAsync($"serviceId={serviceId}&cityId={addisCity}"));
        Assert.Equal(new[] { adamaName }, await SearchNamesAsync($"serviceId={serviceId}&regionId={oromiaRegion}"));
        Assert.Equal(new[] { addisName }, await SearchNamesAsync($"serviceId={serviceId}&regionId={addisRegion}"));
    }

    [Fact]
    public async Task Providers_can_be_filtered_by_minimum_rating()
    {
        using var admin = await _session.AdminAsync();
        var (_, serviceId) = await _scenario.NewServiceAsync(admin.Http);
        var lowName = Unique("Low");
        var highName = Unique("High");
        using var low = await _scenario.VerifiedProviderAsync(admin.Http, lowName, serviceId, 100m);
        using var high = await _scenario.VerifiedProviderAsync(admin.Http, highName, serviceId, 100m);
        await _scenario.SetRatingAsync(low, 3.5m, 8);
        await _scenario.SetRatingAsync(high, 4.5m, 12);

        Assert.Equal(new[] { highName }, await SearchNamesAsync($"serviceId={serviceId}&minRating=4"));
        Assert.Equal(2, (await SearchNamesAsync($"serviceId={serviceId}&minRating=0")).Count);
        Assert.Empty(await SearchNamesAsync($"serviceId={serviceId}&minRating=5"));
        Assert.Empty(await SearchNamesAsync($"serviceId={serviceId}&minRating=9")); // clamped to 5, not an error
    }

    [Fact]
    public async Task Available_only_leaves_out_providers_who_switched_availability_off()
    {
        using var admin = await _session.AdminAsync();
        var (_, serviceId) = await _scenario.NewServiceAsync(admin.Http);
        var busyName = Unique("Busy");
        var freeName = Unique("Free");
        using var busy = await _scenario.VerifiedProviderAsync(admin.Http, busyName, serviceId, 100m);
        using var free = await _scenario.VerifiedProviderAsync(admin.Http, freeName, serviceId, 100m);
        await ApiJson.SendAsync(busy.Http, HttpMethod.Put, "/api/providers/me/availability", new { isAvailable = false });

        Assert.Equal(new[] { freeName }, await SearchNamesAsync($"serviceId={serviceId}&availableOnly=true"));
        Assert.Equal(2, (await SearchNamesAsync($"serviceId={serviceId}")).Count);
    }

    [Fact]
    public async Task The_name_search_is_partial_and_ignores_case()
    {
        using var admin = await _session.AdminAsync();
        var (_, serviceId) = await _scenario.NewServiceAsync(admin.Http);
        var token = Unique("Needle");
        var name = $"{token} Plumbing";
        using var match = await _scenario.VerifiedProviderAsync(admin.Http, name, serviceId, 100m);
        using var other = await _scenario.VerifiedProviderAsync(admin.Http, Unique("Other"), serviceId, 100m);

        Assert.Equal(new[] { name }, await SearchNamesAsync($"serviceId={serviceId}&search={token.ToUpperInvariant()}"));
        Assert.Empty(await SearchNamesAsync($"serviceId={serviceId}&search=%25")); // '%' is treated literally
    }

    // ------------------------------------------------------------- sorting and paging

    [Fact]
    public async Task Results_can_be_sorted_by_rating_price_experience_and_name()
    {
        using var admin = await _session.AdminAsync();
        var (_, serviceId) = await _scenario.NewServiceAsync(admin.Http);
        var token = Unique("Sort");
        var a = $"{token}-A";
        var b = $"{token}-B";
        var c = $"{token}-C";

        //          price  years  rating
        // A         500     15     4.0
        // B         300      5     5.0
        // C         400     10     3.0
        using var pa = await _scenario.VerifiedProviderAsync(admin.Http, a, serviceId, 500m, years: 15);
        using var pb = await _scenario.VerifiedProviderAsync(admin.Http, b, serviceId, 300m, years: 5);
        using var pc = await _scenario.VerifiedProviderAsync(admin.Http, c, serviceId, 400m, years: 10);
        await _scenario.SetRatingAsync(pa, 4.0m, 10);
        await _scenario.SetRatingAsync(pb, 5.0m, 10);
        await _scenario.SetRatingAsync(pc, 3.0m, 10);

        Assert.Equal(new[] { b, a, c }, await SearchNamesAsync($"serviceId={serviceId}&sort=Rating"));
        Assert.Equal(new[] { b, c, a }, await SearchNamesAsync($"serviceId={serviceId}&sort=Price"));
        Assert.Equal(new[] { a, c, b }, await SearchNamesAsync($"serviceId={serviceId}&sort=Experience"));
        Assert.Equal(new[] { a, b, c }, await SearchNamesAsync($"serviceId={serviceId}&sort=Name"));
        Assert.Equal(new[] { b, a, c }, await SearchNamesAsync($"serviceId={serviceId}")); // rating is the default
    }

    [Fact]
    public async Task Results_are_paged_with_stable_ordering_and_complete_metadata()
    {
        using var admin = await _session.AdminAsync();
        using var anonymous = _session.CreateAnonymousClient();
        var (_, serviceId) = await _scenario.NewServiceAsync(admin.Http);
        using var p1 = await _scenario.VerifiedProviderAsync(admin.Http, Unique("Pg"), serviceId, 100m);
        using var p2 = await _scenario.VerifiedProviderAsync(admin.Http, Unique("Pg"), serviceId, 100m);
        using var p3 = await _scenario.VerifiedProviderAsync(admin.Http, Unique("Pg"), serviceId, 100m);

        var (_, first, _) = await ApiJson.GetAsync(anonymous, $"/api/providers?serviceId={serviceId}&pageSize=2&page=1");
        var (_, second, _) = await ApiJson.GetAsync(anonymous, $"/api/providers?serviceId={serviceId}&pageSize=2&page=2");

        Assert.Equal(3, first.GetProperty("totalCount").GetInt32());
        Assert.Equal(2, first.GetProperty("totalPages").GetInt32());
        Assert.Equal(2, first.GetProperty("pageSize").GetInt32());
        Assert.Equal(2, first.GetProperty("items").GetArrayLength());
        Assert.Equal(1, second.GetProperty("items").GetArrayLength());
        Assert.Empty(BusinessNames(first).Intersect(BusinessNames(second)));
    }

    [Fact]
    public async Task Oversized_page_sizes_are_clamped_and_unknown_sort_values_are_rejected()
    {
        using var anonymous = _session.CreateAnonymousClient();

        var (_, clamped, _) = await ApiJson.GetAsync(anonymous, "/api/providers?pageSize=1000");
        var (badSort, _, _) = await ApiJson.GetAsync(anonymous, "/api/providers?sort=banana");

        Assert.Equal(50, clamped.GetProperty("pageSize").GetInt32());
        Assert.Equal(HttpStatusCode.BadRequest, badSort);
    }

    // ------------------------------------------------------------- privacy and detail

    [Fact]
    public async Task Public_responses_never_expose_private_contact_data()
    {
        using var admin = await _session.AdminAsync();
        using var anonymous = _session.CreateAnonymousClient();
        var (_, serviceId) = await _scenario.NewServiceAsync(admin.Http);
        using var provider = await _scenario.VerifiedProviderAsync(admin.Http, Unique("Private"), serviceId, 100m);

        var (_, search, _) = await ApiJson.GetAsync(anonymous, $"/api/providers?serviceId={serviceId}");
        var (_, detail, _) = await ApiJson.GetAsync(anonymous, $"/api/providers/{provider.ProfileId}");
        var item = search.GetProperty("items").EnumerateArray().Single();

        foreach (var forbidden in new[] { "phoneNumber", "address", "latitude", "longitude", "userId", "verificationNote", "verificationStatus" })
        {
            Assert.False(item.TryGetProperty(forbidden, out _), $"The search result must not contain '{forbidden}'.");
            Assert.False(detail.TryGetProperty(forbidden, out _), $"The provider page must not contain '{forbidden}'.");
        }
    }

    [Fact]
    public async Task The_provider_page_is_public_for_verified_providers_and_lists_only_active_services()
    {
        using var admin = await _session.AdminAsync();
        using var anonymous = _session.CreateAnonymousClient();
        var (categoryId, serviceA) = await _scenario.NewServiceAsync(admin.Http);
        var serviceB = await _scenario.AddServiceAsync(admin.Http, categoryId);
        using var verified = await _scenario.VerifiedProviderAsync(admin.Http, Unique("Page"), serviceA, 100m);
        await _scenario.OfferAsync(verified, serviceB, 200m);
        using var pending = await _scenario.PendingProviderAsync(Unique("Waiting"), serviceA, 100m);

        // Switch off the offer for service B only.
        var (_, offers) = await ApiJson.SendAsync(verified.Http, HttpMethod.Get, "/api/providers/me/services");
        var offerB = offers.EnumerateArray().Single(o => o.GetProperty("serviceId").GetGuid() == serviceB);
        await ApiJson.SendAsync(verified.Http, HttpMethod.Put, $"/api/providers/me/services/{offerB.GetProperty("id").GetGuid()}",
            new { price = 200m, description = "Paused", isActive = false });

        var (ok, detail, _) = await ApiJson.GetAsync(anonymous, $"/api/providers/{verified.ProfileId}");
        var (pendingStatus, _, pendingType) = await ApiJson.GetAsync(anonymous, $"/api/providers/{pending.ProfileId}");
        var (unknownStatus, _, _) = await ApiJson.GetAsync(anonymous, $"/api/providers/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.OK, ok);
        var service = Assert.Single(detail.GetProperty("services").EnumerateArray());
        Assert.Equal(serviceA, service.GetProperty("serviceId").GetGuid());
        Assert.Equal("Addis Ababa", detail.GetProperty("cityName").GetString());
        Assert.Equal(HttpStatusCode.NotFound, pendingStatus);
        Assert.Equal("application/problem+json", pendingType);
        Assert.Equal(HttpStatusCode.NotFound, unknownStatus);
    }
}