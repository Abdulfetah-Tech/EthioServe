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

    string HashRefreshToken(string token);
}