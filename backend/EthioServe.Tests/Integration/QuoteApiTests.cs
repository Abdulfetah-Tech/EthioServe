using System.Net;
using System.Text.Json;
using EthioServe.Domain.Entities;
using EthioServe.Domain.Enums;
using EthioServe.Infrastructure.Persistence;
using EthioServe.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace EthioServe.Tests.Integration;

[Collection(ApiCollection.Name)]
public sealed class QuoteApiTests(ApiFactory factory)
{
    private const string UnknownId = "00000000-0000-0000-0000-000000000001";

    private readonly ApiSession _session = new(factory);
    private readonly ProviderScenario _scenario = new(factory, new ApiSession(factory));

    public static IEnumerable<object[]> ProviderRoutes()
    {
        yield return new object[] { "POST", $"/api/providers/me/requests/{UnknownId}/quotes" };
        yield return new object[] { "GET", "/api/providers/me/quotes" };
        yield return new object[] { "GET", $"/api/providers/me/quotes/{UnknownId}" };
        yield return new object[] { "POST", $"/api/providers/me/quotes/{UnknownId}/withdraw" };
    }

    public static IEnumerable<object[]> CustomerRoutes()
    {
        yield return new object[] { "GET", $"/api/requests/{UnknownId}/quotes" };
        yield return new object[] { "POST", $"/api/requests/{UnknownId}/quotes/{UnknownId}/accept" };
        yield return new object[] { "POST", $"/api/requests/{UnknownId}/quotes/{UnknownId}/decline" };
    }

    private static object? BodyFor(string method) => method == "POST" ? new { } : null;

    private static string Unique(string prefix) => ProviderScenario.Unique(prefix);

    private static bool HasField(JsonElement problem, string field) =>
        problem.GetProperty("errors").EnumerateObject().Any(p => string.Equals(p.Name, field, StringComparison.OrdinalIgnoreCase));

    private static string QuotesUrl(Guid requestId, string suffix = "") => $"/api/requests/{requestId}/quotes{suffix}";

    private static string OwnQuoteUrl(Guid quoteId, string suffix = "") => $"/api/providers/me/quotes/{quoteId}{suffix}";

    private static Task<(HttpStatusCode Status, JsonElement Body)> AcceptAsync(HttpClient customer, Guid requestId, Guid quoteId) =>
        ApiJson.SendAsync(customer, HttpMethod.Post, QuotesUrl(requestId, $"/{quoteId}/accept"));

    private static Task<(HttpStatusCode Status, JsonElement Body)> DeclineAsync(HttpClient customer, Guid requestId, Guid quoteId) =>
        ApiJson.SendAsync(customer, HttpMethod.Post, QuotesUrl(requestId, $"/{quoteId}/decline"));

    private static Task<(HttpStatusCode Status, JsonElement Body)> WithdrawAsync(HttpClient provider, Guid quoteId) =>
        ApiJson.SendAsync(provider, HttpMethod.Post, OwnQuoteUrl(quoteId, "/withdraw"));

    private static Task<(HttpStatusCode Status, JsonElement Body)> OwnQuoteAsync(HttpClient provider, Guid quoteId) =>
        ApiJson.SendAsync(provider, HttpMethod.Get, OwnQuoteUrl(quoteId));

    private static async Task<List<JsonElement>> CustomerQuotesAsync(HttpClient customer, Guid requestId)
    {
        var (status, body) = await ApiJson.SendAsync(customer, HttpMethod.Get, QuotesUrl(requestId));
        Assert.Equal(HttpStatusCode.OK, status);

        return body.EnumerateArray().ToList();
    }

    private sealed record Stage(
        SignedInClient Customer, SignedInClient First, SignedInClient Second,
        Guid RequestId, string Title, Guid ServiceId, int RegionId, int CityId) : IDisposable
    {
        public void Dispose()
        {
            Customer.Dispose();
            First.Dispose();
            Second.Dispose();
        }
    }

