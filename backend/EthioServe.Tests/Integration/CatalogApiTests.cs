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
public sealed class CatalogApiTests(ApiFactory factory)
{
    private static Guid IdOf(JsonElement array, string name) =>
        array.EnumerateArray().Single(item => item.GetProperty("name").GetString() == name).GetProperty("id").GetGuid();

    private async Task<Guid> PlumbingIdAsync(HttpClient client)
    {
        var (_, categories, _) = await ApiJson.GetAsync(client, "/api/categories");
        return IdOf(categories, "Plumbing");
    }

    // A category (with one service) that exists in the database but is switched off.
    private async Task<(Guid CategoryId, Guid ServiceId)> CreateHiddenCategoryAsync()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var category = new ServiceCategory($"Hidden-{Guid.NewGuid():N}");
        db.ServiceCategories.Add(category);
        await db.SaveChangesAsync();

        var service = new Service(category.Id, "Hidden service", PricingType.Fixed, 10m);
        db.Services.Add(service);
        await db.SaveChangesAsync();

        await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE service_categories SET is_active = false WHERE id = {category.Id}");

        return (category.Id, service.Id);
    }

    [Fact]
    public async Task Categories_are_public_and_carry_their_service_count()
    {
        using var client = factory.CreateClient();

        var (status, body, _) = await ApiJson.GetAsync(client, "/api/categories");

        Assert.Equal(HttpStatusCode.OK, status);
        Assert.True(body.GetArrayLength() >= 9);
        var plumbing = body.EnumerateArray().Single(c => c.GetProperty("name").GetString() == "Plumbing");
        Assert.Equal(4, plumbing.GetProperty("serviceCount").GetInt32());
    }

    [Fact]
    public async Task A_single_category_can_be_read_and_an_unknown_one_returns_404()
    {
        using var client = factory.CreateClient();
        var plumbingId = await PlumbingIdAsync(client);

        var (found, body, _) = await ApiJson.GetAsync(client, $"/api/categories/{plumbingId}");
        var (missing, _, contentType) = await ApiJson.GetAsync(client, $"/api/categories/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.OK, found);
        Assert.Equal("Plumbing", body.GetProperty("name").GetString());
        Assert.Equal(HttpStatusCode.NotFound, missing);
        Assert.Equal("application/problem+json", contentType);
    }

    [Fact]
    public async Task Inactive_categories_and_their_services_are_hidden_from_the_public()
    {
        var (categoryId, serviceId) = await CreateHiddenCategoryAsync();
        using var client = factory.CreateClient();

        var (_, categories, _) = await ApiJson.GetAsync(client, "/api/categories");
        var (categoryStatus, _, _) = await ApiJson.GetAsync(client, $"/api/categories/{categoryId}");
        var (serviceStatus, _, _) = await ApiJson.GetAsync(client, $"/api/services/{serviceId}");
        var (_, services, _) = await ApiJson.GetAsync(client, $"/api/services?categoryId={categoryId}");

        Assert.DoesNotContain(ApiJson.Names(categories), name => name!.StartsWith("Hidden-", StringComparison.Ordinal));
        Assert.Equal(HttpStatusCode.NotFound, categoryStatus);
        Assert.Equal(HttpStatusCode.NotFound, serviceStatus);
        Assert.Equal(0, services.GetProperty("totalCount").GetInt32());
    }

    [Fact]
    public async Task Services_can_be_filtered_by_category()
    {
        using var client = factory.CreateClient();
        var plumbingId = await PlumbingIdAsync(client);

        var (status, body, _) = await ApiJson.GetAsync(client, $"/api/services?categoryId={plumbingId}");

        Assert.Equal(HttpStatusCode.OK, status);
        Assert.Equal(4, body.GetProperty("totalCount").GetInt32());
        Assert.All(body.GetProperty("items").EnumerateArray(), item =>
        {
            Assert.Equal(plumbingId, item.GetProperty("categoryId").GetGuid());
            Assert.Equal("Plumbing", item.GetProperty("categoryName").GetString());
        });
    }

    [Theory]
    [InlineData("pipe")]
    [InlineData("PIPE")]
    [InlineData("  Pipe  ")]
    public async Task Search_ignores_case_and_surrounding_spaces(string term)
    {
        using var client = factory.CreateClient();

        var (_, body, _) = await ApiJson.GetAsync(client, $"/api/services?search={Uri.EscapeDataString(term)}");

        var names = body.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("name").GetString()).ToList();
        Assert.Contains("Pipe repair", names);
    }

    [Fact]
    public async Task Search_treats_wildcard_characters_literally()
    {
        using var client = factory.CreateClient();

        var (_, body, _) = await ApiJson.GetAsync(client, "/api/services?search=%25");

        Assert.Equal(0, body.GetProperty("totalCount").GetInt32());
    }

    [Fact]
    public async Task Services_are_paged_with_complete_metadata()
    {
        using var client = factory.CreateClient();
        var plumbingId = await PlumbingIdAsync(client);

        var (_, first, _) = await ApiJson.GetAsync(client, $"/api/services?categoryId={plumbingId}&page=1&pageSize=2");
        var (_, second, _) = await ApiJson.GetAsync(client, $"/api/services?categoryId={plumbingId}&page=2&pageSize=2");
        var (_, beyond, _) = await ApiJson.GetAsync(client, $"/api/services?categoryId={plumbingId}&page=3&pageSize=2");

        Assert.Equal(2, first.GetProperty("items").GetArrayLength());
        Assert.Equal(4, first.GetProperty("totalCount").GetInt32());
        Assert.Equal(2, first.GetProperty("totalPages").GetInt32());
        Assert.Equal(1, first.GetProperty("page").GetInt32());
        Assert.Equal(2, first.GetProperty("pageSize").GetInt32());

        var firstNames = ApiJson.Names(first.GetProperty("items"));
        var secondNames = ApiJson.Names(second.GetProperty("items"));
        Assert.Empty(firstNames.Intersect(secondNames));
        Assert.Equal(0, beyond.GetProperty("items").GetArrayLength());
    }

    [Fact]
    public async Task Oversized_page_sizes_are_clamped()
    {
        using var client = factory.CreateClient();

        var (_, body, _) = await ApiJson.GetAsync(client, "/api/services?pageSize=1000");

        Assert.Equal(50, body.GetProperty("pageSize").GetInt32());
    }

    [Fact]
    public async Task Pricing_type_is_returned_as_readable_text()
    {
        using var client = factory.CreateClient();

        var (_, body, _) = await ApiJson.GetAsync(client, "/api/services?search=pipe%20repair");

        var service = body.GetProperty("items").EnumerateArray().Single();
        Assert.Equal("StartingFrom", service.GetProperty("pricingType").GetString());
        Assert.Equal(300m, service.GetProperty("basePrice").GetDecimal());
    }

    [Fact]
    public async Task A_single_service_can_be_read_and_an_unknown_one_returns_404()
    {
        using var client = factory.CreateClient();
        var (_, list, _) = await ApiJson.GetAsync(client, "/api/services?search=pipe%20repair");
        var id = list.GetProperty("items").EnumerateArray().Single().GetProperty("id").GetGuid();

        var (found, body, _) = await ApiJson.GetAsync(client, $"/api/services/{id}");
        var (missing, _, _) = await ApiJson.GetAsync(client, $"/api/services/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.OK, found);
        Assert.Equal("Pipe repair", body.GetProperty("name").GetString());
        Assert.Equal(HttpStatusCode.NotFound, missing);
    }
}