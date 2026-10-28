using System.Net;
using EthioServe.Tests.Support;

namespace EthioServe.Tests.Integration;

[Collection(ApiCollection.Name)]
public sealed class NotificationApiTests(ApiFactory factory)
{
    private const string UnknownId = "00000000-0000-0000-0000-000000000001";

    private readonly ApiSession _session = new(factory);
    private readonly ProviderScenario _scenario = new(factory, new ApiSession(factory));
    private readonly JobBuilder _jobs = new(factory);

    public static IEnumerable<object[]> Routes()
    {
        yield return new object[] { "GET", "/api/notifications" };
        yield return new object[] { "GET", "/api/notifications/unread-count" };
        yield return new object[] { "POST", $"/api/notifications/{UnknownId}/read" };
        yield return new object[] { "POST", "/api/notifications/read-all" };
    }

    private static string Unique(string prefix) => ProviderScenario.Unique(prefix);

    // ------------------------------------------------------------- access

    [Theory]
    [MemberData(nameof(Routes))]
    public async Task Every_notification_route_needs_a_signed_in_user(string method, string url)
    {
        using var anonymous = _session.CreateAnonymousClient();

        var (status, _) = await ApiJson.SendAsync(anonymous, new HttpMethod(method), url, method == "POST" ? new { } : null);

        Assert.Equal(HttpStatusCode.Unauthorized, status);
    }

    [Fact]
    public async Task A_new_user_has_no_notifications()
    {
        using var customer = await _session.CustomerAsync();

        var (status, body) = await ApiJson.SendAsync(customer.Http, HttpMethod.Get, "/api/notifications");

        Assert.Equal(HttpStatusCode.OK, status);
        Assert.Equal(0, body.GetProperty("totalCount").GetInt32());
        Assert.Equal(0, await NotificationScenario.UnreadAsync(customer.Http));
    }

    // ------------------------------------------------------------- new requests

    [Fact]
    public async Task A_new_request_alerts_the_providers_who_could_take_it_and_nobody_else()
    {
        using var admin = await _session.AdminAsync();
        using var anonymous = _session.CreateAnonymousClient();
        var (categoryId, serviceA) = await _scenario.NewServiceAsync(admin.Http);
        var serviceB = await _scenario.AddServiceAsync(admin.Http, categoryId);
        var (regionId, cityId) = await ProviderScenario.LocationAsync(anonymous, "Addis Ababa", "Addis Ababa");
        using var matching = await _scenario.VerifiedProviderAsync(admin.Http, Unique("Match"), serviceA, 300m);
        using var otherService = await _scenario.VerifiedProviderAsync(admin.Http, Unique("Wrong"), serviceB, 100m);
        using var otherCity = await _scenario.VerifiedProviderAsync(admin.Http, Unique("Far"), serviceA, 100m, region: "Oromia", city: "Adama");
        using var unverified = await _session.RegisteredProviderAsync();
        using var customer = await _session.CustomerAsync();

        var title = Unique("Alert me");
        var requestId = await RequestScenario.CreateAsync(customer.Http, serviceA, regionId, cityId, title);

        var alerts = NotificationScenario.OfType(await NotificationScenario.ListAsync(matching.Http), "NewServiceRequest");
        var alert = Assert.Single(alerts);

        Assert.Equal("New request in your city", alert.GetProperty("title").GetString());
        Assert.Contains(title, alert.GetProperty("message").GetString());
        Assert.Equal("ServiceRequest", alert.GetProperty("relatedEntityType").GetString());
        Assert.Equal(requestId, alert.GetProperty("relatedEntityId").GetGuid());
        Assert.False(alert.GetProperty("isRead").GetBoolean());

        foreach (var other in new[] { otherService, otherCity, unverified, customer })
        {
            Assert.Empty(NotificationScenario.OfType(await NotificationScenario.ListAsync(other.Http), "NewServiceRequest"));
        }
    }

    // ------------------------------------------------------------- quotes