    // One open request for a brand-new service in Addis Ababa, and two verified providers who offer it.
    private async Task<Stage> NewStageAsync()
    {
        using var admin = await _session.AdminAsync();
        var (_, serviceId) = await _scenario.NewServiceAsync(admin.Http);
        using var anonymous = _session.CreateAnonymousClient();
        var (regionId, cityId) = await ProviderScenario.LocationAsync(anonymous, "Addis Ababa", "Addis Ababa");
        var first = await _scenario.VerifiedProviderAsync(admin.Http, Unique("Alpha"), serviceId, 300m);
        var second = await _scenario.VerifiedProviderAsync(admin.Http, Unique("Beta"), serviceId, 350m);
        var customer = await _session.CustomerAsync();
        var title = Unique("Quoted job");
        var requestId = await RequestScenario.CreateAsync(customer.Http, serviceId, regionId, cityId, title);

        return new Stage(customer, first, second, requestId, title, serviceId, regionId, cityId);
    }

    // ------------------------------------------------------------- access control

    [Theory]
    [MemberData(nameof(ProviderRoutes))]
    public async Task Only_providers_can_use_the_provider_quote_routes(string method, string url)
    {
        using var anonymous = _session.CreateAnonymousClient();
        using var customer = await _session.CustomerAsync();
        using var admin = await _session.AdminAsync();

        var (anonymousStatus, _) = await ApiJson.SendAsync(anonymous, new HttpMethod(method), url, BodyFor(method));
        Assert.Equal(HttpStatusCode.Unauthorized, anonymousStatus);

        foreach (var user in new[] { customer, admin })
        {
            var (status, _) = await ApiJson.SendAsync(user.Http, new HttpMethod(method), url, BodyFor(method));
            Assert.Equal(HttpStatusCode.Forbidden, status);
        }
    }

    [Theory]
    [MemberData(nameof(CustomerRoutes))]
    public async Task Only_customers_can_use_the_customer_quote_routes(string method, string url)
    {
        using var anonymous = _session.CreateAnonymousClient();
        using var provider = await _session.RegisteredProviderAsync();
        using var admin = await _session.AdminAsync();

        var (anonymousStatus, _) = await ApiJson.SendAsync(anonymous, new HttpMethod(method), url, BodyFor(method));
        Assert.Equal(HttpStatusCode.Unauthorized, anonymousStatus);

        foreach (var user in new[] { provider, admin })
        {
            var (status, _) = await ApiJson.SendAsync(user.Http, new HttpMethod(method), url, BodyFor(method));
            Assert.Equal(HttpStatusCode.Forbidden, status);
        }
    }

    // ------------------------------------------------------------- submitting

    [Fact]
    public async Task A_provider_can_quote_an_open_request_and_the_request_becomes_quoted()
    {
        using var stage = await NewStageAsync();

        var (status, quote) = await QuoteScenario.SubmitRawAsync(
            stage.First.Http, stage.RequestId, QuoteScenario.Body(450.5m, minutes: 90, days: 3));

        Assert.Equal(HttpStatusCode.Created, status);
        Assert.Equal("Pending", quote.GetProperty("status").GetString());
        Assert.Equal(450.5m, quote.GetProperty("amount").GetDecimal());
        Assert.Equal(90, quote.GetProperty("estimatedDurationMinutes").GetInt32());
        Assert.Equal(stage.RequestId, quote.GetProperty("requestId").GetGuid());
        Assert.Equal(JsonValueKind.Null, quote.GetProperty("contact").ValueKind);

        var expected = DateTimeOffset.UtcNow.AddDays(3);
        Assert.InRange(quote.GetProperty("validUntil").GetDateTimeOffset(), expected.AddMinutes(-5), expected.AddMinutes(5));
        Assert.Equal("Quoted", await QuoteScenario.RequestStatusAsync(stage.Customer.Http, stage.RequestId));
    }

