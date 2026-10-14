using System.Security.Claims;
using EthioServe.Application.Auth;
using EthioServe.Application.Common;
using EthioServe.Application.Common.Exceptions;
using EthioServe.Domain.Enums;
using EthioServe.Infrastructure.Identity;
using EthioServe.Tests.Support;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.JsonWebTokens;

namespace EthioServe.Tests.Integration;

[Collection(DatabaseCollection.Name)]
public sealed class AuthServiceTests(DatabaseFixture fixture) : IAsyncLifetime
{
    private AuthHarness _auth = null!;

    public async Task InitializeAsync() => _auth = await AuthHarness.CreateAsync(fixture);

    public async Task DisposeAsync() => await _auth.DisposeAsync();

    private static string NewEmail() => $"user-{Guid.NewGuid():N}@test.local";

    private static RegisterCustomerRequest NewCustomer(string? email = null, string password = AuthHarness.StrongPassword) =>
        new(email ?? NewEmail(), password, "Abebe", "Kebede", "0911223344");

    private static RegisterProviderRequest NewProvider(int regionId, int cityId, string? email = null) =>
        new(email ?? NewEmail(), AuthHarness.StrongPassword, "Abebe Plumbing", "0911556677", regionId, cityId);

    private static ClientInfo Client => AuthHarness.Client;

    private Task<AuthTokens> RegisterCustomerAsync(string? email = null) =>
        _auth.Call(a => a.RegisterCustomerAsync(NewCustomer(email), Client));

    private Task<AuthTokens> RefreshAsync(string token) =>
        _auth.Call(a => a.RefreshAsync(token, Client));

    private Task<(int RegionId, int CityId)> AddisAsync() =>
        _auth.Db(async db =>
        {
            var city = await db.Cities.FirstAsync(c => c.Name == "Addis Ababa");
            return (city.RegionId, city.Id);
        });

    [Fact]
    public async Task Registering_a_customer_creates_account_role_profile_and_valid_tokens()
    {
        var request = NewCustomer();

        var result = await _auth.Call(a => a.RegisterCustomerAsync(request, Client));

        Assert.Equal(request.Email, result.User.Email);
        Assert.Equal(new[] { "Customer" }, result.User.Roles);
        Assert.NotNull(result.User.CustomerId);
        Assert.Null(result.User.ProviderId);
        Assert.Equal("Abebe Kebede", result.User.DisplayName);

        var validation = await new JsonWebTokenHandler()
            .ValidateTokenAsync(result.AccessToken, JwtTokenValidation.CreateParameters(_auth.Jwt));
        Assert.True(validation.IsValid, validation.Exception?.Message);

        var principal = new ClaimsPrincipal(validation.ClaimsIdentity);
        Assert.True(principal.IsInRole("Customer"));
        Assert.Equal(result.User.Id.ToString(), principal.FindFirst(AppClaims.Subject)?.Value);
        Assert.Equal(result.User.CustomerId.ToString(), principal.FindFirst(AppClaims.CustomerId)?.Value);
    }

    [Fact]
    public async Task Passwords_are_stored_hashed()
    {
        var request = NewCustomer();
        await _auth.Call(a => a.RegisterCustomerAsync(request, Client));

        var hash = await _auth.Db(db => db.Users.Where(u => u.Email == request.Email).Select(u => u.PasswordHash).SingleAsync());

        Assert.NotNull(hash);
        Assert.NotEqual(request.Password, hash);
    }

    [Fact]
    public async Task Duplicate_email_is_rejected_regardless_of_letter_case()
    {
        var email = NewEmail();
        await RegisterCustomerAsync(email);

        await Assert.ThrowsAsync<ConflictException>(() => RegisterCustomerAsync(email.ToUpperInvariant()));
    }

    [Fact]
    public async Task Weak_password_is_rejected_with_a_password_error()
    {
        var error = await Assert.ThrowsAsync<ValidationException>(
            () => _auth.Call(a => a.RegisterCustomerAsync(NewCustomer(password: "short"), Client)));

        Assert.Contains(error.Errors, e => e.PropertyName == "Password");
    }

