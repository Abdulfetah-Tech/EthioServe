namespace EthioServe.Application.Auth;

public sealed record RegisterCustomerRequest(
    string Email, string Password, string FirstName, string LastName, string PhoneNumber);

public sealed record RegisterProviderRequest(
    string Email, string Password, string BusinessName, string PhoneNumber, int RegionId, int CityId);

public sealed record LoginRequest(string Email, string Password);

public sealed record ForgotPasswordRequest(string Email);

public sealed record ResetPasswordRequest(string Token, string NewPassword);

public sealed record CurrentUserDto(
    Guid Id,
    string Email,
    string DisplayName,
    IReadOnlyList<string> Roles,
    Guid? CustomerId,
    Guid? ProviderId);

/// <summary>Result of register/login/refresh. The API sends the refresh token only as an httpOnly cookie, never in the body.</summary>
public sealed record AuthTokens(
    string AccessToken,
    DateTimeOffset AccessTokenExpiresAt,
    string RefreshToken,
    DateTimeOffset RefreshTokenExpiresAt,
    CurrentUserDto User);

public sealed record ClientInfo(string IpAddress, string UserAgent);