    [Fact]
    public async Task Invalid_quotes_return_400_with_field_errors_and_change_nothing()
    {
        using var stage = await NewStageAsync();
        var http = stage.First.Http;

        var (zero, zeroBody) = await QuoteScenario.SubmitRawAsync(http, stage.RequestId, QuoteScenario.Body(amount: 0m));
        var (cents, centsBody) = await QuoteScenario.SubmitRawAsync(http, stage.RequestId, QuoteScenario.Body(amount: 10.555m));
        var (brief, briefBody) = await QuoteScenario.SubmitRawAsync(http, stage.RequestId, QuoteScenario.Body(description: "short"));
        var (rushed, rushedBody) = await QuoteScenario.SubmitRawAsync(http, stage.RequestId, QuoteScenario.Body(minutes: 5));
        var (forever, foreverBody) = await QuoteScenario.SubmitRawAsync(http, stage.RequestId, QuoteScenario.Body(days: 90));
        var (_, list) = await ApiJson.SendAsync(http, HttpMethod.Get, "/api/providers/me/quotes");

        Assert.Equal(HttpStatusCode.BadRequest, zero);
        Assert.True(HasField(zeroBody, "amount"));
        Assert.Equal(HttpStatusCode.BadRequest, cents);
        Assert.True(HasField(centsBody, "amount"));
        Assert.Equal(HttpStatusCode.BadRequest, brief);
        Assert.True(HasField(briefBody, "description"));
        Assert.Equal(HttpStatusCode.BadRequest, rushed);
        Assert.True(HasField(rushedBody, "estimatedDurationMinutes"));
        Assert.Equal(HttpStatusCode.BadRequest, forever);
        Assert.True(HasField(foreverBody, "validForDays"));
        Assert.Equal(0, list.GetProperty("totalCount").GetInt32());
        Assert.Equal("Pending", await QuoteScenario.RequestStatusAsync(stage.Customer.Http, stage.RequestId));
    }

    [Fact]
    public async Task A_provider_has_one_open_quote_per_request_until_they_withdraw_it()
    {
        using var stage = await NewStageAsync();
        var first = await QuoteScenario.SubmitAsync(stage.First.Http, stage.RequestId, 300m);

        var (duplicate, _) = await QuoteScenario.SubmitRawAsync(stage.First.Http, stage.RequestId, QuoteScenario.Body(250m));
        var (withdrawn, withdrawnBody) = await WithdrawAsync(stage.First.Http, first);
        var (again, _) = await QuoteScenario.SubmitRawAsync(stage.First.Http, stage.RequestId, QuoteScenario.Body(250m));

        Assert.Equal(HttpStatusCode.Conflict, duplicate);
        Assert.Equal(HttpStatusCode.OK, withdrawn);
        Assert.Equal("Cancelled", withdrawnBody.GetProperty("status").GetString());
        Assert.Equal(HttpStatusCode.Created, again);
    }

    [Fact]
    public async Task Providers_who_cannot_see_a_request_cannot_quote_on_it()
    {
        using var admin = await _session.AdminAsync();
        using var anonymous = _session.CreateAnonymousClient();
        var (categoryId, serviceA) = await _scenario.NewServiceAsync(admin.Http);
        var serviceB = await _scenario.AddServiceAsync(admin.Http, categoryId);
        var (regionId, cityId) = await ProviderScenario.LocationAsync(anonymous, "Addis Ababa", "Addis Ababa");
        using var otherService = await _scenario.VerifiedProviderAsync(admin.Http, Unique("Wrong"), serviceB, 100m);
        using var otherCity = await _scenario.VerifiedProviderAsync(admin.Http, Unique("Far"), serviceA, 100m, region: "Oromia", city: "Adama");
        using var unverified = await _session.RegisteredProviderAsync();
        using var eligible = await _scenario.VerifiedProviderAsync(admin.Http, Unique("Eligible"), serviceA, 100m);
        using var customer = await _session.CustomerAsync();
        var requestId = await RequestScenario.CreateAsync(customer.Http, serviceA, regionId, cityId, Unique("Scoped"));

        var (wrongService, _) = await QuoteScenario.SubmitRawAsync(otherService.Http, requestId, QuoteScenario.Body());
        var (wrongCity, _) = await QuoteScenario.SubmitRawAsync(otherCity.Http, requestId, QuoteScenario.Body());
        var (notVerified, _) = await QuoteScenario.SubmitRawAsync(unverified.Http, requestId, QuoteScenario.Body());

        await ApiJson.SendAsync(customer.Http, HttpMethod.Post, $"/api/requests/{requestId}/cancel", new { });
        var (closed, _) = await QuoteScenario.SubmitRawAsync(eligible.Http, requestId, QuoteScenario.Body());

        Assert.Equal(HttpStatusCode.NotFound, wrongService);
        Assert.Equal(HttpStatusCode.NotFound, wrongCity);
        Assert.Equal(HttpStatusCode.Forbidden, notVerified);
        Assert.Equal(HttpStatusCode.NotFound, closed);
    }

