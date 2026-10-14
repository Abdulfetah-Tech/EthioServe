using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using EthioServe.API.Configuration;
using EthioServe.Infrastructure.Identity;
using EthioServe.Tests.Support;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace EthioServe.Tests.Integration;

[Collection(ApiCollection.Name)]
public sealed class AuthApiTests(ApiFactory factory)
{
    private const string Password = "Str0ngPassw0rd!";

    // https, because the refresh cookie is Secure. Cookies are handled by hand so every header can be asserted.
    private static WebApplicationFactoryClientOptions ClientOptions => new()
    {
        BaseAddress = new Uri("https://localhost"),
        HandleCookies = false,
        AllowAutoRedirect = false
    };

    private HttpClient NewClient() => factory.CreateClient(ClientOptions);

    private static string NewEmail() => $"user-{Guid.NewGuid():N}@test.local";

    private static object CustomerBody(string email, string password = Password) => new
    {
        email,
        password,
        firstName = "Abebe",
        lastName = "Kebede",
        phoneNumber = "0911223344"
    };

    private static async Task<JsonElement> ReadJsonAsync(HttpResponseMessage response) =>
        await response.Content.ReadFromJsonAsync<JsonElement>();

    private static string? SetCookieHeader(HttpResponseMessage response) =>
        response.Headers.TryGetValues("Set-Cookie", out var values)
            ? values.FirstOrDefault(v => v.StartsWith("refresh_token=", StringComparison.Ordinal))
            : null;

    private static string? CookieValue(HttpResponseMessage response) =>
        SetCookieHeader(response)?.Split(';')[0]["refresh_token=".Length..];

    private static HttpRequestMessage AjaxRequest(HttpMethod method, string url, string? cookie, bool csrfHeader = true)
    {
        var request = new HttpRequestMessage(method, url);

        if (csrfHeader)
        {
            request.Headers.Add("X-Requested-With", "XMLHttpRequest");
        }

        if (cookie is not null)
        {
            request.Headers.Add("Cookie", $"refresh_token={cookie}");
        }

        return request;
    }

