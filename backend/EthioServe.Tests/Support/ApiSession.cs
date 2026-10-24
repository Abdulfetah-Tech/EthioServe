using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using EthioServe.Domain.Common;
using EthioServe.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace EthioServe.Tests.Support;

/// <summary>A client that is already signed in as a freshly registered user.</summary>
public sealed class SignedInClient(HttpClient http, Guid userId, Guid? profileId = null) : IDisposable
{
    public HttpClient Http { get; } = http;

    public Guid UserId { get; } = userId;

    /// <summary>The provider or customer profile id, when the account has one.</summary>
    public Guid? ProfileId { get; } = profileId;

    public void Dispose() => Http.Dispose();
}

public sealed class ApiSession(ApiFactory factory)
{
    private const string Password = "Str0ngPassw0rd!";

    public HttpClient CreateAnonymousClient() => factory.CreateClient(new WebApplicationFactoryClientOptions
    {
        BaseAddress = new Uri("https://localhost"),
        HandleCookies = false,
        AllowAutoRedirect = false
    });

    public Task<SignedInClient> CustomerAsync() => SignInAsync();

    /// <summary>A customer account that was given the Provider ROLE but has no provider profile.</summary>
    public Task<SignedInClient> ProviderAsync() => SignInAsync(Roles.Provider);

    public Task<SignedInClient> AdminAsync() => SignInAsync(Roles.Admin);

    /// <summary>A real provider, registered through the public API, with an (incomplete) Unverified profile in Addis Ababa.</summary>
    public async Task<SignedInClient> RegisteredProviderAsync(string businessName = "Test Plumbing")
    {
        var client = CreateAnonymousClient();
        var (regionId, cityId) = await AddisAsync(client);

        using var register = await client.PostAsJsonAsync("/api/auth/register/provider", new
        {
            email = $"provider-{Guid.NewGuid():N}@test.local",
            password = Password,
            businessName,
            phoneNumber = "0911556677",
            regionId,
            cityId
        });
        register.EnsureSuccessStatusCode();

        var body = await register.Content.ReadFromJsonAsync<JsonElement>();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", body.GetProperty("accessToken").GetString());

        var user = body.GetProperty("user");

        return new SignedInClient(client, user.GetProperty("id").GetGuid(), user.GetProperty("providerId").GetGuid());
    }

    private static async Task<(int RegionId, int CityId)> AddisAsync(HttpClient client)
    {
        var regions = await client.GetFromJsonAsync<JsonElement>("/api/locations/regions");
        var regionId = regions.EnumerateArray()
            .Single(r => r.GetProperty("name").GetString() == "Addis Ababa").GetProperty("id").GetInt32();

        var cities = await client.GetFromJsonAsync<JsonElement>($"/api/locations/regions/{regionId}/cities");
        var cityId = cities.EnumerateArray()
            .Single(c => c.GetProperty("name").GetString() == "Addis Ababa").GetProperty("id").GetInt32();

        return (regionId, cityId);
    }

    private async Task<SignedInClient> SignInAsync(params string[] extraRoles)
    {
        var client = CreateAnonymousClient();
        var email = $"user-{Guid.NewGuid():N}@test.local";

        using var register = await client.PostAsJsonAsync("/api/auth/register/customer", new
        {
            email,
            password = Password,
            firstName = "Test",
            lastName = "User",
            phoneNumber = "0911223344"
        });
        register.EnsureSuccessStatusCode();

        if (extraRoles.Length > 0)
        {
            await using var scope = factory.Services.CreateAsyncScope();
            var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var user = await users.FindByEmailAsync(email);
            await users.AddToRolesAsync(user!, extraRoles);

            // A real admin is only an admin. Keeping the Customer role would let test admins pass customer-only checks.
            if (extraRoles.Contains(Roles.Admin))
            {
                await users.RemoveFromRoleAsync(user!, Roles.Customer);
            }
        }

        // Sign in again so the access token carries the extra roles.
        using var login = await client.PostAsJsonAsync("/api/auth/login", new { email, password = Password });
        login.EnsureSuccessStatusCode();

        var body = await login.Content.ReadFromJsonAsync<JsonElement>();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", body.GetProperty("accessToken").GetString());

        return new SignedInClient(client, body.GetProperty("user").GetProperty("id").GetGuid());
    }
}