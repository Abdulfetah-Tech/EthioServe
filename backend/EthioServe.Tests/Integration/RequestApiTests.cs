using System.Globalization;
using System.Net;
using System.Text.Json;
using EthioServe.Domain.Entities;
using EthioServe.Infrastructure.Persistence;
using EthioServe.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace EthioServe.Tests.Integration;

[Collection(ApiCollection.Name)]
public sealed class RequestApiTests(ApiFactory factory)
{
    private const string UnknownId = "00000000-0000-0000-0000-000000000001";

    private readonly ApiSession _session = new(factory);
    private readonly ProviderScenario _scenario = new(factory, new ApiSession(factory));

    public static IEnumerable<object[]> CustomerRoutes()
    {
        yield return new object[] { "POST", "/api/requests" };
        yield return new object[] { "GET", "/api/requests" };
        yield return new object[] { "GET", $"/api/requests/{UnknownId}" };
        yield return new object[] { "PUT", $"/api/requests/{UnknownId}" };
        yield return new object[] { "POST", $"/api/requests/{UnknownId}/cancel" };
    }

    private static object? BodyFor(string method) => method is "POST" or "PUT" ? new { } : null;

    private static bool HasField(JsonElement problem, string field) =>
        problem.GetProperty("errors").EnumerateObject().Any(p => string.Equals(p.Name, field, StringComparison.OrdinalIgnoreCase));

    private async Task<(Guid ServiceId, int RegionId, int CityId)> CatalogAsync()
    {
        using var admin = await _session.AdminAsync();
        var (_, serviceId) = await _scenario.NewServiceAsync(admin.Http);
        using var anonymous = _session.CreateAnonymousClient();
        var (regionId, cityId) = await ProviderScenario.LocationAsync(anonymous, "Addis Ababa", "Addis Ababa");

        return (serviceId, regionId, cityId);
    }

    private static Task<(HttpStatusCode Status, JsonElement Body)> PostAsync(HttpClient client, object body) =>
        ApiJson.SendAsync(client, HttpMethod.Post, "/api/requests", body);