    private static async Task<HttpStatusCode> StatusOfAsync(HttpClient client, string url, string? token = null)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);

        if (token is not null)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        using var response = await client.SendAsync(request);
        return response.StatusCode;
    }

    private static async Task<(string Email, string AccessToken, string RefreshCookie)> RegisterAsync(
        HttpClient client, string? email = null)
    {
        email ??= NewEmail();

        using var response = await client.PostAsJsonAsync("/api/auth/register/customer", CustomerBody(email));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var body = await ReadJsonAsync(response);
        return (email, body.GetProperty("accessToken").GetString()!, CookieValue(response)!);
    }

    private async Task<string> TokenWithRoleAsync(HttpClient client, string role)
    {
        var email = NewEmail();
        await RegisterAsync(client, email); // everyone registers as a Customer first

        if (role != "Customer")
        {
            await using var scope = factory.Services.CreateAsyncScope();
            var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var user = await users.FindByEmailAsync(email);
            await users.AddToRoleAsync(user!, role);
        }

        // Log in again so the new token carries the extra role.
        using var login = await client.PostAsJsonAsync("/api/auth/login", new { email, password = Password });
        return (await ReadJsonAsync(login)).GetProperty("accessToken").GetString()!;
    }

    [Fact]
    public async Task Registering_returns_a_token_and_sets_a_locked_down_refresh_cookie()
    {
        using var client = NewClient();

        using var response = await client.PostAsJsonAsync("/api/auth/register/customer", CustomerBody(NewEmail()));
        var raw = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.DoesNotContain("refreshToken", raw, StringComparison.OrdinalIgnoreCase);

        var cookie = SetCookieHeader(response);
        Assert.NotNull(cookie);
        Assert.Contains("httponly", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("secure", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=strict", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("path=/api/auth", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.False(string.IsNullOrEmpty(CookieValue(response)));
    }

    [Fact]
    public async Task Me_requires_a_valid_access_token()
    {
        using var client = NewClient();
        var (email, token, _) = await RegisterAsync(client);

        Assert.Equal(HttpStatusCode.Unauthorized, await StatusOfAsync(client, "/api/auth/me"));
        Assert.Equal(HttpStatusCode.Unauthorized, await StatusOfAsync(client, "/api/auth/me", "not.a.jwt"));

        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/auth/me");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(email, (await ReadJsonAsync(response)).GetProperty("email").GetString());
    }

    [Fact]
    public async Task Login_with_the_wrong_password_returns_401_problem_details()
    {
        using var client = NewClient();
        var (email, _, _) = await RegisterAsync(client);

        using var response = await client.PostAsJsonAsync("/api/auth/login", new { email, password = "Wr0ngPassword!" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Login_with_correct_credentials_returns_a_token_and_a_new_cookie()
    {
        using var client = NewClient();
        var (email, _, _) = await RegisterAsync(client);

        using var response = await client.PostAsJsonAsync("/api/auth/login", new { email, password = Password });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.False(string.IsNullOrEmpty((await ReadJsonAsync(response)).GetProperty("accessToken").GetString()));
        Assert.False(string.IsNullOrEmpty(CookieValue(response)));
    }

    [Fact]
    public async Task Refresh_rotates_the_cookie_and_returns_a_new_access_token()
    {
        using var client = NewClient();
        var (_, _, firstCookie) = await RegisterAsync(client);

        using var request = AjaxRequest(HttpMethod.Post, "/api/auth/refresh-token", firstCookie);
        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var secondCookie = CookieValue(response);
        Assert.NotNull(secondCookie);
        Assert.NotEqual(firstCookie, secondCookie);
        Assert.False(string.IsNullOrEmpty((await ReadJsonAsync(response)).GetProperty("accessToken").GetString()));
    }

    [Fact]
    public async Task Refresh_without_the_csrf_header_is_forbidden_and_does_not_burn_the_token()
    {
        using var client = NewClient();
        var (_, _, cookie) = await RegisterAsync(client);

        using var forbidden = AjaxRequest(HttpMethod.Post, "/api/auth/refresh-token", cookie, csrfHeader: false);
        using var forbiddenResponse = await client.SendAsync(forbidden);
        Assert.Equal(HttpStatusCode.Forbidden, forbiddenResponse.StatusCode);

        using var valid = AjaxRequest(HttpMethod.Post, "/api/auth/refresh-token", cookie);
        using var validResponse = await client.SendAsync(valid);
        Assert.Equal(HttpStatusCode.OK, validResponse.StatusCode);
    }

    [Fact]
    public async Task Refresh_without_a_cookie_returns_401()
    {
        using var client = NewClient();

        using var request = AjaxRequest(HttpMethod.Post, "/api/auth/refresh-token", cookie: null);
        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Replaying_an_old_refresh_cookie_kills_the_whole_session()
    {
        using var client = NewClient();
        var (_, _, oldCookie) = await RegisterAsync(client);

        using var rotate = AjaxRequest(HttpMethod.Post, "/api/auth/refresh-token", oldCookie);
        using var rotated = await client.SendAsync(rotate);
        var newCookie = CookieValue(rotated)!;

        using var replay = AjaxRequest(HttpMethod.Post, "/api/auth/refresh-token", oldCookie);
        using var replayed = await client.SendAsync(replay);
        Assert.Equal(HttpStatusCode.Unauthorized, replayed.StatusCode);

        using var latest = AjaxRequest(HttpMethod.Post, "/api/auth/refresh-token", newCookie);
        using var latestResponse = await client.SendAsync(latest);
        Assert.Equal(HttpStatusCode.Unauthorized, latestResponse.StatusCode);
    }

    [Fact]
    public async Task Logout_clears_the_cookie_and_ends_the_session()
    {
        using var client = NewClient();
        var (_, _, cookie) = await RegisterAsync(client);

        using var logout = AjaxRequest(HttpMethod.Post, "/api/auth/logout", cookie);
        using var logoutResponse = await client.SendAsync(logout);

        Assert.Equal(HttpStatusCode.NoContent, logoutResponse.StatusCode);
        var cleared = SetCookieHeader(logoutResponse);
        Assert.NotNull(cleared);
        Assert.Contains("1970", cleared);

        using var refresh = AjaxRequest(HttpMethod.Post, "/api/auth/refresh-token", cookie);
        using var refreshResponse = await client.SendAsync(refresh);
        Assert.Equal(HttpStatusCode.Unauthorized, refreshResponse.StatusCode);
    }

    [Fact]
    public async Task Weak_password_returns_400_with_a_password_error()
    {
        using var client = NewClient();

        using var response = await client.PostAsJsonAsync("/api/auth/register/customer", CustomerBody(NewEmail(), "weak"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var errors = (await ReadJsonAsync(response)).GetProperty("errors");
        Assert.Contains(errors.EnumerateObject(), p => string.Equals(p.Name, "Password", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Registering_the_same_email_twice_returns_409()
    {
        using var client = NewClient();
        var (email, _, _) = await RegisterAsync(client);

        using var response = await client.PostAsJsonAsync("/api/auth/register/customer", CustomerBody(email));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Theory]
    [InlineData("Customer", "customer", HttpStatusCode.OK)]
    [InlineData("Customer", "provider", HttpStatusCode.Forbidden)]
    [InlineData("Customer", "admin", HttpStatusCode.Forbidden)]
    [InlineData("Provider", "provider", HttpStatusCode.OK)]
    [InlineData("Provider", "admin", HttpStatusCode.Forbidden)]
    [InlineData("Admin", "admin", HttpStatusCode.OK)]
    public async Task Role_policies_allow_and_deny_as_expected(string role, string route, HttpStatusCode expected)
    {
        using var client = NewClient();
        var token = await TokenWithRoleAsync(client, role);

        Assert.Equal(expected, await StatusOfAsync(client, $"/test-support/secure/{route}", token));
    }

    [Theory]
    [InlineData("any")]
    [InlineData("admin")]
    [InlineData("provider")]
    [InlineData("customer")]
    [InlineData("unmarked")]
    public async Task Anonymous_requests_to_protected_routes_get_401(string route)
    {
        using var client = NewClient();

        Assert.Equal(HttpStatusCode.Unauthorized, await StatusOfAsync(client, $"/test-support/secure/{route}"));
    }

    [Fact]
    public async Task Explicitly_public_routes_stay_open()
    {
        using var client = NewClient();

        Assert.Equal(HttpStatusCode.OK, await StatusOfAsync(client, "/test-support/secure/public"));
    }

    [Fact]
    public async Task Auth_routes_are_rate_limited_per_client()
    {
        await using var limited = factory.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services =>
                services.Configure<RateLimitSettings>(settings => settings.AuthPermitsPerMinute = 3)));
        using var client = limited.CreateClient(ClientOptions);

        var statuses = new List<HttpStatusCode>();
        for (var attempt = 0; attempt < 4; attempt++)
        {
            using var response = await client.PostAsJsonAsync(
                "/api/auth/login", new { email = NewEmail(), password = "Wr0ngPassword!" });
            statuses.Add(response.StatusCode);
        }

        Assert.Equal(
            new[] { HttpStatusCode.Unauthorized, HttpStatusCode.Unauthorized, HttpStatusCode.Unauthorized, HttpStatusCode.TooManyRequests },
            statuses);
    }
}