    [Fact]
    public async Task Quotes_alert_the_customer_and_choosing_one_alerts_the_providers()
    {
        using var world = await _jobs.NewWorldAsync();
        using var other = await _jobs.ExtraProviderAsync(world);
        var requestId = await RequestScenario.CreateAsync(
            world.Customer.Http, world.ServiceId, world.RegionId, world.CityId, Unique("Quoted"));
        var first = await QuoteScenario.SubmitAsync(world.Provider.Http, requestId, 300m);
        await QuoteScenario.SubmitAsync(other.Http, requestId, 350m);

        var received = NotificationScenario.OfType(await NotificationScenario.ListAsync(world.Customer.Http), "QuoteReceived");
        var messages = received.Select(n => n.GetProperty("message").GetString()!).ToList();

        Assert.Equal(2, received.Count);
        Assert.Contains(messages, m => m.Contains("300 ETB"));
        Assert.Contains(messages, m => m.Contains("350 ETB"));
        Assert.All(received, n =>
        {
            Assert.Equal("New quote received", n.GetProperty("title").GetString());
            Assert.Equal("ServiceRequest", n.GetProperty("relatedEntityType").GetString());
            Assert.Equal(requestId, n.GetProperty("relatedEntityId").GetGuid());
        });

        await ApiJson.SendAsync(world.Customer.Http, HttpMethod.Post, $"/api/requests/{requestId}/quotes/{first}/accept");

        var winner = await NotificationScenario.ListAsync(world.Provider.Http);
        var accepted = Assert.Single(NotificationScenario.OfType(winner, "QuoteAccepted"));
        var loser = await NotificationScenario.ListAsync(other.Http);

        Assert.Equal("Your quote was accepted", accepted.GetProperty("title").GetString());
        Assert.Equal("Quote", accepted.GetProperty("relatedEntityType").GetString());
        Assert.Equal(first, accepted.GetProperty("relatedEntityId").GetGuid());
        Assert.Contains("Quote not chosen", NotificationScenario.Titles(loser));
        Assert.Empty(NotificationScenario.OfType(loser, "QuoteAccepted"));
    }

    [Fact]
    public async Task Declining_a_quote_tells_the_provider()
    {
        using var world = await _jobs.NewWorldAsync();
        var requestId = await RequestScenario.CreateAsync(
            world.Customer.Http, world.ServiceId, world.RegionId, world.CityId, Unique("Declined"));
        var quoteId = await QuoteScenario.SubmitAsync(world.Provider.Http, requestId, 300m);

        await ApiJson.SendAsync(world.Customer.Http, HttpMethod.Post, $"/api/requests/{requestId}/quotes/{quoteId}/decline");

        var notes = await NotificationScenario.ListAsync(world.Provider.Http);
        var declined = notes.Single(n => n.GetProperty("title").GetString() == "Quote declined");

        Assert.Equal("General", declined.GetProperty("type").GetString());
        Assert.Equal(quoteId, declined.GetProperty("relatedEntityId").GetGuid());
    }

    [Fact]
    public async Task A_quote_that_is_refused_leaves_no_notification_behind()
    {
        using var world = await _jobs.NewWorldAsync();
        var requestId = await RequestScenario.CreateAsync(
            world.Customer.Http, world.ServiceId, world.RegionId, world.CityId, Unique("Nothing"));

        var (status, _) = await QuoteScenario.SubmitRawAsync(world.Provider.Http, requestId, QuoteScenario.Body(amount: 0m));

        Assert.Equal(HttpStatusCode.BadRequest, status);
        Assert.Empty(NotificationScenario.OfType(await NotificationScenario.ListAsync(world.Customer.Http), "QuoteReceived"));
    }

    // ------------------------------------------------------------- appointments

    [Fact]
    public async Task Each_appointment_step_tells_the_other_side_and_never_shares_contact_details()
    {
        using var world = await _jobs.NewWorldAsync();
        var requestId = await _jobs.AcceptedRequestAsync(world);
        var http = world.Provider.Http;

        async Task<List<string>> CustomerTitles() => NotificationScenario.Titles(await NotificationScenario.ListAsync(world.Customer.Http));
        async Task<List<string>> ProviderTitles() => NotificationScenario.Titles(await NotificationScenario.ListAsync(http));

        var id = await AppointmentScenario.ScheduleAsync(http, requestId, AppointmentScenario.InHours(30));
        Assert.Contains("A time was proposed", await CustomerTitles());

        await AppointmentScenario.CustomerActionAsync(world.Customer.Http, id, "confirm");
        var confirmed = (await NotificationScenario.ListAsync(http)).Single(n => n.GetProperty("title").GetString() == "The customer confirmed");
        Assert.Equal("AppointmentUpdated", confirmed.GetProperty("type").GetString());
        Assert.Equal("Appointment", confirmed.GetProperty("relatedEntityType").GetString());
        Assert.Equal(id, confirmed.GetProperty("relatedEntityId").GetGuid());

        await AppointmentScenario.ProviderActionAsync(http, id, "reschedule", AppointmentScenario.Body(AppointmentScenario.InHours(40)));
        Assert.Contains("The time was changed", await CustomerTitles());

        await AppointmentScenario.CustomerActionAsync(world.Customer.Http, id, "cancel");
        Assert.Single(await ProviderTitles(), t => t == "Appointment cancelled");

        var second = await AppointmentScenario.ScheduleAsync(http, requestId, AppointmentScenario.InHours(50));
        await AppointmentScenario.ProviderActionAsync(http, second, "cancel");
        Assert.Single(await CustomerTitles(), t => t == "Appointment cancelled");

        var customerNote = (await NotificationScenario.ListAsync(world.Customer.Http)).First(n => n.GetProperty("type").GetString() == "AppointmentUpdated");
        Assert.Equal("ServiceRequest", customerNote.GetProperty("relatedEntityType").GetString());
        Assert.Equal(requestId, customerNote.GetProperty("relatedEntityId").GetGuid());

        var everything = await NotificationScenario.AllTextAsync(world.Customer.Http) + await NotificationScenario.AllTextAsync(http);
        Assert.DoesNotContain("0911", everything);
        Assert.DoesNotContain("Bole", everything);
    }

