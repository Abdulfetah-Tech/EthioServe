namespace EthioServe.API.Security;

public static class RefreshTokenCookie
{
    public const string Name = "refresh_token";

    // Scoped to the auth routes, so the browser never sends it with ordinary API calls.
    private const string CookiePath = "/api/auth";

    public static void Set(HttpResponse response, string token, DateTimeOffset expiresAt) =>
        response.Cookies.Append(Name, token, new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Strict,
            Path = CookiePath,
            Expires = expiresAt,
            IsEssential = true
        });

    public static void Clear(HttpResponse response) =>
        response.Cookies.Delete(Name, new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Strict,
            Path = CookiePath
        });
}