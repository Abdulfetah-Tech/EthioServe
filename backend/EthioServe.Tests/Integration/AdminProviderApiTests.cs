using System.Net;
using System.Text.Json;
using EthioServe.Domain.Entities;
using EthioServe.Infrastructure.Persistence;
using EthioServe.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace EthioServe.Tests.Integration;

[Collection(ApiCollection.Name)]
public sealed class AdminProviderApiTests(ApiFactory factory)
{
    private const string UnknownId = "00000000-0000-0000-0000-000000000001";

    private readonly ApiSession _session = new(factory);
    private readonly ProviderScenario _scenario = new(factory, new ApiSession(factory));

    public static IEnumerable<object[]> AdminProviderRoutes()
    {
        yield return new object[] { "GET", "/api/admin/providers" };
        yield return new object[] { "GET", $"/api/admin/providers/{UnknownId}" };
        yield return new object[] { "POST", $"/api/admin/providers/{UnknownId}/approve" };
        yield return new object[] { "POST", $"/api/admin/providers/{UnknownId}/reject" };
        yield return new object[] { "POST", $"/api/admin/providers/{UnknownId}/suspend" };
        yield return new object[] { "POST", $"/api/admin/providers/{UnknownId}/reinstate" };
    }

    private static object? BodyFor(string method) => method == "POST" ? new { } : null;

    private static bool HasField(JsonElement problem, string field) =>
        problem.GetProperty("errors").EnumerateObject().Any(p => string.Equals(p.Name, field, StringComparison.OrdinalIgnoreCase));

    private static string StatusOf(JsonElement detail) =>
        detail.GetProperty("profile").GetProperty("verificationStatus").GetString()!;

    private static string Url(SignedInClient provider, string suffix = "") =>
        $"/api/admin/providers/{provider.ProfileId}{suffix}";

    // ------------------------------------------------------------- access control

    [Theory]
    [MemberData(nameof(AdminProviderRoutes))]
    public async Task Only_admins_can_use_the_verification_routes(string method, string url)
    {
        using var anonymous = _session.CreateAnonymousClient();
        using var customer = await _session.CustomerAsync();
        using var provider = await _session.RegisteredProviderAsync();

        var (anonymousStatus, _) = await ApiJson.SendAsync(anonymous, new HttpMethod(method), url, BodyFor(method));
        Assert.Equal(HttpStatusCode.Unauthorized, anonymousStatus);

        foreach (var user in new[] { customer, provider })
        {
            var (status, _) = await ApiJson.SendAsync(user.Http, new HttpMethod(method), url, BodyFor(method));
            Assert.Equal(HttpStatusCode.Forbidden, status);
        }
    }

    // ------------------------------------------------------------- queue and detail

    [Fact]
    public async Task The_queue_can_be_filtered_by_status_and_searched_by_name()
    {
        using var admin = await _session.AdminAsync();
        var (_, serviceId) = await _scenario.NewServiceAsync(admin.Http);
        var token = ProviderScenario.Unique("Tok");
        using var pending = await _scenario.PendingProviderAsync($"{token} Pending", serviceId, 100m);
        using var unverified = await _session.RegisteredProviderAsync($"{token} Unverified");

        var (_, pendingOnly) = await ApiJson.SendAsync(
            admin.Http, HttpMethod.Get, $"/api/admin/providers?status=Pending&search={token}");
        var (_, everyone) = await ApiJson.SendAsync(admin.Http, HttpMethod.Get, $"/api/admin/providers?search={token}");

        var item = Assert.Single(pendingOnly.GetProperty("items").EnumerateArray());
        Assert.Equal(pending.ProfileId, item.GetProperty("id").GetGuid());
        Assert.Equal("Pending", item.GetProperty("verificationStatus").GetString());
        Assert.Equal(1, item.GetProperty("activeServiceCount").GetInt32());
        Assert.Equal(2, everyone.GetProperty("totalCount").GetInt32());
    }

    [Fact]
    public async Task The_queue_is_paged()
    {
        using var admin = await _session.AdminAsync();
        var (_, serviceId) = await _scenario.NewServiceAsync(admin.Http);
        var token = ProviderScenario.Unique("Pg");
        using var first = await _scenario.PendingProviderAsync($"{token} One", serviceId, 100m);
        using var second = await _scenario.PendingProviderAsync($"{token} Two", serviceId, 100m);
        using var third = await _scenario.PendingProviderAsync($"{token} Three", serviceId, 100m);

        var (_, page1) = await ApiJson.SendAsync(admin.Http, HttpMethod.Get, $"/api/admin/providers?search={token}&pageSize=2");
        var (_, page2) = await ApiJson.SendAsync(admin.Http, HttpMethod.Get, $"/api/admin/providers?search={token}&pageSize=2&page=2");

        Assert.Equal(3, page1.GetProperty("totalCount").GetInt32());
        Assert.Equal(2, page1.GetProperty("totalPages").GetInt32());
        Assert.Equal(2, page1.GetProperty("items").GetArrayLength());
        Assert.Equal(1, page2.GetProperty("items").GetArrayLength());
    }

