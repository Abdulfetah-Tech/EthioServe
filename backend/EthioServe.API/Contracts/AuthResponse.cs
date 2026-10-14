using EthioServe.Application.Auth;

namespace EthioServe.API.Contracts;

/// <summary>What the browser receives. The refresh token travels only in an httpOnly cookie.</summary>
public sealed record AuthResponse(string AccessToken, DateTimeOffset AccessTokenExpiresAt, CurrentUserDto User);