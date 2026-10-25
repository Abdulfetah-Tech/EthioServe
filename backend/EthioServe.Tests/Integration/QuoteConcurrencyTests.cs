using System.Net;
using EthioServe.Domain.Enums;
using EthioServe.Infrastructure.Persistence;
using EthioServe.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace EthioServe.Tests.Integration;

/// <summary>
/// These tests fire real requests at the same moment. They prove the rules hold under a race, not just in sequence.
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class QuoteConcurrencyTests(ApiFactory factory)
{
    private readonly ApiSession _session = new(factory);
    private readonly ProviderScenario _scenario = new(factory, new ApiSession(factory));

    private static string Unique(string prefix) => ProviderScenario.Unique(prefix);

    [Fact]
    public async Task Providers_quoting_the_same_new_request_at_the_same_time_all_succeed()
    {
        using var admin = await _session.AdminAsync();
        using var anonymous = _session.CreateAnonymousClient();
        var (_, serviceId) = await _scenario.NewServiceAsync(admin.Http);
        var (regionId, cityId) = await ProviderScenario.LocationAsync(anonymous, "Addis Ababa", "Addis Ababa");
        var providers = new List<SignedInClient>();

        try
        {
            for (var i = 1; i <= 3; i++)
            {
                providers.Add(await _scenario.VerifiedProviderAsync(admin.Http, Unique($"Racer{i}"), serviceId, 100m * i));
            }

            using var customer = await _session.CustomerAsync();
            var requestId = await RequestScenario.CreateAsync(customer.Http, serviceId, regionId, cityId, Unique("Contested"));

            var results = await Task.WhenAll(
                providers.Select(p => QuoteScenario.SubmitRawAsync(p.Http, requestId, QuoteScenario.Body(200m))));

            var (_, quotes) = await ApiJson.SendAsync(customer.Http, HttpMethod.Get, $"/api/requests/{requestId}/quotes");

            Assert.All(results, r => Assert.Equal(HttpStatusCode.Created, r.Status));
            Assert.Equal(3, quotes.GetArrayLength());
            Assert.Equal("Quoted", await QuoteScenario.RequestStatusAsync(customer.Http, requestId));
        }
        finally
        {
            foreach (var provider in providers)
            {
                provider.Dispose();
            }
        }
    }

    [Fact]
    public async Task Two_quotes_accepted_at_the_same_time_never_both_win()
    {
        using var admin = await _session.AdminAsync();
        using var anonymous = _session.CreateAnonymousClient();
        var (_, serviceId) = await _scenario.NewServiceAsync(admin.Http);
        var (regionId, cityId) = await ProviderScenario.LocationAsync(anonymous, "Addis Ababa", "Addis Ababa");
        using var first = await _scenario.VerifiedProviderAsync(admin.Http, Unique("Left"), serviceId, 300m);
        using var second = await _scenario.VerifiedProviderAsync(admin.Http, Unique("Right"), serviceId, 350m);
        using var customer = await _session.CustomerAsync();

        for (var round = 1; round <= 5; round++)
        {
            var requestId = await RequestScenario.CreateAsync(customer.Http, serviceId, regionId, cityId, Unique($"Race{round}"));
            var left = await QuoteScenario.SubmitAsync(first.Http, requestId, 300m);
            var right = await QuoteScenario.SubmitAsync(second.Http, requestId, 350m);

            var results = await Task.WhenAll(
                ApiJson.SendAsync(customer.Http, HttpMethod.Post, $"/api/requests/{requestId}/quotes/{left}/accept"),
                ApiJson.SendAsync(customer.Http, HttpMethod.Post, $"/api/requests/{requestId}/quotes/{right}/accept"));

            // Exactly one wins. The other is told the request changed (409) or that it is already decided (422).
            Assert.Equal(1, results.Count(r => r.Status == HttpStatusCode.OK));
            Assert.All(
                results.Where(r => r.Status != HttpStatusCode.OK),
                r => Assert.True(
                    r.Status is HttpStatusCode.Conflict or HttpStatusCode.UnprocessableEntity,
                    $"Round {round}: the losing accept returned {(int)r.Status}."));

            await using var scope = factory.Services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var states = await db.Quotes.AsNoTracking()
                .Where(q => q.ServiceRequestId == requestId)
                .Select(q => q.Status)
                .ToListAsync();

            Assert.Equal(1, states.Count(s => s == QuoteStatus.Accepted));
            Assert.Equal(1, states.Count(s => s == QuoteStatus.Rejected));
            Assert.Equal("Accepted", await QuoteScenario.RequestStatusAsync(customer.Http, requestId));
        }
    }
}