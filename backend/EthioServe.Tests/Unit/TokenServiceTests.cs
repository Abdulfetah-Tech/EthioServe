using System.ComponentModel.DataAnnotations;
using EthioServe.Application.Abstractions;
using EthioServe.Application.Common;
using EthioServe.Infrastructure.Identity;
using EthioServe.Infrastructure.Time;
using EthioServe.Tests.Support;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using System.Security.Claims;

namespace EthioServe.Tests.Unit;

public sealed class TokenServiceTests
{
    private const string KeyA = "unit-test-signing-key-A-0123456789-abcdefghij";
    private const string KeyB = "unit-test-signing-key-B-0123456789-abcdefghij";

    private static JwtOptions NewOptions(string key = KeyA, string audience = "EthioServe.Web") => new()
    {
        Issuer = "EthioServe",
        Audience = audience,
        SigningKey = key,
        AccessTokenMinutes = 15,
        RefreshTokenDays = 7
    };

    private static JwtTokenService NewService(JwtOptions options, IClock? clock = null) =>
        new(Options.Create(options), clock ?? new SystemClock());

    private static TokenSubject Subject(params string[] roles) =>
        new(Guid.NewGuid(), "user@test.local", roles, CustomerId: Guid.NewGuid());

    private static Task<TokenValidationResult> ValidateAsync(string token, JwtOptions options) =>
        new JsonWebTokenHandler().ValidateTokenAsync(token, JwtTokenValidation.CreateParameters(options));

    [Fact]
    public async Task Access_token_validates_and_carries_the_expected_claims()
    {
        var options = NewOptions();
        var subject = Subject("Customer");

        var token = NewService(options).CreateAccessToken(subject);
        var result = await ValidateAsync(token.Value, options);

        Assert.True(result.IsValid, result.Exception?.Message);
        Assert.Equal(subject.UserId.ToString(), result.ClaimsIdentity.FindFirst(AppClaims.Subject)?.Value);
        Assert.Equal("user@test.local", result.ClaimsIdentity.FindFirst(AppClaims.Email)?.Value);
        Assert.Equal(subject.CustomerId.ToString(), result.ClaimsIdentity.FindFirst(AppClaims.CustomerId)?.Value);
        Assert.Null(result.ClaimsIdentity.FindFirst(AppClaims.ProviderId));
        var principal = new ClaimsPrincipal(result.ClaimsIdentity);
        Assert.True(principal.IsInRole("Customer"));
        Assert.False(principal.IsInRole("Admin"));
    }

    [Fact]
    public void Access_token_lifetime_follows_the_configuration()
    {
        var clock = new TestClock(new DateTimeOffset(2026, 10, 13, 8, 0, 0, TimeSpan.Zero));

        var token = NewService(NewOptions(), clock).CreateAccessToken(Subject("Customer"));

        Assert.Equal(clock.UtcNow.AddMinutes(15), token.ExpiresAt);
    }

    [Fact]
    public async Task Expired_token_is_rejected()
    {
        var options = NewOptions();
        var clock = new TestClock(DateTimeOffset.UtcNow.AddHours(-2));

        var token = NewService(options, clock).CreateAccessToken(Subject("Customer"));
        var result = await ValidateAsync(token.Value, options);

        Assert.False(result.IsValid);
        Assert.IsType<SecurityTokenExpiredException>(result.Exception);
    }

    [Fact]
    public async Task Token_signed_with_a_different_key_is_rejected()
    {
        var token = NewService(NewOptions(KeyA)).CreateAccessToken(Subject("Customer"));

        var result = await ValidateAsync(token.Value, NewOptions(KeyB));

        Assert.False(result.IsValid);
    }

    [Fact]
    public async Task Token_for_a_different_audience_is_rejected()
    {
        var token = NewService(NewOptions(audience: "SomeOtherApp")).CreateAccessToken(Subject("Customer"));

        var result = await ValidateAsync(token.Value, NewOptions());

        Assert.False(result.IsValid);
        Assert.IsType<SecurityTokenInvalidAudienceException>(result.Exception);
    }

    [Fact]
    public async Task Token_with_a_tampered_role_is_rejected()
    {
        var options = NewOptions();
        var token = NewService(options).CreateAccessToken(Subject("Customer")).Value;

        // An attacker edits the payload to promote themselves to Admin but cannot re-sign it.
        var parts = token.Split('.');
        var payload = Base64UrlEncoder.Decode(parts[1]);
        Assert.Contains("Customer", payload);
        parts[1] = Base64UrlEncoder.Encode(payload.Replace("Customer", "Admin"));

        var result = await ValidateAsync(string.Join('.', parts), options);

        Assert.False(result.IsValid);
    }

    [Fact]
    public async Task Unsigned_token_using_alg_none_is_rejected()
    {
        var header = Base64UrlEncoder.Encode("{\"alg\":\"none\",\"typ\":\"JWT\"}");
        var payload = Base64UrlEncoder.Encode(
            "{\"sub\":\"attacker\",\"role\":\"Admin\",\"iss\":\"EthioServe\",\"aud\":\"EthioServe.Web\",\"exp\":4102444800}");

        var result = await ValidateAsync($"{header}.{payload}.", NewOptions());

        Assert.False(result.IsValid);
    }

    [Fact]
    public void Refresh_tokens_are_random_long_and_stored_only_as_a_hash()
    {
        var clock = new TestClock(new DateTimeOffset(2026, 10, 13, 8, 0, 0, TimeSpan.Zero));
        var service = NewService(NewOptions(), clock);

        var first = service.CreateRefreshToken();
        var second = service.CreateRefreshToken();

        Assert.NotEqual(first.Value, second.Value);
        Assert.True(first.Value.Length >= 80);
        Assert.Equal(64, first.Hash.Length);
        Assert.NotEqual(first.Value, first.Hash);
        Assert.Equal(first.Hash, service.HashRefreshToken(first.Value));
        Assert.NotEqual(first.Hash, second.Hash);
        Assert.Equal(clock.UtcNow.AddDays(7), first.ExpiresAt);
    }

    [Fact]
    public void Valid_options_pass_validation()
    {
        var options = NewOptions();

        var valid = Validator.TryValidateObject(
            options, new ValidationContext(options), new List<ValidationResult>(), validateAllProperties: true);

        Assert.True(valid);
    }

    [Fact]
    public void Signing_key_shorter_than_32_characters_is_invalid()
    {
        var options = NewOptions(key: "too-short");
        var results = new List<ValidationResult>();

        var valid = Validator.TryValidateObject(options, new ValidationContext(options), results, validateAllProperties: true);

        Assert.False(valid);
        Assert.Contains(results, r => r.MemberNames.Contains(nameof(JwtOptions.SigningKey)));
    }
}