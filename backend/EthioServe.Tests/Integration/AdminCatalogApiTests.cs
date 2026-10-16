using System.Net;
using System.Text.Json;
using EthioServe.Domain.Entities;
using EthioServe.Infrastructure.Persistence;
using EthioServe.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace EthioServe.Tests.Integration;

[Collection(ApiCollection.Name)]
public sealed class AdminCatalogApiTests(ApiFactory factory)
{
    private const string UnknownId = "00000000-0000-0000-0000-000000000001";

    private readonly ApiSession _session = new(factory);

    public static IEnumerable<object[]> AdminRoutes()
    {
        yield return new object[] { "GET", "/api/admin/categories" };
        yield return new object[] { "POST", "/api/admin/categories" };
        yield return new object[] { "GET", $"/api/admin/categories/{UnknownId}" };
        yield return new object[] { "PUT", $"/api/admin/categories/{UnknownId}" };
        yield return new object[] { "POST", $"/api/admin/categories/{UnknownId}/deactivate" };
        yield return new object[] { "GET", "/api/admin/services" };
        yield return new object[] { "POST", "/api/admin/services" };
        yield return new object[] { "PUT", $"/api/admin/services/{UnknownId}" };
        yield return new object[] { "POST", $"/api/admin/services/{UnknownId}/activate" };
    }

    private static string Unique(string prefix) => $"{prefix}-{Guid.NewGuid():N}"[..24];

    private static object? BodyFor(string method) => method is "POST" or "PUT" ? new { } : null;

    private static async Task<Guid> CreateCategoryAsync(HttpClient admin, string? name = null)
    {
        var (status, body) = await ApiJson.SendAsync(
            admin, HttpMethod.Post, "/api/admin/categories", new { name = name ?? Unique("Cat"), description = "Test category" });

        Assert.Equal(HttpStatusCode.Created, status);
        return body.GetProperty("id").GetGuid();
    }

    private static async Task<Guid> CreateServiceAsync(HttpClient admin, Guid categoryId, string? name = null)
    {
        var (status, body) = await ApiJson.SendAsync(admin, HttpMethod.Post, "/api/admin/services", new
        {
            categoryId,
            name = name ?? Unique("Svc"),
            description = "Test service",
            basePrice = 100m,
            pricingType = "Fixed"
        });

        Assert.Equal(HttpStatusCode.Created, status);
        return body.GetProperty("id").GetGuid();
    }

    private static bool HasField(JsonElement problem, string field) =>
        problem.GetProperty("errors").EnumerateObject().Any(p => string.Equals(p.Name, field, StringComparison.OrdinalIgnoreCase));

    // ------------------------------------------------------------- access control

    [Theory]
    [MemberData(nameof(AdminRoutes))]
    public async Task Anonymous_callers_get_401_on_every_admin_route(string method, string url)
    {
        using var client = _session.CreateAnonymousClient();

        var (status, _) = await ApiJson.SendAsync(client, new HttpMethod(method), url, BodyFor(method));

        Assert.Equal(HttpStatusCode.Unauthorized, status);
    }

    [Theory]
    [MemberData(nameof(AdminRoutes))]
    public async Task Customers_and_providers_get_403_on_every_admin_route(string method, string url)
    {
        using var customer = await _session.CustomerAsync();
        using var provider = await _session.ProviderAsync();

        foreach (var user in new[] { customer, provider })
        {
            var (status, _) = await ApiJson.SendAsync(user.Http, new HttpMethod(method), url, BodyFor(method));

            Assert.Equal(HttpStatusCode.Forbidden, status);
        }
    }

    // ------------------------------------------------------------- categories

    [Fact]
    public async Task Created_categories_are_public_and_deactivated_ones_disappear_but_stay_visible_to_admins()
    {
        using var admin = await _session.AdminAsync();
        using var anonymous = _session.CreateAnonymousClient();
        var id = await CreateCategoryAsync(admin.Http);

        var (_, before, _) = await ApiJson.GetAsync(anonymous, "/api/categories");
        Assert.Contains(before.EnumerateArray(), c => c.GetProperty("id").GetGuid() == id);

        var (deactivated, body) = await ApiJson.SendAsync(admin.Http, HttpMethod.Post, $"/api/admin/categories/{id}/deactivate");
        Assert.Equal(HttpStatusCode.OK, deactivated);
        Assert.False(body.GetProperty("isActive").GetBoolean());

        var (_, hidden, _) = await ApiJson.GetAsync(anonymous, "/api/categories");
        Assert.DoesNotContain(hidden.EnumerateArray(), c => c.GetProperty("id").GetGuid() == id);

        var (_, adminList) = await ApiJson.SendAsync(admin.Http, HttpMethod.Get, "/api/admin/categories");
        Assert.Contains(adminList.EnumerateArray(), c => c.GetProperty("id").GetGuid() == id && !c.GetProperty("isActive").GetBoolean());

        await ApiJson.SendAsync(admin.Http, HttpMethod.Post, $"/api/admin/categories/{id}/activate");

        var (_, restored, _) = await ApiJson.GetAsync(anonymous, "/api/categories");
        Assert.Contains(restored.EnumerateArray(), c => c.GetProperty("id").GetGuid() == id);
    }