    [Fact]
    public async Task Registering_a_provider_creates_an_unverified_provider_profile()
    {
        var (regionId, cityId) = await AddisAsync();

        var result = await _auth.Call(a => a.RegisterProviderAsync(NewProvider(regionId, cityId), Client));

        Assert.Equal(new[] { "Provider" }, result.User.Roles);
        Assert.NotNull(result.User.ProviderId);

        var status = await _auth.Db(db => db.ProviderProfiles
            .Where(p => p.Id == result.User.ProviderId)
            .Select(p => p.VerificationStatus)
            .SingleAsync());
        Assert.Equal(VerificationStatus.Unverified, status);
    }

    [Fact]
    public async Task Provider_with_an_unknown_city_is_rejected_and_no_account_is_left_behind()
    {
        var (regionId, _) = await AddisAsync();
        var email = NewEmail();

        await Assert.ThrowsAsync<ValidationException>(
            () => _auth.Call(a => a.RegisterProviderAsync(NewProvider(regionId, 999_999, email), Client)));

        var exists = await _auth.Db(db => db.Users.AnyAsync(u => u.Email == email));
        Assert.False(exists);
    }

    [Fact]
    public async Task Login_succeeds_regardless_of_email_case_and_records_the_login_time()
    {
        var email = NewEmail();
        await RegisterCustomerAsync(email);

        var result = await _auth.Call(a => a.LoginAsync(
            new LoginRequest(email.ToUpperInvariant(), AuthHarness.StrongPassword), Client));

        Assert.Equal(email, result.User.Email);

        var lastLogin = await _auth.Db(db => db.Users.Where(u => u.Email == email).Select(u => u.LastLoginAt).SingleAsync());
        Assert.NotNull(lastLogin);
    }

    [Fact]
    public async Task Wrong_password_and_unknown_email_give_the_same_error()
    {
        var email = NewEmail();
        await RegisterCustomerAsync(email);

        var wrongPassword = await Assert.ThrowsAsync<UnauthorizedException>(
            () => _auth.Call(a => a.LoginAsync(new LoginRequest(email, "Wr0ngPassword!"), Client)));
        var unknownEmail = await Assert.ThrowsAsync<UnauthorizedException>(
            () => _auth.Call(a => a.LoginAsync(new LoginRequest(NewEmail(), "Wr0ngPassword!"), Client)));

        Assert.Equal(wrongPassword.Message, unknownEmail.Message);
    }