    [Fact]
    public async Task Starting_and_finishing_the_job_tells_the_customer()
    {
        using var world = await _jobs.NewWorldAsync();
        var requestId = await _jobs.AcceptedRequestAsync(world);
        var http = world.Provider.Http;
        var id = await AppointmentScenario.ScheduleAsync(http, requestId, AppointmentScenario.InHours(1.5));
        await AppointmentScenario.CustomerActionAsync(world.Customer.Http, id, "confirm");

        await AppointmentScenario.ProviderActionAsync(http, id, "start");
        await AppointmentScenario.ProviderActionAsync(http, id, "complete");

        var titles = NotificationScenario.Titles(await NotificationScenario.ListAsync(world.Customer.Http));

        Assert.Contains("The job has started", titles);
        Assert.Contains("The job is complete", titles);
    }

    // ------------------------------------------------------------- admin decisions

    [Fact]
    public async Task Admin_decisions_tell_the_provider_and_include_the_reason()
    {
        using var admin = await _session.AdminAsync();
        var (_, serviceId) = await _scenario.NewServiceAsync(admin.Http);

        using var approved = await _scenario.PendingProviderAsync(Unique("Appr"), serviceId, 100m);
        await ProviderScenario.ApproveAsync(admin.Http, approved);
        var verified = Assert.Single(NotificationScenario.OfType(await NotificationScenario.ListAsync(approved.Http), "ProviderVerified"));
        Assert.Equal("You are verified", verified.GetProperty("title").GetString());
        Assert.Equal("ProviderProfile", verified.GetProperty("relatedEntityType").GetString());
        Assert.Equal(approved.ProfileId, verified.GetProperty("relatedEntityId").GetGuid());

        using var rejected = await _scenario.PendingProviderAsync(Unique("Rej"), serviceId, 100m);
        await ProviderScenario.RejectAsync(admin.Http, rejected, "Please add more detail");
        var refusal = Assert.Single(NotificationScenario.OfType(await NotificationScenario.ListAsync(rejected.Http), "General"));
        Assert.Equal("Verification not approved", refusal.GetProperty("title").GetString());
        Assert.Contains("Please add more detail", refusal.GetProperty("message").GetString());

        using var suspended = await _scenario.VerifiedProviderAsync(admin.Http, Unique("Susp"), serviceId, 100m);
        await ProviderScenario.SuspendAsync(admin.Http, suspended, "Customer complaints");
        var suspension = (await NotificationScenario.ListAsync(suspended.Http)).Single(n => n.GetProperty("title").GetString() == "Your profile was suspended");
        Assert.Contains("Customer complaints", suspension.GetProperty("message").GetString());

        await ApiJson.SendAsync(admin.Http, HttpMethod.Post, $"/api/admin/providers/{suspended.ProfileId}/reinstate");
        Assert.Contains("You are verified again", NotificationScenario.Titles(await NotificationScenario.ListAsync(suspended.Http)));
    }

    // ------------------------------------------------------------- reading them