    [Fact]
    public async Task The_detail_shows_the_profile_the_offers_and_the_owner()
    {
        using var admin = await _session.AdminAsync();
        var (_, serviceId) = await _scenario.NewServiceAsync(admin.Http);
        using var provider = await _scenario.PendingProviderAsync(ProviderScenario.Unique("Detail"), serviceId, 250m);

        var (status, detail) = await ApiJson.SendAsync(admin.Http, HttpMethod.Get, Url(provider));

        Assert.Equal(HttpStatusCode.OK, status);
        Assert.Equal(provider.UserId, detail.GetProperty("userId").GetGuid());
        Assert.Equal("Pending", StatusOf(detail));
        var offer = Assert.Single(detail.GetProperty("offers").EnumerateArray());
        Assert.Equal(serviceId, offer.GetProperty("serviceId").GetGuid());
        Assert.Equal(250m, offer.GetProperty("price").GetDecimal());
        Assert.False(string.IsNullOrEmpty(detail.GetProperty("profile").GetProperty("phoneNumber").GetString()));
    }

    [Fact]
    public async Task Unknown_providers_return_404()
    {
        using var admin = await _session.AdminAsync();

        var (get, _) = await ApiJson.SendAsync(admin.Http, HttpMethod.Get, $"/api/admin/providers/{UnknownId}");
        var (approve, _) = await ApiJson.SendAsync(admin.Http, HttpMethod.Post, $"/api/admin/providers/{UnknownId}/approve");

        Assert.Equal(HttpStatusCode.NotFound, get);
        Assert.Equal(HttpStatusCode.NotFound, approve);
    }

    // ------------------------------------------------------------- decisions

    [Fact]
    public async Task Approving_verifies_the_provider_once_and_is_audited_with_the_admin()
    {
        using var admin = await _session.AdminAsync();
        var (_, serviceId) = await _scenario.NewServiceAsync(admin.Http);
        using var provider = await _scenario.PendingProviderAsync(ProviderScenario.Unique("Approve"), serviceId, 100m);

        var (first, body) = await ApiJson.SendAsync(admin.Http, HttpMethod.Post, Url(provider, "/approve"));
        var (second, _) = await ApiJson.SendAsync(admin.Http, HttpMethod.Post, Url(provider, "/approve"));

        Assert.Equal(HttpStatusCode.OK, first);
        Assert.Equal("Verified", StatusOf(body));
        Assert.Equal(HttpStatusCode.UnprocessableEntity, second);

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var entries = await db.AuditLogs.AsNoTracking()
            .Where(a => a.EntityId == provider.ProfileId.ToString() && a.EntityName == nameof(ProviderProfile))
            .OrderBy(a => a.Id)
            .ToListAsync();

        Assert.Equal(new[] { "ProviderVerificationRequested", "ProviderApproved" }, entries.Select(e => e.Action).ToArray());
        Assert.Equal(provider.UserId, entries[0].UserId);
        Assert.Equal(admin.UserId, entries[1].UserId);
        Assert.Contains("Pending", entries[1].OldValues);
        Assert.Contains("Verified", entries[1].NewValues);
    }

    [Fact]
    public async Task Rejecting_needs_a_reason_the_provider_can_read_and_they_can_apply_again()
    {
        using var admin = await _session.AdminAsync();
        var (_, serviceId) = await _scenario.NewServiceAsync(admin.Http);
        using var provider = await _scenario.PendingProviderAsync(ProviderScenario.Unique("Reject"), serviceId, 100m);

        var (blank, blankBody) = await ApiJson.SendAsync(admin.Http, HttpMethod.Post, Url(provider, "/reject"), new { reason = "  " });
        var (missing, _) = await ApiJson.SendAsync(admin.Http, HttpMethod.Post, Url(provider, "/reject"), new { });
        var (rejected, body) = await ApiJson.SendAsync(
            admin.Http, HttpMethod.Post, Url(provider, "/reject"), new { reason = "Please add clearer details" });
        var (again, _) = await ApiJson.SendAsync(admin.Http, HttpMethod.Post, Url(provider, "/reject"), new { reason = "Still no" });

        Assert.Equal(HttpStatusCode.BadRequest, blank);
        Assert.True(HasField(blankBody, "reason"));
        Assert.Equal(HttpStatusCode.BadRequest, missing);
        Assert.Equal(HttpStatusCode.OK, rejected);
        Assert.Equal("Rejected", StatusOf(body));
        Assert.Equal(HttpStatusCode.UnprocessableEntity, again);

        var (_, mine) = await ApiJson.SendAsync(provider.Http, HttpMethod.Get, "/api/providers/me");
        Assert.Equal("Rejected", mine.GetProperty("verificationStatus").GetString());
        Assert.Equal("Please add clearer details", mine.GetProperty("verificationNote").GetString());

        var (reapply, reapplied) = await ApiJson.SendAsync(provider.Http, HttpMethod.Post, "/api/providers/me/verification-request");
        Assert.Equal(HttpStatusCode.OK, reapply);
        Assert.Equal("Pending", reapplied.GetProperty("verificationStatus").GetString());
        Assert.Equal(JsonValueKind.Null, reapplied.GetProperty("verificationNote").ValueKind);
    }

