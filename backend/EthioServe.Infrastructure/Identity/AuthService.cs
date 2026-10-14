using EthioServe.Application.Abstractions;
using EthioServe.Application.Auth;
using EthioServe.Application.Common.Exceptions;
using EthioServe.Domain.Common;
using EthioServe.Domain.Entities;
using EthioServe.Infrastructure.Persistence;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace EthioServe.Infrastructure.Identity;

public sealed class AuthService(
    UserManager<ApplicationUser> userManager,
    AppDbContext db,
    ITokenService tokens,
    IClock clock,
    IValidator<RegisterCustomerRequest> customerValidator,
    IValidator<RegisterProviderRequest> providerValidator,
    IValidator<LoginRequest> loginValidator) : IAuthService
{
    private const string InvalidCredentials = "Invalid email or password.";
    private const string LockedOut = "Too many failed attempts. The account is temporarily locked; try again later.";
    private const string Suspended = "This account has been suspended.";
    private const string InvalidRefreshToken = "Invalid refresh token.";
    private const string EmailTaken = "An account with this email already exists.";

    public async Task<AuthTokens> RegisterCustomerAsync(
        RegisterCustomerRequest request, ClientInfo client, CancellationToken ct = default)
    {
        await customerValidator.ValidateAndThrowAsync(request, ct);

        // One transaction: if anything below fails, no half-created account is left behind.
        await using var transaction = await db.Database.BeginTransactionAsync(ct);

        var user = await CreateUserAsync(request.Email, request.Password, request.PhoneNumber, Roles.Customer);
        db.CustomerProfiles.Add(new CustomerProfile(user.Id, request.FirstName, request.LastName, request.PhoneNumber));
        await db.SaveChangesAsync(ct);

        var result = await IssueTokensAsync(user, client, Guid.NewGuid(), ct);
        await transaction.CommitAsync(ct);

        return result;
    }

    public async Task<AuthTokens> RegisterProviderAsync(
        RegisterProviderRequest request, ClientInfo client, CancellationToken ct = default)
    {
        await providerValidator.ValidateAndThrowAsync(request, ct);

        var cityMatchesRegion = await db.Cities.AnyAsync(c => c.Id == request.CityId && c.RegionId == request.RegionId, ct);
        if (!cityMatchesRegion)
        {
            throw new ValidationException(new[]
            {
                new ValidationFailure(nameof(request.CityId), "The selected city does not belong to the selected region.")
            });
        }

        await using var transaction = await db.Database.BeginTransactionAsync(ct);

        var user = await CreateUserAsync(request.Email, request.Password, request.PhoneNumber, Roles.Provider);
        db.ProviderProfiles.Add(new ProviderProfile(
            user.Id, request.BusinessName, request.PhoneNumber, request.RegionId, request.CityId));
        await db.SaveChangesAsync(ct);

        var result = await IssueTokensAsync(user, client, Guid.NewGuid(), ct);
        await transaction.CommitAsync(ct);

        return result;
    }

    public async Task<AuthTokens> LoginAsync(LoginRequest request, ClientInfo client, CancellationToken ct = default)
    {
        await loginValidator.ValidateAndThrowAsync(request, ct);

        var user = await userManager.FindByEmailAsync(request.Email.Trim());
        if (user is null)
        {
            // Hash anyway, so an unknown email takes as long as a wrong password (no timing side channel).
            userManager.PasswordHasher.HashPassword(new ApplicationUser(), request.Password);
            throw new UnauthorizedException(InvalidCredentials);
        }

        if (await userManager.IsLockedOutAsync(user))
        {
            throw new UnauthorizedException(LockedOut);
        }

        if (!await userManager.CheckPasswordAsync(user, request.Password))
        {
            await userManager.AccessFailedAsync(user); // counts the failure and locks the account at the limit
            throw new UnauthorizedException(InvalidCredentials);
        }

        await userManager.ResetAccessFailedCountAsync(user);

        // Checked only after the password is right, so only the real owner learns the account is suspended.
        if (user.IsSuspended)
        {
            throw new UnauthorizedException(Suspended);
        }

        user.RecordLogin(clock.UtcNow);
        await userManager.UpdateAsync(user);

        return await IssueTokensAsync(user, client, Guid.NewGuid(), ct);
    }

    public async Task<AuthTokens> RefreshAsync(string refreshToken, ClientInfo client, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(refreshToken))
        {
            throw new UnauthorizedException(InvalidRefreshToken);
        }

        var now = clock.UtcNow;
        var hash = tokens.HashRefreshToken(refreshToken);
        var stored = await db.RefreshTokens.AsNoTracking().SingleOrDefaultAsync(t => t.TokenHash == hash, ct);

        if (stored is null)
        {
            throw new UnauthorizedException(InvalidRefreshToken);
        }

        if (stored.IsRevoked)
        {
            // An already-used or revoked token is being replayed: assume it was stolen and end the whole session family.
            await RevokeFamilyAsync(stored.FamilyId, now, ct);
            throw new UnauthorizedException(InvalidRefreshToken);
        }

        if (stored.IsExpired(now))
        {
            throw new UnauthorizedException(InvalidRefreshToken);
        }

        var user = await userManager.FindByIdAsync(stored.UserId.ToString());
        if (user is null || user.IsSuspended)
        {
            await RevokeFamilyAsync(stored.FamilyId, now, ct);
            throw new UnauthorizedException(InvalidRefreshToken);
        }

        await using var transaction = await db.Database.BeginTransactionAsync(ct);

        var next = tokens.CreateRefreshToken();

        // Atomic claim: only one concurrent caller can change revoked_at from NULL, so two simultaneous
        // refreshes with the same token can never both succeed.
        var claimed = await db.RefreshTokens
            .Where(t => t.Id == stored.Id && t.RevokedAt == null)
            .ExecuteUpdateAsync(
                s => s.SetProperty(t => t.RevokedAt, (DateTimeOffset?)now)
                      .SetProperty(t => t.ReplacedByTokenHash, next.Hash),
                ct);

        if (claimed == 0)
        {
            await transaction.RollbackAsync(ct);
            await RevokeFamilyAsync(stored.FamilyId, now, ct);
            throw new UnauthorizedException(InvalidRefreshToken);
        }

        db.RefreshTokens.Add(new RefreshToken(user.Id, stored.FamilyId, next.Hash, next.ExpiresAt, client.IpAddress));
        await db.SaveChangesAsync(ct);

        var result = await BuildResultAsync(user, next, ct);
        await transaction.CommitAsync(ct);

        return result;
    }

    public async Task LogoutAsync(string? refreshToken, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(refreshToken))
        {
            return;
        }

        var hash = tokens.HashRefreshToken(refreshToken);
        var familyId = await db.RefreshTokens.AsNoTracking()
            .Where(t => t.TokenHash == hash)
            .Select(t => (Guid?)t.FamilyId)
            .FirstOrDefaultAsync(ct);

        if (familyId is { } id)
        {
            await RevokeFamilyAsync(id, clock.UtcNow, ct);
        }
    }

    public async Task<CurrentUserDto> GetCurrentUserAsync(Guid userId, CancellationToken ct = default)
    {
        var user = await userManager.FindByIdAsync(userId.ToString());

        if (user is null)
        {
            throw new UnauthorizedException(InvalidCredentials);
        }

        if (user.IsSuspended)
        {
            throw new UnauthorizedException(Suspended);
        }

        return await BuildCurrentUserAsync(user, ct);
    }

    private async Task<ApplicationUser> CreateUserAsync(string email, string password, string phoneNumber, string role)
    {
        var trimmedEmail = email.Trim();
        var user = new ApplicationUser { UserName = trimmedEmail, Email = trimmedEmail, PhoneNumber = phoneNumber };

        IdentityResult created;
        try
        {
            created = await userManager.CreateAsync(user, password);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            // Two registrations with the same email raced; the unique index in the database decided the winner.
            throw new ConflictException(EmailTaken);
        }

        if (!created.Succeeded)
        {
            throw ToException(created);
        }

        var roleResult = await userManager.AddToRoleAsync(user, role);
        if (!roleResult.Succeeded)
        {
            // A missing role is a deployment/seed problem, not the caller's fault.
            throw new InvalidOperationException(
                $"Could not assign role '{role}': {string.Join("; ", roleResult.Errors.Select(e => e.Description))}");
        }

        return user;
    }

    private static Exception ToException(IdentityResult result)
    {
        if (result.Errors.Any(e => e.Code is "DuplicateEmail" or "DuplicateUserName"))
        {
            return new ConflictException(EmailTaken);
        }

        var failures = result.Errors.Select(e => new ValidationFailure(
            e.Code.StartsWith("Password", StringComparison.Ordinal) ? "Password" : "Email", e.Description));

        return new ValidationException(failures);
    }

    private async Task<AuthTokens> IssueTokensAsync(
        ApplicationUser user, ClientInfo client, Guid familyId, CancellationToken ct)
    {
        var refresh = tokens.CreateRefreshToken();
        db.RefreshTokens.Add(new RefreshToken(user.Id, familyId, refresh.Hash, refresh.ExpiresAt, client.IpAddress));
        await db.SaveChangesAsync(ct);

        return await BuildResultAsync(user, refresh, ct);
    }

    private async Task<AuthTokens> BuildResultAsync(ApplicationUser user, NewRefreshToken refresh, CancellationToken ct)
    {
        var current = await BuildCurrentUserAsync(user, ct);
        var access = tokens.CreateAccessToken(
            new TokenSubject(user.Id, current.Email, current.Roles, current.CustomerId, current.ProviderId));

        return new AuthTokens(access.Value, access.ExpiresAt, refresh.Value, refresh.ExpiresAt, current);
    }

    private async Task<CurrentUserDto> BuildCurrentUserAsync(ApplicationUser user, CancellationToken ct)
    {
        var roles = (await userManager.GetRolesAsync(user)).ToList();

        var customer = await db.CustomerProfiles.AsNoTracking()
            .Where(p => p.UserId == user.Id)
            .Select(p => new { p.Id, Name = p.FirstName + " " + p.LastName })
            .FirstOrDefaultAsync(ct);

        var provider = await db.ProviderProfiles.AsNoTracking()
            .Where(p => p.UserId == user.Id)
            .Select(p => new { p.Id, Name = p.BusinessName })
            .FirstOrDefaultAsync(ct);

        var email = user.Email!;
        var displayName = customer?.Name ?? provider?.Name ?? email;

        return new CurrentUserDto(user.Id, email, displayName, roles, customer?.Id, provider?.Id);
    }

    private Task<int> RevokeFamilyAsync(Guid familyId, DateTimeOffset now, CancellationToken ct) =>
        db.RefreshTokens
            .Where(t => t.FamilyId == familyId && t.RevokedAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.RevokedAt, (DateTimeOffset?)now), ct);
}