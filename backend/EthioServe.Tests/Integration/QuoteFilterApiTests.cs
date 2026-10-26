using System.Net;
using EthioServe.Tests.Support;

namespace EthioServe.Tests.Integration;

[Collection(ApiCollection.Name)]
public sealed class QuoteFilterApiTests(ApiFactory factory)
{
    private readonly ApiSession _session = new(factory);
    private readonly ProviderScenario _scenario = new(factory, new ApiSession(factory));

    [Fact]
    public async Task A_provider_can_list_only_their_own_quotes_for_one_request()
    {
        using var admin = await _session.AdminAsync();
        using var anonymous = _session.CreateAnonymousClient();
        var (_, serviceId) = await _scenario.NewServiceAsync(admin.Http);
        var (regionId, cityId) = await ProviderScenario.LocationAsync(anonymous, "Addis Ababa", "Addis Ababa");
        using var provider = await _scenario.VerifiedProviderAsync(admin.Http, ProviderScenario.Unique("Filter"), serviceId, 300m);
        using var other = await _scenario.VerifiedProviderAsync(admin.Http, ProviderScenario.Unique("Other"), serviceId, 300m);
        using var customer = await _session.CustomerAsync();

        var first = await RequestScenario.CreateAsync(customer.Http, serviceId, regionId, cityId, ProviderScenario.Unique("One"));
        var second = await RequestScenario.CreateAsync(customer.Http, serviceId, regionId, cityId, ProviderScenario.Unique("Two"));
        var quoteOne = await QuoteScenario.SubmitAsync(provider.Http, first, 300m);
        var quoteTwo = await QuoteScenario.SubmitAsync(provider.Http, second, 310m);
        await QuoteScenario.SubmitAsync(other.Http, first, 280m);

        var (_, all) = await ApiJson.SendAsync(provider.Http, HttpMethod.Get, "/api/providers/me/quotes");
        var (_, forFirst) = await ApiJson.SendAsync(provider.Http, HttpMethod.Get, $"/api/providers/me/quotes?requestId={first}");
        var (_, forSecond) = await ApiJson.SendAsync(provider.Http, HttpMethod.Get, $"/api/providers/me/quotes?requestId={second}");
        var (_, nothing) = await ApiJson.SendAsync(provider.Http, HttpMethod.Get, $"/api/providers/me/quotes?requestId={Guid.NewGuid()}");
        var (malformed, _) = await ApiJson.SendAsync(provider.Http, HttpMethod.Get, "/api/providers/me/quotes?requestId=banana");

        Assert.Equal(2, all.GetProperty("totalCount").GetInt32());
        Assert.Equal(1, forFirst.GetProperty("totalCount").GetInt32());
        Assert.Equal(quoteOne, forFirst.GetProperty("items").EnumerateArray().Single().GetProperty("id").GetGuid());
        Assert.Equal(quoteTwo, forSecond.GetProperty("items").EnumerateArray().Single().GetProperty("id").GetGuid());
        Assert.Equal(0, nothing.GetProperty("totalCount").GetInt32());
        Assert.Equal(HttpStatusCode.BadRequest, malformed);
    }
}