    [Fact]
    public async Task Duplicate_category_names_are_rejected_ignoring_case()
    {
        using var admin = await _session.AdminAsync();
        var name = Unique("Dup");
        await CreateCategoryAsync(admin.Http, name);

        var (status, _) = await ApiJson.SendAsync(
            admin.Http, HttpMethod.Post, "/api/admin/categories", new { name = name.ToUpperInvariant() });

        Assert.Equal(HttpStatusCode.Conflict, status);
    }

    [Fact]
    public async Task Renaming_a_category_to_an_existing_name_is_rejected_but_keeping_its_own_name_is_fine()
    {
        using var admin = await _session.AdminAsync();
        var firstName = Unique("First");
        var secondId = await CreateCategoryAsync(admin.Http);
        await CreateCategoryAsync(admin.Http, firstName);

        var (conflict, _) = await ApiJson.SendAsync(
            admin.Http, HttpMethod.Put, $"/api/admin/categories/{secondId}", new { name = firstName });
        var (_, current) = await ApiJson.SendAsync(admin.Http, HttpMethod.Get, $"/api/admin/categories/{secondId}");
        var (same, _) = await ApiJson.SendAsync(
            admin.Http, HttpMethod.Put, $"/api/admin/categories/{secondId}",
            new { name = current.GetProperty("name").GetString(), description = "Edited" });

        Assert.Equal(HttpStatusCode.Conflict, conflict);
        Assert.Equal(HttpStatusCode.OK, same);
    }

    [Fact]
    public async Task Invalid_category_input_returns_400_with_field_errors()
    {
        using var admin = await _session.AdminAsync();

        var (blank, blankBody) = await ApiJson.SendAsync(admin.Http, HttpMethod.Post, "/api/admin/categories", new { name = "   " });
        var (tooLong, tooLongBody) = await ApiJson.SendAsync(
            admin.Http, HttpMethod.Post, "/api/admin/categories", new { name = new string('x', 101) });

        Assert.Equal(HttpStatusCode.BadRequest, blank);
        Assert.True(HasField(blankBody, "name"));
        Assert.Equal(HttpStatusCode.BadRequest, tooLong);
        Assert.True(HasField(tooLongBody, "name"));
    }

    [Fact]
    public async Task Clients_cannot_set_the_id_state_or_counts_when_creating_a_category()
    {
        using var admin = await _session.AdminAsync();
        var forcedId = Guid.NewGuid();

        var (status, body) = await ApiJson.SendAsync(admin.Http, HttpMethod.Post, "/api/admin/categories", new
        {
            name = Unique("Mass"),
            id = forcedId,
            isActive = false,
            serviceCount = 99
        });

        Assert.Equal(HttpStatusCode.Created, status);
        Assert.NotEqual(forcedId, body.GetProperty("id").GetGuid());
        Assert.True(body.GetProperty("isActive").GetBoolean());
        Assert.Equal(0, body.GetProperty("serviceCount").GetInt32());
    }

    [Fact]
    public async Task Unknown_ids_return_404_problem_details()
    {
        using var admin = await _session.AdminAsync();

        var (get, _) = await ApiJson.SendAsync(admin.Http, HttpMethod.Get, $"/api/admin/categories/{UnknownId}");
        var (put, _) = await ApiJson.SendAsync(admin.Http, HttpMethod.Put, $"/api/admin/categories/{UnknownId}", new { name = "X" });
        var (service, _) = await ApiJson.SendAsync(admin.Http, HttpMethod.Get, $"/api/admin/services/{UnknownId}");

        Assert.Equal(HttpStatusCode.NotFound, get);
        Assert.Equal(HttpStatusCode.NotFound, put);
        Assert.Equal(HttpStatusCode.NotFound, service);
    }

    // ------------------------------------------------------------- services

