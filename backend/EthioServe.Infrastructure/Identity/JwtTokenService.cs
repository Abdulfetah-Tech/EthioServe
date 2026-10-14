using System.Security.Cryptography;
using System.Text;
using EthioServe.Application.Abstractions;
using EthioServe.Application.Common;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace EthioServe.Infrastructure.Identity;

public sealed class JwtTokenService : ITokenService
{
    private readonly JwtOptions _options;
    private readonly IClock _clock;
    private readonly JsonWebTokenHandler _handler = new();
    private readonly SigningCredentials _credentials;

    public JwtTokenService(IOptions<JwtOptions> options, IClock clock)
    {
        _options = options.Value;
        _clock = clock;
        _credentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.SigningKey)),
            SecurityAlgorithms.HmacSha256);
    }

    public AccessToken CreateAccessToken(TokenSubject subject)
    {
        var now = _clock.UtcNow;
        var expiresAt = now.AddMinutes(_options.AccessTokenMinutes);

        var claims = new Dictionary<string, object>
        {
            [AppClaims.Subject] = subject.UserId.ToString(),
            [AppClaims.Email] = subject.Email,
            [AppClaims.TokenId] = Guid.NewGuid().ToString(),
            [AppClaims.Role] = subject.Roles.ToArray()
        };

        if (subject.CustomerId is { } customerId)
        {
            claims[AppClaims.CustomerId] = customerId.ToString();
        }

        if (subject.ProviderId is { } providerId)
        {
            claims[AppClaims.ProviderId] = providerId.ToString();
        }

        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = _options.Issuer,
            Audience = _options.Audience,
            IssuedAt = now.UtcDateTime,
            NotBefore = now.UtcDateTime,
            Expires = expiresAt.UtcDateTime,
            Claims = claims,
            SigningCredentials = _credentials
        };

        return new AccessToken(_handler.CreateToken(descriptor), expiresAt);
    }

    public NewRefreshToken CreateRefreshToken()
    {
        var token = Base64UrlEncoder.Encode(RandomNumberGenerator.GetBytes(64));

        return new NewRefreshToken(token, HashRefreshToken(token), _clock.UtcNow.AddDays(_options.RefreshTokenDays));
    }

    // SHA-256 is enough here because the token is 512 bits of randomness, not a human-chosen password.
    public string CreateOpaqueToken() => Base64UrlEncoder.Encode(RandomNumberGenerator.GetBytes(32));
    public string HashRefreshToken(string token) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}