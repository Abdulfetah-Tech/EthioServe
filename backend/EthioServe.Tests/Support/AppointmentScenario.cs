using System.Net;
using System.Text.Json;
using EthioServe.Application.Appointments;

namespace EthioServe.Tests.Support;

/// <summary>A customer, a verified provider and the service they share.</summary>
public sealed record JobWorld(SignedInClient Provider, SignedInClient Customer, Guid ServiceId, int RegionId, int CityId)
    : IDisposable
{
    public void Dispose()
    {
        Provider.Dispose();
        Customer.Dispose();
    }
}

public sealed class JobBuilder(ApiFactory factory)
{
    private readonly ApiSession _session = new(factory);
    private readonly ProviderScenario _scenario = new(factory, new ApiSession(factory));

    public async Task<JobWorld> NewWorldAsync()
    {
        using var admin = await _session.AdminAsync();
        var (_, serviceId) = await _scenario.NewServiceAsync(admin.Http);
        using var anonymous = _session.CreateAnonymousClient();
        var (regionId, cityId) = await ProviderScenario.LocationAsync(anonymous, "Addis Ababa", "Addis Ababa");
        var provider = await _scenario.VerifiedProviderAsync(admin.Http, ProviderScenario.Unique("Crew"), serviceId, 300m);
        var customer = await _session.CustomerAsync();

        return new JobWorld(provider, customer, serviceId, regionId, cityId);
    }

    /// <summary>Another verified provider who offers the same service.</summary>
    public async Task<SignedInClient> ExtraProviderAsync(JobWorld world)
    {
        using var admin = await _session.AdminAsync();

        return await _scenario.VerifiedProviderAsync(admin.Http, ProviderScenario.Unique("Other crew"), world.ServiceId, 320m);
    }

    /// <summary>A new request that the customer has already accepted a quote for.</summary>
    public async Task<Guid> AcceptedRequestAsync(JobWorld world, int minutes = 120, SignedInClient? provider = null)
    {
        var who = provider ?? world.Provider;
        var requestId = await RequestScenario.CreateAsync(
            world.Customer.Http, world.ServiceId, world.RegionId, world.CityId, ProviderScenario.Unique("Job"));

        var (submitted, quote) = await QuoteScenario.SubmitRawAsync(who.Http, requestId, QuoteScenario.Body(500m, minutes: minutes));
        Assert.Equal(HttpStatusCode.Created, submitted);

        var (accepted, _) = await ApiJson.SendAsync(
            world.Customer.Http, HttpMethod.Post, $"/api/requests/{requestId}/quotes/{quote.GetProperty("id").GetGuid()}/accept");
        Assert.Equal(HttpStatusCode.OK, accepted);

        return requestId;
    }
}

public static class AppointmentScenario
{
    /// <summary>A time this many hours from now, cut to the minute the way the API stores it.</summary>
    public static DateTimeOffset InHours(double hours) => AppointmentRules.Normalize(DateTimeOffset.UtcNow.AddHours(hours));

    public static object Body(DateTimeOffset start, string? notes = null) => new { startAt = start, notes };

    public static Task<(HttpStatusCode Status, JsonElement Body)> ScheduleRawAsync(HttpClient provider, Guid requestId, object body) =>
        ApiJson.SendAsync(provider, HttpMethod.Post, $"/api/providers/me/requests/{requestId}/appointment", body);

    public static async Task<Guid> ScheduleAsync(HttpClient provider, Guid requestId, DateTimeOffset start, string? notes = null)
    {
        var (status, body) = await ScheduleRawAsync(provider, requestId, Body(start, notes));
        Assert.Equal(HttpStatusCode.Created, status);

        return body.GetProperty("id").GetGuid();
    }

    public static Task<(HttpStatusCode Status, JsonElement Body)> ProviderActionAsync(
        HttpClient provider, Guid appointmentId, string action, object? body = null) =>
        ApiJson.SendAsync(provider, HttpMethod.Post, $"/api/providers/me/appointments/{appointmentId}/{action}", body);

    public static Task<(HttpStatusCode Status, JsonElement Body)> CustomerActionAsync(
        HttpClient customer, Guid appointmentId, string action) =>
        ApiJson.SendAsync(customer, HttpMethod.Post, $"/api/appointments/{appointmentId}/{action}");

    public static Task<(HttpStatusCode Status, JsonElement Body)> ProviderGetAsync(HttpClient provider, Guid appointmentId) =>
        ApiJson.SendAsync(provider, HttpMethod.Get, $"/api/providers/me/appointments/{appointmentId}");

    public static Task<(HttpStatusCode Status, JsonElement Body)> CustomerGetAsync(HttpClient customer, Guid appointmentId) =>
        ApiJson.SendAsync(customer, HttpMethod.Get, $"/api/appointments/{appointmentId}");

    public static async Task<List<JsonElement>> ProviderListAsync(HttpClient provider, string query = "")
    {
        var (status, body) = await ApiJson.SendAsync(provider, HttpMethod.Get, $"/api/providers/me/appointments{query}");
        Assert.Equal(HttpStatusCode.OK, status);

        return body.GetProperty("items").EnumerateArray().ToList();
    }

    public static async Task<List<JsonElement>> CustomerListAsync(HttpClient customer, string query = "")
    {
        var (status, body) = await ApiJson.SendAsync(customer, HttpMethod.Get, $"/api/appointments{query}");
        Assert.Equal(HttpStatusCode.OK, status);

        return body.GetProperty("items").EnumerateArray().ToList();
    }
}