    [Fact]
    public async Task Service_pricing_rules_are_enforced()
    {
        using var admin = await _session.AdminAsync();
        var categoryId = await CreateCategoryAsync(admin.Http);

        var (noPrice, noPriceBody) = await ApiJson.SendAsync(admin.Http, HttpMethod.Post, "/api/admin/services",
            new { categoryId, name = Unique("Svc"), pricingType = "Fixed" });
        var (negotiableWithPrice, _) = await ApiJson.SendAsync(admin.Http, HttpMethod.Post, "/api/admin/services",
            new { categoryId, name = Unique("Svc"), pricingType = "Negotiable", basePrice = 50m });
        var (negotiable, _) = await ApiJson.SendAsync(admin.Http, HttpMethod.Post, "/api/admin/services",
            new { categoryId, name = Unique("Svc"), pricingType = "Negotiable" });
        var (decimals, _) = await ApiJson.SendAsync(admin.Http, HttpMethod.Post, "/api/admin/services",
            new { categoryId, name = Unique("Svc"), pricingType = "Fixed", basePrice = 150.5m });

        Assert.Equal(HttpStatusCode.BadRequest, noPrice);
        Assert.True(HasField(noPriceBody, "basePrice"));
        Assert.Equal(HttpStatusCode.BadRequest, negotiableWithPrice);
        Assert.Equal(HttpStatusCode.Created, negotiable);
        Assert.Equal(HttpStatusCode.Created, decimals);
    }

    [Fact]
    public async Task Services_need_an_existing_category_and_a_unique_name_within_it()
    {
        using var admin = await _session.AdminAsync();
        var first = await CreateCategoryAsync(admin.Http);
        var second = await CreateCategoryAsync(admin.Http);
        var name = Unique("Svc");
        await CreateServiceAsync(admin.Http, first, name);

        var (missingCategory, missingBody) = await ApiJson.SendAsync(admin.Http, HttpMethod.Post, "/api/admin/services",
            new { categoryId = Guid.NewGuid(), name = Unique("Svc"), basePrice = 10m, pricingType = "Fixed" });
        var (duplicate, _) = await ApiJson.SendAsync(admin.Http, HttpMethod.Post, "/api/admin/services",
            new { categoryId = first, name = name.ToUpperInvariant(), basePrice = 10m, pricingType = "Fixed" });
        var (otherCategory, _) = await ApiJson.SendAsync(admin.Http, HttpMethod.Post, "/api/admin/services",
            new { categoryId = second, name, basePrice = 10m, pricingType = "Fixed" });

        Assert.Equal(HttpStatusCode.BadRequest, missingCategory);
        Assert.True(HasField(missingBody, "categoryId"));
        Assert.Equal(HttpStatusCode.Conflict, duplicate);
        Assert.Equal(HttpStatusCode.Created, otherCategory);
    }

    [Fact]
    public async Task Services_can_be_edited_and_their_category_cannot_change()
    {
        using var admin = await _session.AdminAsync();
        var categoryId = await CreateCategoryAsync(admin.Http);
        var id = await CreateServiceAsync(admin.Http, categoryId);

        var (status, body) = await ApiJson.SendAsync(admin.Http, HttpMethod.Put, $"/api/admin/services/{id}", new
        {
            name = Unique("Renamed"),
            description = "Edited",
            pricingType = "Hourly",
            basePrice = 250m,
            categoryId = Guid.NewGuid() // not part of the update contract: must be ignored
        });

        Assert.Equal(HttpStatusCode.OK, status);
        Assert.Equal("Hourly", body.GetProperty("pricingType").GetString());
        Assert.Equal(250m, body.GetProperty("basePrice").GetDecimal());
        Assert.Equal(categoryId, body.GetProperty("categoryId").GetGuid());
    }

    [Fact]
    public async Task Deactivating_a_service_hides_it_publicly_and_deactivating_a_category_hides_its_services()
    {
        using var admin = await _session.AdminAsync();
        using var anonymous = _session.CreateAnonymousClient();
        var categoryId = await CreateCategoryAsync(admin.Http);
        var serviceId = await CreateServiceAsync(admin.Http, categoryId);

        var (visible, _, _) = await ApiJson.GetAsync(anonymous, $"/api/services/{serviceId}");

        await ApiJson.SendAsync(admin.Http, HttpMethod.Post, $"/api/admin/services/{serviceId}/deactivate");
        var (serviceHidden, _, _) = await ApiJson.GetAsync(anonymous, $"/api/services/{serviceId}");

        await ApiJson.SendAsync(admin.Http, HttpMethod.Post, $"/api/admin/services/{serviceId}/activate");
        var (restored, _, _) = await ApiJson.GetAsync(anonymous, $"/api/services/{serviceId}");

        await ApiJson.SendAsync(admin.Http, HttpMethod.Post, $"/api/admin/categories/{categoryId}/deactivate");
        var (categoryHidesService, _, _) = await ApiJson.GetAsync(anonymous, $"/api/services/{serviceId}");
        var (adminStillSees, _) = await ApiJson.SendAsync(admin.Http, HttpMethod.Get, $"/api/admin/services/{serviceId}");

        Assert.Equal(HttpStatusCode.OK, visible);
        Assert.Equal(HttpStatusCode.NotFound, serviceHidden);
        Assert.Equal(HttpStatusCode.OK, restored);
        Assert.Equal(HttpStatusCode.NotFound, categoryHidesService);
        Assert.Equal(HttpStatusCode.OK, adminStillSees);
    }

