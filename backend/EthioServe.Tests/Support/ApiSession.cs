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
public sealed class SignedInClient(HttpClient http, Guid userId) : IDisposable
{
    public HttpClient Http { get; } = http;

    public Guid UserId { get; } = userId;

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

    public Task<SignedInClient> ProviderAsync() => SignInAsync(Roles.Provider);

    public Task<SignedInClient> AdminAsync() => SignInAsync(Roles.Admin);

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