    [Fact]
    public async Task Notifications_are_listed_newest_first_with_paging_and_an_unread_filter()
    {
        using var world = await _jobs.NewWorldAsync();

        for (var i = 1; i <= 3; i++)
        {
            await RequestScenario.CreateAsync(world.Customer.Http, world.ServiceId, world.RegionId, world.CityId, Unique($"Many{i}"));
        }

        // One "you are verified" plus three new-request alerts.
        var http = world.Provider.Http;
        var all = await NotificationScenario.ListAsync(http);
        var times = all.Select(n => n.GetProperty("createdAt").GetDateTimeOffset()).ToList();
        var (_, second) = await ApiJson.SendAsync(http, HttpMethod.Get, "/api/notifications?pageSize=2&page=2");
        var (_, clamped) = await ApiJson.SendAsync(http, HttpMethod.Get, "/api/notifications?pageSize=1000");
        var (badFlag, _) = await ApiJson.SendAsync(http, HttpMethod.Get, "/api/notifications?unreadOnly=maybe");

        Assert.Equal(4, all.Count);
        Assert.Equal(times.OrderByDescending(t => t).ToList(), times);
        Assert.Equal(4, second.GetProperty("totalCount").GetInt32());
        Assert.Equal(2, second.GetProperty("totalPages").GetInt32());
        Assert.Equal(2, second.GetProperty("items").GetArrayLength());
        Assert.Equal(50, clamped.GetProperty("pageSize").GetInt32());
        Assert.Equal(HttpStatusCode.BadRequest, badFlag);
    }

    [Fact]
    public async Task Marking_read_updates_the_count_is_repeatable_and_belongs_to_the_owner()
    {
        using var world = await _jobs.NewWorldAsync();

        for (var i = 1; i <= 3; i++)
        {
            await RequestScenario.CreateAsync(world.Customer.Http, world.ServiceId, world.RegionId, world.CityId, Unique($"Read{i}"));
        }

        var http = world.Provider.Http;
        var all = await NotificationScenario.ListAsync(http);
        var first = all[0].GetProperty("id").GetGuid();
        var second = all[1].GetProperty("id").GetGuid();

        Assert.Equal(4, await NotificationScenario.UnreadAsync(http));

        var (marked, _) = await ApiJson.SendAsync(http, HttpMethod.Post, $"/api/notifications/{first}/read");
        var (again, _) = await ApiJson.SendAsync(http, HttpMethod.Post, $"/api/notifications/{first}/read");

        Assert.Equal(HttpStatusCode.NoContent, marked);
        Assert.Equal(HttpStatusCode.NoContent, again);
        Assert.Equal(3, await NotificationScenario.UnreadAsync(http));
        Assert.Equal(3, (await NotificationScenario.ListAsync(http, "?unreadOnly=true")).Count);
        Assert.Equal(4, (await NotificationScenario.ListAsync(http)).Count);

        var (stranger, _) = await ApiJson.SendAsync(world.Customer.Http, HttpMethod.Post, $"/api/notifications/{second}/read");
        var (unknown, _) = await ApiJson.SendAsync(http, HttpMethod.Post, $"/api/notifications/{UnknownId}/read");

        Assert.Equal(HttpStatusCode.NotFound, stranger);
        Assert.Equal(HttpStatusCode.NotFound, unknown);
        Assert.Equal(3, await NotificationScenario.UnreadAsync(http));
    }

    [Fact]
    public async Task Mark_all_read_reports_how_many_it_changed_and_touches_only_the_callers_notifications()
    {
        using var world = await _jobs.NewWorldAsync();
        var requestId = await RequestScenario.CreateAsync(
            world.Customer.Http, world.ServiceId, world.RegionId, world.CityId, Unique("All"));
        await QuoteScenario.SubmitAsync(world.Provider.Http, requestId, 300m);

        var providerUnread = await NotificationScenario.UnreadAsync(world.Provider.Http);
        var customerUnread = await NotificationScenario.UnreadAsync(world.Customer.Http);
        Assert.True(providerUnread >= 2);
        Assert.Equal(1, customerUnread);

        var (status, body) = await ApiJson.SendAsync(world.Provider.Http, HttpMethod.Post, "/api/notifications/read-all", new { });
        var (_, repeat) = await ApiJson.SendAsync(world.Provider.Http, HttpMethod.Post, "/api/notifications/read-all", new { });

        Assert.Equal(HttpStatusCode.OK, status);
        Assert.Equal(providerUnread, body.GetProperty("updated").GetInt32());
        Assert.Equal(0, repeat.GetProperty("updated").GetInt32());
        Assert.Equal(0, await NotificationScenario.UnreadAsync(world.Provider.Http));
        Assert.Equal(customerUnread, await NotificationScenario.UnreadAsync(world.Customer.Http));
    }
}