    [Fact]
    public async Task Admin_service_list_supports_paging_and_hiding_inactive_items()
    {
        using var admin = await _session.AdminAsync();
        var categoryId = await CreateCategoryAsync(admin.Http);
        await CreateServiceAsync(admin.Http, categoryId);
        await CreateServiceAsync(admin.Http, categoryId);
        var inactive = await CreateServiceAsync(admin.Http, categoryId);
        await ApiJson.SendAsync(admin.Http, HttpMethod.Post, $"/api/admin/services/{inactive}/deactivate");

        var (_, all) = await ApiJson.SendAsync(admin.Http, HttpMethod.Get, $"/api/admin/services?categoryId={categoryId}&pageSize=2");
        var (_, activeOnly) = await ApiJson.SendAsync(
            admin.Http, HttpMethod.Get, $"/api/admin/services?categoryId={categoryId}&includeInactive=false");

        Assert.Equal(3, all.GetProperty("totalCount").GetInt32());
        Assert.Equal(2, all.GetProperty("items").GetArrayLength());
        Assert.Equal(2, all.GetProperty("totalPages").GetInt32());
        Assert.Equal(2, activeOnly.GetProperty("totalCount").GetInt32());
    }

    // ------------------------------------------------------------- audit

    [Fact]
    public async Task Every_change_is_audited_with_the_acting_admin_and_no_op_changes_are_not()
    {
        using var admin = await _session.AdminAsync();
        var name = Unique("Aud");
        var id = await CreateCategoryAsync(admin.Http, name);

        await ApiJson.SendAsync(admin.Http, HttpMethod.Put, $"/api/admin/categories/{id}", new { name, description = "Changed" });
        await ApiJson.SendAsync(admin.Http, HttpMethod.Put, $"/api/admin/categories/{id}", new { name, description = "Changed" }); // no-op
        await ApiJson.SendAsync(admin.Http, HttpMethod.Post, $"/api/admin/categories/{id}/deactivate");
        await ApiJson.SendAsync(admin.Http, HttpMethod.Post, $"/api/admin/categories/{id}/deactivate"); // no-op
        await ApiJson.SendAsync(admin.Http, HttpMethod.Post, $"/api/admin/categories/{id}/activate");

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var entries = await db.AuditLogs.AsNoTracking()
            .Where(a => a.EntityId == id.ToString())
            .OrderBy(a => a.Id)
            .ToListAsync();

        Assert.Equal(
            new[] { "CategoryCreated", "CategoryUpdated", "CategoryDeactivated", "CategoryActivated" },
            entries.Select(e => e.Action).ToArray());
        Assert.All(entries, e => Assert.Equal(admin.UserId, e.UserId));
        Assert.All(entries, e => Assert.Equal(nameof(ServiceCategory), e.EntityName));
        Assert.Null(entries[0].OldValues);
        Assert.Contains(name, entries[0].NewValues);
        Assert.Contains("Changed", entries[1].NewValues);
        Assert.DoesNotContain("Changed", entries[1].OldValues);
    }

    [Fact]
    public async Task Service_changes_are_audited_too()
    {
        using var admin = await _session.AdminAsync();
        var categoryId = await CreateCategoryAsync(admin.Http);
        var serviceId = await CreateServiceAsync(admin.Http, categoryId);
        await ApiJson.SendAsync(admin.Http, HttpMethod.Post, $"/api/admin/services/{serviceId}/deactivate");

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var actions = await db.AuditLogs.AsNoTracking()
            .Where(a => a.EntityId == serviceId.ToString())
            .OrderBy(a => a.Id)
            .Select(a => a.Action)
            .ToListAsync();

        Assert.Equal(new[] { "ServiceCreated", "ServiceDeactivated" }, actions);
    }
}