    [Fact]
    public async Task Five_wrong_passwords_lock_the_account_even_for_the_correct_password()
    {
        var email = NewEmail();
        await RegisterCustomerAsync(email);

        for (var attempt = 0; attempt < 5; attempt++)
        {
            await Assert.ThrowsAsync<UnauthorizedException>(
                () => _auth.Call(a => a.LoginAsync(new LoginRequest(email, "Wr0ngPassword!"), Client)));
        }

        var locked = await Assert.ThrowsAsync<UnauthorizedException>(
            () => _auth.Call(a => a.LoginAsync(new LoginRequest(email, AuthHarness.StrongPassword), Client)));

        Assert.Contains("locked", locked.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Suspended_user_cannot_log_in()
    {
        var email = NewEmail();
        await RegisterCustomerAsync(email);
        await _auth.DbVoid(async db =>
        {
            var user = await db.Users.SingleAsync(u => u.Email == email);
            user.Suspend();
            await db.SaveChangesAsync();
        });

        var error = await Assert.ThrowsAsync<UnauthorizedException>(
            () => _auth.Call(a => a.LoginAsync(new LoginRequest(email, AuthHarness.StrongPassword), Client)));

        Assert.Contains("suspended", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Refresh_rotates_the_token_and_revokes_the_old_one()
    {
        var first = await RegisterCustomerAsync();

        var second = await RefreshAsync(first.RefreshToken);

        Assert.NotEqual(first.RefreshToken, second.RefreshToken);
        Assert.Equal(first.User.Id, second.User.Id);

        var oldHash = _auth.Hash(first.RefreshToken);
        var newHash = _auth.Hash(second.RefreshToken);
        var rows = await _auth.Db(db => db.RefreshTokens
            .AsNoTracking()
            .Where(t => t.TokenHash == oldHash || t.TokenHash == newHash)
            .ToListAsync());

        var oldRow = rows.Single(t => t.TokenHash == oldHash);
        var newRow = rows.Single(t => t.TokenHash == newHash);
        Assert.True(oldRow.IsRevoked);
        Assert.Equal(newHash, oldRow.ReplacedByTokenHash);
        Assert.False(newRow.IsRevoked);
        Assert.Equal(oldRow.FamilyId, newRow.FamilyId);
    }

    [Fact]
    public async Task Replaying_a_used_refresh_token_revokes_the_whole_session()
    {
        var first = await RegisterCustomerAsync();
        var second = await RefreshAsync(first.RefreshToken);

        // The thief (or the owner's stale tab) replays the old token...
        await Assert.ThrowsAsync<UnauthorizedException>(() => RefreshAsync(first.RefreshToken));

        // ...and now even the newest, previously valid token is dead.
        await Assert.ThrowsAsync<UnauthorizedException>(() => RefreshAsync(second.RefreshToken));
    }

    [Fact]
    public async Task Simultaneous_refreshes_with_the_same_token_allow_only_one_winner()
    {
        var login = await RegisterCustomerAsync();

        var outcomes = await Task.WhenAll(Enumerable.Range(0, 5).Select(async _ =>
        {
            try
            {
                await RefreshAsync(login.RefreshToken);
                return true;
            }
            catch (UnauthorizedException)
            {
                return false;
            }
        }));

        Assert.Equal(1, outcomes.Count(succeeded => succeeded));
    }

    [Fact]
    public async Task Expired_refresh_token_is_rejected()
    {
        var login = await RegisterCustomerAsync();

        _auth.Clock.UtcNow = _auth.Clock.UtcNow.AddDays(8);

        await Assert.ThrowsAsync<UnauthorizedException>(() => RefreshAsync(login.RefreshToken));
    }

    [Fact]
    public async Task Logout_ends_the_session()
    {
        var login = await RegisterCustomerAsync();

        await _auth.CallVoid(a => a.LogoutAsync(login.RefreshToken));

        await Assert.ThrowsAsync<UnauthorizedException>(() => RefreshAsync(login.RefreshToken));
    }

    [Fact]
    public async Task Logout_with_an_unknown_or_missing_token_is_harmless()
    {
        await _auth.CallVoid(a => a.LogoutAsync("not-a-real-token"));
        await _auth.CallVoid(a => a.LogoutAsync(null));
    }

    [Fact]
    public async Task Refresh_tokens_are_stored_only_as_hashes()
    {
        var login = await RegisterCustomerAsync();

        var stored = await _auth.Db(db => db.RefreshTokens
            .Where(t => t.UserId == login.User.Id)
            .Select(t => t.TokenHash)
            .ToListAsync());

        Assert.DoesNotContain(login.RefreshToken, stored);
        Assert.Contains(_auth.Hash(login.RefreshToken), stored);
    }

    [Fact]
    public async Task Current_user_returns_roles_and_profile_ids()
    {
        var login = await RegisterCustomerAsync();

        var me = await _auth.Call(a => a.GetCurrentUserAsync(login.User.Id));

        Assert.Equal(login.User.Email, me.Email);
        Assert.Equal(new[] { "Customer" }, me.Roles);
        Assert.Equal(login.User.CustomerId, me.CustomerId);
    }

    [Fact]
    public async Task Current_user_is_unavailable_once_suspended()
    {
        var login = await RegisterCustomerAsync();
        await _auth.DbVoid(async db =>
        {
            var user = await db.Users.SingleAsync(u => u.Id == login.User.Id);
            user.Suspend();
            await db.SaveChangesAsync();
        });

        await Assert.ThrowsAsync<UnauthorizedException>(() => _auth.Call(a => a.GetCurrentUserAsync(login.User.Id)));
    }
}