    [Fact]
    public async Task Suspending_hides_the_provider_publicly_and_reinstating_brings_them_back()
    {
        using var admin = await _session.AdminAsync();
        using var anonymous = _session.CreateAnonymousClient();
        var (_, serviceId) = await _scenario.NewServiceAsync(admin.Http);
        using var provider = await _scenario.VerifiedProviderAsync(admin.Http, ProviderScenario.Unique("Susp"), serviceId, 100m);
        var publicUrl = $"/api/providers/{provider.ProfileId}";

        var (visible, _, _) = await ApiJson.GetAsync(anonymous, publicUrl);

        var (noReason, _) = await ApiJson.SendAsync(admin.Http, HttpMethod.Post, Url(provider, "/suspend"), new { reason = "" });
        var (suspended, suspendedBody) = await ApiJson.SendAsync(
            admin.Http, HttpMethod.Post, Url(provider, "/suspend"), new { reason = "Customer complaints" });
        var (hidden, _, _) = await ApiJson.GetAsync(anonymous, publicUrl);

        var (reinstated, reinstatedBody) = await ApiJson.SendAsync(admin.Http, HttpMethod.Post, Url(provider, "/reinstate"));
        var (restored, _, _) = await ApiJson.GetAsync(anonymous, publicUrl);
        var (reinstateAgain, _) = await ApiJson.SendAsync(admin.Http, HttpMethod.Post, Url(provider, "/reinstate"));

        Assert.Equal(HttpStatusCode.OK, visible);
        Assert.Equal(HttpStatusCode.BadRequest, noReason);
        Assert.Equal(HttpStatusCode.OK, suspended);
        Assert.Equal("Suspended", StatusOf(suspendedBody));
        Assert.Equal(HttpStatusCode.NotFound, hidden);
        Assert.Equal(HttpStatusCode.OK, reinstated);
        Assert.Equal("Verified", StatusOf(reinstatedBody));
        Assert.Equal(HttpStatusCode.OK, restored);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, reinstateAgain);
    }

    [Fact]
    public async Task Moves_the_state_machine_does_not_allow_return_422_and_change_nothing()
    {
        using var admin = await _session.AdminAsync();
        var (_, serviceId) = await _scenario.NewServiceAsync(admin.Http);
        using var unverified = await _session.RegisteredProviderAsync(ProviderScenario.Unique("Unv"));
        using var pending = await _scenario.PendingProviderAsync(ProviderScenario.Unique("Pen"), serviceId, 100m);

        var (approveUnverified, _) = await ApiJson.SendAsync(admin.Http, HttpMethod.Post, Url(unverified, "/approve"));
        var (rejectUnverified, _) = await ApiJson.SendAsync(admin.Http, HttpMethod.Post, Url(unverified, "/reject"), new { reason = "No" });
        var (suspendPending, _) = await ApiJson.SendAsync(admin.Http, HttpMethod.Post, Url(pending, "/suspend"), new { reason = "No" });
        var (reinstatePending, _) = await ApiJson.SendAsync(admin.Http, HttpMethod.Post, Url(pending, "/reinstate"));

        Assert.All(
            new[] { approveUnverified, rejectUnverified, suspendPending, reinstatePending },
            status => Assert.Equal(HttpStatusCode.UnprocessableEntity, status));

        var (_, unchangedUnverified) = await ApiJson.SendAsync(admin.Http, HttpMethod.Get, Url(unverified));
        var (_, unchangedPending) = await ApiJson.SendAsync(admin.Http, HttpMethod.Get, Url(pending));
        Assert.Equal("Unverified", StatusOf(unchangedUnverified));
        Assert.Equal("Pending", StatusOf(unchangedPending));
    }
}