    private static async Task<List<string>> ListTitlesAsync(HttpClient client, string query = "")
    {
        var (status, body) = await ApiJson.SendAsync(client, HttpMethod.Get, $"/api/requests{query}");
        Assert.Equal(HttpStatusCode.OK, status);

        return body.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("title").GetString()!).ToList();
    }

    // ------------------------------------------------------------- access control

    [Theory]
    [MemberData(nameof(CustomerRoutes))]
    public async Task Only_customers_can_use_the_request_routes(string method, string url)
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

    // ------------------------------------------------------------- creating

    [Fact]
    public async Task A_customer_can_create_a_request_and_read_it_back()
    {
        var (serviceId, regionId, cityId) = await CatalogAsync();
        using var customer = await _session.CustomerAsync();
        var date = RequestScenario.InDays(7);

        var (status, created) = await PostAsync(customer.Http,
            RequestScenario.Body(serviceId, regionId, cityId, budgetMin: 500m, budgetMax: 1500m, date: date, time: new TimeOnly(14, 30)));
        var id = created.GetProperty("id").GetGuid();
        var (readStatus, read) = await ApiJson.SendAsync(customer.Http, HttpMethod.Get, $"/api/requests/{id}");

        Assert.Equal(HttpStatusCode.Created, status);
        Assert.Equal(HttpStatusCode.OK, readStatus);
        Assert.Equal("Pending", read.GetProperty("status").GetString());
        Assert.Equal(serviceId, read.GetProperty("serviceId").GetGuid());
        Assert.Equal("Addis Ababa", read.GetProperty("cityName").GetString());
        Assert.Equal("Bole, near the main road", read.GetProperty("address").GetString());
        Assert.Equal(500m, read.GetProperty("budgetMin").GetDecimal());
        Assert.Equal(1500m, read.GetProperty("budgetMax").GetDecimal());
        Assert.Equal(date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), read.GetProperty("preferredDate").GetString());
        Assert.Equal("14:30:00", read.GetProperty("preferredTime").GetString());
        Assert.False(string.IsNullOrEmpty(read.GetProperty("categoryName").GetString()));
    }

    [Fact]
    public async Task Invalid_input_returns_400_with_field_errors()
    {
        var (serviceId, regionId, cityId) = await CatalogAsync();
        using var customer = await _session.CustomerAsync();

        var (shortTitle, shortTitleBody) = await PostAsync(customer.Http, RequestScenario.Body(serviceId, regionId, cityId, title: "Pipe"));
        var (shortDescription, shortDescriptionBody) = await PostAsync(customer.Http,
            RequestScenario.Body(serviceId, regionId, cityId, description: "too short"));
        var (reversed, reversedBody) = await PostAsync(customer.Http,
            RequestScenario.Body(serviceId, regionId, cityId, budgetMin: 900m, budgetMax: 100m));
        var (negative, negativeBody) = await PostAsync(customer.Http, RequestScenario.Body(serviceId, regionId, cityId, budgetMin: -5m));
        var (timeOnly, timeOnlyBody) = await PostAsync(customer.Http,
            RequestScenario.Body(serviceId, regionId, cityId, time: new TimeOnly(14, 0)));
        var (noRegion, noRegionBody) = await PostAsync(customer.Http, RequestScenario.Body(serviceId, 0, cityId));

        Assert.Equal(HttpStatusCode.BadRequest, shortTitle);
        Assert.True(HasField(shortTitleBody, "title"));
        Assert.Equal(HttpStatusCode.BadRequest, shortDescription);
        Assert.True(HasField(shortDescriptionBody, "description"));
        Assert.Equal(HttpStatusCode.BadRequest, reversed);
        Assert.True(HasField(reversedBody, "budgetMax"));
        Assert.Equal(HttpStatusCode.BadRequest, negative);
        Assert.True(HasField(negativeBody, "budgetMin"));
        Assert.Equal(HttpStatusCode.BadRequest, timeOnly);
        Assert.True(HasField(timeOnlyBody, "preferredTime"));
        Assert.Equal(HttpStatusCode.BadRequest, noRegion);
        Assert.True(HasField(noRegionBody, "regionId"));
    }

    [Fact]
    public async Task The_service_must_be_available_and_the_city_must_belong_to_the_region()
    {
        var (serviceId, regionId, cityId) = await CatalogAsync();
        using var customer = await _session.CustomerAsync();
        using var admin = await _session.AdminAsync();
        using var anonymous = _session.CreateAnonymousClient();

        var (_, inactiveServiceId) = await _scenario.NewServiceAsync(admin.Http);
        await ApiJson.SendAsync(admin.Http, HttpMethod.Post, $"/api/admin/services/{inactiveServiceId}/deactivate");
        var (_, adamaCity) = await ProviderScenario.LocationAsync(anonymous, "Oromia", "Adama");

        var (unknown, unknownBody) = await PostAsync(customer.Http, RequestScenario.Body(Guid.NewGuid(), regionId, cityId));
        var (inactive, inactiveBody) = await PostAsync(customer.Http, RequestScenario.Body(inactiveServiceId, regionId, cityId));
        var (wrongCity, wrongCityBody) = await PostAsync(customer.Http, RequestScenario.Body(serviceId, regionId, adamaCity));

        Assert.Equal(HttpStatusCode.BadRequest, unknown);
        Assert.True(HasField(unknownBody, "serviceId"));
        Assert.Equal(HttpStatusCode.BadRequest, inactive);
        Assert.True(HasField(inactiveBody, "serviceId"));
        Assert.Equal(HttpStatusCode.BadRequest, wrongCity);
        Assert.True(HasField(wrongCityBody, "cityId"));
    }

    [Fact]
    public async Task The_preferred_date_cannot_be_in_the_past_but_today_is_fine()
    {
        var (serviceId, regionId, cityId) = await CatalogAsync();
        using var customer = await _session.CustomerAsync();

        var (past, pastBody) = await PostAsync(customer.Http,
            RequestScenario.Body(serviceId, regionId, cityId, date: RequestScenario.InDays(-3)));
        var (today, _) = await PostAsync(customer.Http,
            RequestScenario.Body(serviceId, regionId, cityId, date: RequestScenario.InDays(0)));

        Assert.Equal(HttpStatusCode.BadRequest, past);
        Assert.True(HasField(pastBody, "preferredDate"));
        Assert.Equal(HttpStatusCode.Created, today);
    }

    [Fact]
    public async Task Clients_cannot_set_the_id_owner_status_or_reason()
    {
        var (serviceId, regionId, cityId) = await CatalogAsync();
        using var customer = await _session.CustomerAsync();
        var forcedId = Guid.NewGuid();

        var (status, body) = await PostAsync(customer.Http, new
        {
            serviceId,
            title = "Leaking kitchen pipe",
            description = "The pipe under the kitchen sink leaks and needs repair soon.",
            address = "Bole",
            regionId,
            cityId,
            id = forcedId,
            customerId = Guid.NewGuid(),
            status = "Completed",
            cancelledReason = "forced"
        });

        Assert.Equal(HttpStatusCode.Created, status);
        Assert.NotEqual(forcedId, body.GetProperty("id").GetGuid());
        Assert.Equal("Pending", body.GetProperty("status").GetString());
        Assert.Equal(JsonValueKind.Null, body.GetProperty("cancelledReason").ValueKind);
    }

    [Fact]
    public async Task A_customer_can_have_at_most_ten_open_requests()
    {
        var (serviceId, regionId, cityId) = await CatalogAsync();
        using var customer = await _session.CustomerAsync();
        Guid first = Guid.Empty;

        for (var i = 1; i <= 10; i++)
        {
            var id = await RequestScenario.CreateAsync(customer.Http, serviceId, regionId, cityId, $"Request number {i}");
            first = first == Guid.Empty ? id : first;
        }

        var (eleventh, _) = await PostAsync(customer.Http, RequestScenario.Body(serviceId, regionId, cityId, title: "Request number 11"));
        await ApiJson.SendAsync(customer.Http, HttpMethod.Post, $"/api/requests/{first}/cancel", new { });
        var (afterCancel, _) = await PostAsync(customer.Http, RequestScenario.Body(serviceId, regionId, cityId, title: "Request number 12"));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, eleventh);
        Assert.Equal(HttpStatusCode.Created, afterCancel);
    }

    // ------------------------------------------------------------- listing

    [Fact]
    public async Task Requests_are_listed_newest_first_with_paging_and_only_for_their_owner()
    {
        var (serviceId, regionId, cityId) = await CatalogAsync();
        using var customer = await _session.CustomerAsync();
        using var other = await _session.CustomerAsync();

        await RequestScenario.CreateAsync(customer.Http, serviceId, regionId, cityId, "First job");
        await RequestScenario.CreateAsync(customer.Http, serviceId, regionId, cityId, "Second job");
        await RequestScenario.CreateAsync(customer.Http, serviceId, regionId, cityId, "Third job");

        var (_, firstPage) = await ApiJson.SendAsync(customer.Http, HttpMethod.Get, "/api/requests?pageSize=2");
        var (_, secondPage) = await ApiJson.SendAsync(customer.Http, HttpMethod.Get, "/api/requests?pageSize=2&page=2");

        Assert.Equal(new[] { "Third job", "Second job" }, firstPage.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("title").GetString()));
        Assert.Equal(new[] { "First job" }, secondPage.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("title").GetString()));
        Assert.Equal(3, firstPage.GetProperty("totalCount").GetInt32());
        Assert.Equal(2, firstPage.GetProperty("totalPages").GetInt32());
        Assert.Empty(await ListTitlesAsync(other.Http));
    }

    [Fact]
    public async Task The_list_can_be_filtered_by_status_and_rejects_unknown_statuses()
    {
        var (serviceId, regionId, cityId) = await CatalogAsync();
        using var customer = await _session.CustomerAsync();
        await RequestScenario.CreateAsync(customer.Http, serviceId, regionId, cityId, "Stay open one");
        await RequestScenario.CreateAsync(customer.Http, serviceId, regionId, cityId, "Stay open two");
        var cancelled = await RequestScenario.CreateAsync(customer.Http, serviceId, regionId, cityId, "Will be cancelled");
        await ApiJson.SendAsync(customer.Http, HttpMethod.Post, $"/api/requests/{cancelled}/cancel", new { });

        var pending = await ListTitlesAsync(customer.Http, "?status=Pending");
        var cancelledTitles = await ListTitlesAsync(customer.Http, "?status=Cancelled");
        var (badStatus, _) = await ApiJson.SendAsync(customer.Http, HttpMethod.Get, "/api/requests?status=banana");

        Assert.Equal(2, pending.Count);
        Assert.Equal(new[] { "Will be cancelled" }, cancelledTitles);
        Assert.Equal(HttpStatusCode.BadRequest, badStatus);
    }

    // ------------------------------------------------------------- ownership

    [Fact]
    public async Task Other_customers_cannot_see_edit_or_cancel_a_request()
    {
        var (serviceId, regionId, cityId) = await CatalogAsync();
        using var owner = await _session.CustomerAsync();
        using var intruder = await _session.CustomerAsync();
        var id = await RequestScenario.CreateAsync(owner.Http, serviceId, regionId, cityId, "Private job");

        var (get, _) = await ApiJson.SendAsync(intruder.Http, HttpMethod.Get, $"/api/requests/{id}");
        var (put, _) = await ApiJson.SendAsync(intruder.Http, HttpMethod.Put, $"/api/requests/{id}",
            RequestScenario.Body(serviceId, regionId, cityId, title: "Hijacked job"));
        var (cancel, _) = await ApiJson.SendAsync(intruder.Http, HttpMethod.Post, $"/api/requests/{id}/cancel", new { });
        var (_, untouched) = await ApiJson.SendAsync(owner.Http, HttpMethod.Get, $"/api/requests/{id}");

        Assert.Equal(HttpStatusCode.NotFound, get);
        Assert.Equal(HttpStatusCode.NotFound, put);
        Assert.Equal(HttpStatusCode.NotFound, cancel);
        Assert.Equal("Private job", untouched.GetProperty("title").GetString());
        Assert.Equal("Pending", untouched.GetProperty("status").GetString());
    }

    [Fact]
    public async Task Unknown_requests_return_404()
    {
        using var customer = await _session.CustomerAsync();

        var (get, _) = await ApiJson.SendAsync(customer.Http, HttpMethod.Get, $"/api/requests/{UnknownId}");
        var (cancel, _) = await ApiJson.SendAsync(customer.Http, HttpMethod.Post, $"/api/requests/{UnknownId}/cancel", new { });

        Assert.Equal(HttpStatusCode.NotFound, get);
        Assert.Equal(HttpStatusCode.NotFound, cancel);
    }

    // ------------------------------------------------------------- editing and cancelling

    [Fact]
    public async Task A_pending_request_can_be_edited_but_its_service_stays_the_same()
    {
        var (serviceId, regionId, cityId) = await CatalogAsync();
        using var customer = await _session.CustomerAsync();
        var id = await RequestScenario.CreateAsync(customer.Http, serviceId, regionId, cityId, "Original title");

        var (status, body) = await ApiJson.SendAsync(customer.Http, HttpMethod.Put, $"/api/requests/{id}",
            RequestScenario.Body(Guid.NewGuid(), regionId, cityId, title: "Edited title", budgetMin: 200m, budgetMax: 800m));
        var (invalid, invalidBody) = await ApiJson.SendAsync(customer.Http, HttpMethod.Put, $"/api/requests/{id}",
            RequestScenario.Body(serviceId, regionId, cityId, title: "Pipe"));

        Assert.Equal(HttpStatusCode.OK, status);
        Assert.Equal("Edited title", body.GetProperty("title").GetString());
        Assert.Equal(200m, body.GetProperty("budgetMin").GetDecimal());
        Assert.Equal(serviceId, body.GetProperty("serviceId").GetGuid());
        Assert.Equal(HttpStatusCode.BadRequest, invalid);
        Assert.True(HasField(invalidBody, "title"));
    }

    [Fact]
    public async Task A_cancelled_request_cannot_be_edited_or_cancelled_again()
    {
        var (serviceId, regionId, cityId) = await CatalogAsync();
        using var customer = await _session.CustomerAsync();
        var id = await RequestScenario.CreateAsync(customer.Http, serviceId, regionId, cityId);

        var (cancelled, cancelledBody) = await ApiJson.SendAsync(
            customer.Http, HttpMethod.Post, $"/api/requests/{id}/cancel", new { reason = "  Found someone else  " });
        var (edit, _) = await ApiJson.SendAsync(customer.Http, HttpMethod.Put, $"/api/requests/{id}",
            RequestScenario.Body(serviceId, regionId, cityId, title: "Too late"));
        var (again, _) = await ApiJson.SendAsync(customer.Http, HttpMethod.Post, $"/api/requests/{id}/cancel", new { });

        Assert.Equal(HttpStatusCode.OK, cancelled);
        Assert.Equal("Cancelled", cancelledBody.GetProperty("status").GetString());
        Assert.Equal("Found someone else", cancelledBody.GetProperty("cancelledReason").GetString());
        Assert.Equal(HttpStatusCode.UnprocessableEntity, edit);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, again);
    }

    [Fact]
    public async Task A_cancellation_reason_over_500_characters_is_rejected()
    {
        var (serviceId, regionId, cityId) = await CatalogAsync();
        using var customer = await _session.CustomerAsync();
        var id = await RequestScenario.CreateAsync(customer.Http, serviceId, regionId, cityId);

        var (status, body) = await ApiJson.SendAsync(
            customer.Http, HttpMethod.Post, $"/api/requests/{id}/cancel", new { reason = new string('x', 501) });
        var (_, request) = await ApiJson.SendAsync(customer.Http, HttpMethod.Get, $"/api/requests/{id}");

        Assert.Equal(HttpStatusCode.BadRequest, status);
        Assert.True(HasField(body, "reason"));
        Assert.Equal("Pending", request.GetProperty("status").GetString());
    }

    // ------------------------------------------------------------- audit

    [Fact]
    public async Task Creating_editing_and_cancelling_are_audited_without_the_address()
    {
        var (serviceId, regionId, cityId) = await CatalogAsync();
        using var customer = await _session.CustomerAsync();
        var id = await RequestScenario.CreateAsync(customer.Http, serviceId, regionId, cityId, "Audited job");
        await ApiJson.SendAsync(customer.Http, HttpMethod.Put, $"/api/requests/{id}",
            RequestScenario.Body(serviceId, regionId, cityId, title: "Audited job, edited"));
        await ApiJson.SendAsync(customer.Http, HttpMethod.Post, $"/api/requests/{id}/cancel", new { reason = "Not needed" });

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var entries = await db.AuditLogs.AsNoTracking()
            .Where(a => a.EntityId == id.ToString() && a.EntityName == nameof(ServiceRequest))
            .OrderBy(a => a.Id)
            .ToListAsync();

        Assert.Equal(
            new[] { "ServiceRequestCreated", "ServiceRequestUpdated", "ServiceRequestCancelled" },
            entries.Select(e => e.Action).ToArray());
        Assert.All(entries, e => Assert.Equal(customer.UserId, e.UserId));
        Assert.All(entries, e => Assert.DoesNotContain("Bole", e.NewValues));
        Assert.Contains("Cancelled", entries[2].NewValues);
        Assert.Contains("Pending", entries[2].OldValues);
    }
}