    [Fact]
    public async Task A_quote_past_its_deadline_reads_expired_and_does_not_block_a_new_one()
    {
        using var stage = await NewStageAsync();
        var oldId = await QuoteScenario.SubmitAsync(stage.First.Http, stage.RequestId, 300m);
        await QuoteScenario.ExpireAsync(factory, oldId);

        var newId = await QuoteScenario.SubmitAsync(stage.First.Http, stage.RequestId, 320m);
        var (_, page) = await ApiJson.SendAsync(stage.First.Http, HttpMethod.Get, "/api/providers/me/quotes");
        var items = page.GetProperty("items").EnumerateArray().ToList();

        Assert.Equal(2, items.Count);
        Assert.Equal(newId, items[0].GetProperty("id").GetGuid());
        Assert.Equal("Pending", items[0].GetProperty("status").GetString());
        Assert.Equal(oldId, items[1].GetProperty("id").GetGuid());
        Assert.Equal("Expired", items[1].GetProperty("status").GetString());

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var stored = await db.Quotes.AsNoTracking().Where(q => q.Id == oldId).Select(q => q.Status).SingleAsync();

        Assert.Equal(QuoteStatus.Expired, stored);
    }

    // ------------------------------------------------------------- a provider's own quotes

    [Fact]
    public async Task Providers_see_only_their_own_quotes()
    {
        using var stage = await NewStageAsync();
        var mine = await QuoteScenario.SubmitAsync(stage.First.Http, stage.RequestId, 300m);
        var theirs = await QuoteScenario.SubmitAsync(stage.Second.Http, stage.RequestId, 350m);

        var (_, page) = await ApiJson.SendAsync(stage.First.Http, HttpMethod.Get, "/api/providers/me/quotes");
        var (peek, _) = await OwnQuoteAsync(stage.First.Http, theirs);
        var (withdraw, _) = await WithdrawAsync(stage.First.Http, theirs);
        var (unknown, _) = await OwnQuoteAsync(stage.First.Http, Guid.Parse(UnknownId));
        var (own, ownBody) = await OwnQuoteAsync(stage.First.Http, mine);

        Assert.Equal(1, page.GetProperty("totalCount").GetInt32());
        Assert.Equal(mine, page.GetProperty("items").EnumerateArray().Single().GetProperty("id").GetGuid());
        Assert.Equal(HttpStatusCode.NotFound, peek);
        Assert.Equal(HttpStatusCode.NotFound, withdraw);
        Assert.Equal(HttpStatusCode.NotFound, unknown);
        Assert.Equal(HttpStatusCode.OK, own);
        Assert.Equal(stage.Title, ownBody.GetProperty("requestTitle").GetString());
        Assert.Equal("Addis Ababa", ownBody.GetProperty("cityName").GetString());
    }

    // ------------------------------------------------------------- the customer's view

    [Fact]
    public async Task The_customer_compares_quotes_cheapest_first_and_sees_no_phone_numbers_yet()
    {
        using var stage = await NewStageAsync();
        await QuoteScenario.SubmitAsync(stage.Second.Http, stage.RequestId, 450m);
        await QuoteScenario.SubmitAsync(stage.First.Http, stage.RequestId, 300m);

        var quotes = await CustomerQuotesAsync(stage.Customer.Http, stage.RequestId);

        Assert.Equal(new[] { 300m, 450m }, quotes.Select(q => q.GetProperty("amount").GetDecimal()).ToList());
        Assert.Equal(stage.First.ProfileId, quotes[0].GetProperty("providerId").GetGuid());
        Assert.All(quotes, q =>
        {
            Assert.Equal("Pending", q.GetProperty("status").GetString());
            Assert.Equal(JsonValueKind.Null, q.GetProperty("providerPhone").ValueKind);
            Assert.False(string.IsNullOrEmpty(q.GetProperty("providerName").GetString()));
        });
    }

