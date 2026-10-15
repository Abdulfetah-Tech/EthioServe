using System.Net;
using System.Text.Json;
using EthioServe.Tests.Support;

namespace EthioServe.Tests.Integration;

[Collection(ApiCollection.Name)]
public sealed class LocationApiTests(ApiFactory factory)
{
    private static int IdOf(JsonElement array, string name) =>
        array.EnumerateArray().Single(item => item.GetProperty("name").GetString() == name).GetProperty("id").GetInt32();

    [Fact]
    public async Task Regions_are_public_and_include_addis_ababa()
    {
        using var client = factory.CreateClient(); // no token: this data feeds the sign-up form

        var (status, body, _) = await ApiJson.GetAsync(client, "/api/locations/regions");

        Assert.Equal(HttpStatusCode.OK, status);
        var names = ApiJson.Names(body);
        Assert.Contains("Addis Ababa", names);
        Assert.True(names.Count >= 7);
    }

    [Fact]
    public async Task Cities_of_a_region_are_listed()
    {
        using var client = factory.CreateClient();
        var (_, regions, _) = await ApiJson.GetAsync(client, "/api/locations/regions");

        var (status, cities, _) = await ApiJson.GetAsync(client, $"/api/locations/regions/{IdOf(regions, "Oromia")}/cities");

        Assert.Equal(HttpStatusCode.OK, status);
        Assert.Contains("Adama", ApiJson.Names(cities));
        Assert.DoesNotContain("Addis Ababa", ApiJson.Names(cities));
    }

    [Fact]
    public async Task Addis_ababa_has_eleven_sub_cities()
    {
        using var client = factory.CreateClient();
        var (_, regions, _) = await ApiJson.GetAsync(client, "/api/locations/regions");
        var (_, cities, _) = await ApiJson.GetAsync(client, $"/api/locations/regions/{IdOf(regions, "Addis Ababa")}/cities");

        var (status, subCities, _) = await ApiJson.GetAsync(
            client, $"/api/locations/cities/{IdOf(cities, "Addis Ababa")}/sub-cities");

        Assert.Equal(HttpStatusCode.OK, status);
        Assert.Equal(11, subCities.GetArrayLength());
        Assert.Contains("Bole", ApiJson.Names(subCities));
    }

    [Theory]
    [InlineData("/api/locations/regions/999999/cities")]
    [InlineData("/api/locations/cities/999999/sub-cities")]
    public async Task Unknown_parents_return_404_problem_details(string url)
    {
        using var client = factory.CreateClient();

        var (status, _, contentType) = await ApiJson.GetAsync(client, url);

        Assert.Equal(HttpStatusCode.NotFound, status);
        Assert.Equal("application/problem+json", contentType);
    }
}