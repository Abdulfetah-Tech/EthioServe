namespace EthioServe.Application.Abstractions;

/// <summary>Who the access token is for.</summary>
public sealed record TokenSubject(
    Guid UserId,
    string Email,
    IReadOnlyCollection<string> Roles,
    Guid? CustomerId = null,
    Guid? ProviderId = null);

public sealed record AccessToken(string Value, DateTimeOffset ExpiresAt);

/// <summary>The raw value is sent to the client once; only the hash is stored.</summary>
public sealed record NewRefreshToken(string Value, string Hash, DateTimeOffset ExpiresAt);

public interface ITokenService
{
    AccessToken CreateAccessToken(TokenSubject subject);

    NewRefreshToken CreateRefreshToken();

    /// <summary>A random, URL-safe, single-purpose token (for example a password reset link).</summary>
    string CreateOpaqueToken();

    /// <summary>SHA-256 of any opaque token. Used for refresh tokens and reset tokens.</summary>
    
    string HashRefreshToken(string token);

}