    [Fact]
    public async Task Withdrawn_quotes_are_hidden_and_the_request_waits_for_quotes_again_when_the_last_one_goes()
    {
        using var stage = await NewStageAsync();
        var first = await QuoteScenario.SubmitAsync(stage.First.Http, stage.RequestId, 300m);
        var second = await QuoteScenario.SubmitAsync(stage.Second.Http, stage.RequestId, 350m);

        await WithdrawAsync(stage.First.Http, first);
        var afterFirst = await CustomerQuotesAsync(stage.Customer.Http, stage.RequestId);
        var statusAfterFirst = await QuoteScenario.RequestStatusAsync(stage.Customer.Http, stage.RequestId);

        await WithdrawAsync(stage.Second.Http, second);
        var afterSecond = await CustomerQuotesAsync(stage.Customer.Http, stage.RequestId);
        var statusAfterSecond = await QuoteScenario.RequestStatusAsync(stage.Customer.Http, stage.RequestId);
        var (again, _) = await WithdrawAsync(stage.Second.Http, second);

        Assert.Equal(second, afterFirst.Single().GetProperty("id").GetGuid());
        Assert.Equal("Quoted", statusAfterFirst);
        Assert.Empty(afterSecond);
        Assert.Equal("Pending", statusAfterSecond);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, again);
    }

    // ------------------------------------------------------------- accepting

    [Fact]
    public async Task Accepting_a_quote_rejects_the_others_closes_the_request_and_reveals_the_contact_details()
    {
        using var stage = await NewStageAsync();
        var winning = await QuoteScenario.SubmitAsync(stage.First.Http, stage.RequestId, 300m);
        var losing = await QuoteScenario.SubmitAsync(stage.Second.Http, stage.RequestId, 350m);

        var (_, before) = await OwnQuoteAsync(stage.First.Http, winning);
        var (status, accepted) = await AcceptAsync(stage.Customer.Http, stage.RequestId, winning);
        var quotes = await CustomerQuotesAsync(stage.Customer.Http, stage.RequestId);
        var (_, winnerView) = await OwnQuoteAsync(stage.First.Http, winning);
        var (_, loserView) = await OwnQuoteAsync(stage.Second.Http, losing);

        Assert.Equal(JsonValueKind.Null, before.GetProperty("contact").ValueKind);

        Assert.Equal(HttpStatusCode.OK, status);
        Assert.Equal("Accepted", accepted.GetProperty("status").GetString());
        Assert.Equal("0911556677", accepted.GetProperty("providerPhone").GetString());
        Assert.Equal("Accepted", await QuoteScenario.RequestStatusAsync(stage.Customer.Http, stage.RequestId));

        var winner = quotes.Single(q => q.GetProperty("id").GetGuid() == winning);
        var loser = quotes.Single(q => q.GetProperty("id").GetGuid() == losing);
        Assert.Equal("Accepted", winner.GetProperty("status").GetString());
        Assert.Equal("Rejected", loser.GetProperty("status").GetString());
        Assert.Equal(JsonValueKind.Null, loser.GetProperty("providerPhone").ValueKind);

        var contact = winnerView.GetProperty("contact");
        Assert.Equal("Accepted", winnerView.GetProperty("status").GetString());
        Assert.Equal("Test User", contact.GetProperty("name").GetString());
        Assert.Equal("0911223344", contact.GetProperty("phoneNumber").GetString());
        Assert.Equal("Bole, near the main road", contact.GetProperty("address").GetString());

        Assert.Equal("Rejected", loserView.GetProperty("status").GetString());
        Assert.Equal(JsonValueKind.Null, loserView.GetProperty("contact").ValueKind);
    }

    [Fact]
    public async Task Accepting_twice_or_accepting_a_second_quote_is_refused()
    {
        using var stage = await NewStageAsync();
        var first = await QuoteScenario.SubmitAsync(stage.First.Http, stage.RequestId, 300m);
        var second = await QuoteScenario.SubmitAsync(stage.Second.Http, stage.RequestId, 350m);

        var (ok, _) = await AcceptAsync(stage.Customer.Http, stage.RequestId, first);
        var (twice, _) = await AcceptAsync(stage.Customer.Http, stage.RequestId, first);
        var (other, _) = await AcceptAsync(stage.Customer.Http, stage.RequestId, second);
        var quotes = await CustomerQuotesAsync(stage.Customer.Http, stage.RequestId);

        Assert.Equal(HttpStatusCode.OK, ok);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, twice);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, other);
        Assert.Equal(1, quotes.Count(q => q.GetProperty("status").GetString() == "Accepted"));
    }

    [Fact]
    public async Task A_quote_past_its_deadline_cannot_be_accepted()
    {
        using var stage = await NewStageAsync();
        var quoteId = await QuoteScenario.SubmitAsync(stage.First.Http, stage.RequestId, 300m);
        await QuoteScenario.ExpireAsync(factory, quoteId);

        var (status, _) = await AcceptAsync(stage.Customer.Http, stage.RequestId, quoteId);
        var quotes = await CustomerQuotesAsync(stage.Customer.Http, stage.RequestId);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, status);
        Assert.Equal("Expired", quotes.Single().GetProperty("status").GetString());
        Assert.Equal("Quoted", await QuoteScenario.RequestStatusAsync(stage.Customer.Http, stage.RequestId));
    }

    [Fact]
    public async Task After_acceptance_the_request_is_closed_to_other_providers_edits_and_cancelling()
    {
        using var stage = await NewStageAsync();
        var first = await QuoteScenario.SubmitAsync(stage.First.Http, stage.RequestId, 300m);
        await QuoteScenario.SubmitAsync(stage.Second.Http, stage.RequestId, 350m);
        await AcceptAsync(stage.Customer.Http, stage.RequestId, first);

        var (anotherQuote, _) = await QuoteScenario.SubmitRawAsync(stage.Second.Http, stage.RequestId, QuoteScenario.Body(200m));
        var (edit, _) = await ApiJson.SendAsync(stage.Customer.Http, HttpMethod.Put, $"/api/requests/{stage.RequestId}",
            RequestScenario.Body(stage.ServiceId, stage.RegionId, stage.CityId, title: "Too late"));
        var (cancel, _) = await ApiJson.SendAsync(stage.Customer.Http, HttpMethod.Post, $"/api/requests/{stage.RequestId}/cancel", new { });
        var (withdraw, _) = await WithdrawAsync(stage.First.Http, first);
        var (_, feed) = await ApiJson.SendAsync(stage.First.Http, HttpMethod.Get, "/api/providers/me/requests");
        var titles = feed.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("title").GetString()).ToList();

        Assert.Equal(HttpStatusCode.NotFound, anotherQuote);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, edit);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, cancel);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, withdraw);
        Assert.DoesNotContain(stage.Title, titles);
    }

    // ------------------------------------------------------------- declining

    [Fact]
    public async Task Declining_a_quote_rejects_it_and_reopens_the_request_when_it_was_the_last()
    {
        using var stage = await NewStageAsync();
        var first = await QuoteScenario.SubmitAsync(stage.First.Http, stage.RequestId, 300m);
        var second = await QuoteScenario.SubmitAsync(stage.Second.Http, stage.RequestId, 350m);

        var (declined, declinedBody) = await DeclineAsync(stage.Customer.Http, stage.RequestId, first);
        var statusAfterFirst = await QuoteScenario.RequestStatusAsync(stage.Customer.Http, stage.RequestId);
        var (_, providerView) = await OwnQuoteAsync(stage.First.Http, first);
        var (again, _) = await DeclineAsync(stage.Customer.Http, stage.RequestId, first);

        await DeclineAsync(stage.Customer.Http, stage.RequestId, second);
        var statusAfterSecond = await QuoteScenario.RequestStatusAsync(stage.Customer.Http, stage.RequestId);

        Assert.Equal(HttpStatusCode.OK, declined);
        Assert.Equal("Rejected", declinedBody.GetProperty("status").GetString());
        Assert.Equal("Quoted", statusAfterFirst);
        Assert.Equal("Rejected", providerView.GetProperty("status").GetString());
        Assert.Equal(HttpStatusCode.UnprocessableEntity, again);
        Assert.Equal("Pending", statusAfterSecond);
    }

    // ------------------------------------------------------------- ownership

    [Fact]
    public async Task Other_customers_cannot_see_accept_or_decline_quotes()
    {
        using var stage = await NewStageAsync();
        using var intruder = await _session.CustomerAsync();
        var quoteId = await QuoteScenario.SubmitAsync(stage.First.Http, stage.RequestId, 300m);

        var (list, _) = await ApiJson.SendAsync(intruder.Http, HttpMethod.Get, QuotesUrl(stage.RequestId));
        var (accept, _) = await AcceptAsync(intruder.Http, stage.RequestId, quoteId);
        var (decline, _) = await DeclineAsync(intruder.Http, stage.RequestId, quoteId);

        Assert.Equal(HttpStatusCode.NotFound, list);
        Assert.Equal(HttpStatusCode.NotFound, accept);
        Assert.Equal(HttpStatusCode.NotFound, decline);
        Assert.Equal("Quoted", await QuoteScenario.RequestStatusAsync(stage.Customer.Http, stage.RequestId));
    }

    [Fact]
    public async Task A_quote_cannot_be_accepted_through_a_different_request()
    {
        using var stage = await NewStageAsync();
        var otherRequest = await RequestScenario.CreateAsync(
            stage.Customer.Http, stage.ServiceId, stage.RegionId, stage.CityId, Unique("Another"));
        var quoteId = await QuoteScenario.SubmitAsync(stage.First.Http, stage.RequestId, 300m);

        var (accept, _) = await AcceptAsync(stage.Customer.Http, otherRequest, quoteId);
        var (decline, _) = await DeclineAsync(stage.Customer.Http, otherRequest, quoteId);
        var (unknown, _) = await AcceptAsync(stage.Customer.Http, stage.RequestId, Guid.Parse(UnknownId));

        Assert.Equal(HttpStatusCode.NotFound, accept);
        Assert.Equal(HttpStatusCode.NotFound, decline);
        Assert.Equal(HttpStatusCode.NotFound, unknown);
    }

    // ------------------------------------------------------------- audit

    [Fact]
    public async Task Quote_actions_are_audited_without_any_contact_details()
    {
        using var stage = await NewStageAsync();
        var winning = await QuoteScenario.SubmitAsync(stage.First.Http, stage.RequestId, 300m);
        var losing = await QuoteScenario.SubmitAsync(stage.Second.Http, stage.RequestId, 350m);
        await AcceptAsync(stage.Customer.Http, stage.RequestId, winning);

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        async Task<List<AuditLog>> EntriesAsync(Guid quoteId) =>
            await db.AuditLogs.AsNoTracking()
                .Where(a => a.EntityName == nameof(Quote) && a.EntityId == quoteId.ToString())
                .OrderBy(a => a.Id)
                .ToListAsync();

        var winningEntries = await EntriesAsync(winning);
        var losingEntries = await EntriesAsync(losing);

        Assert.Equal(new[] { "QuoteSubmitted", "QuoteAccepted" }, winningEntries.Select(e => e.Action).ToArray());
        Assert.Equal(new[] { "QuoteSubmitted", "QuoteRejected" }, losingEntries.Select(e => e.Action).ToArray());
        Assert.Equal(stage.First.UserId, winningEntries[0].UserId);
        Assert.Equal(stage.Customer.UserId, winningEntries[1].UserId);
        Assert.Equal(stage.Second.UserId, losingEntries[0].UserId);
        Assert.Equal(stage.Customer.UserId, losingEntries[1].UserId);
        Assert.Contains("Pending", winningEntries[1].OldValues);
        Assert.Contains("Accepted", winningEntries[1].NewValues);

        Assert.All(winningEntries.Concat(losingEntries), e =>
        {
            var text = (e.OldValues ?? string.Empty) + (e.NewValues ?? string.Empty);

            Assert.DoesNotContain("0911", text);
            Assert.DoesNotContain("Bole", text);
        });
    }
}