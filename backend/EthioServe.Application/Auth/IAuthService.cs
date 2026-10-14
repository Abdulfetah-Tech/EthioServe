namespace EthioServe.Application.Auth;

public interface IAuthService
{
    Task<AuthTokens> RegisterCustomerAsync(RegisterCustomerRequest request, ClientInfo client, CancellationToken ct = default);

    Task<AuthTokens> RegisterProviderAsync(RegisterProviderRequest request, ClientInfo client, CancellationToken ct = default);

    Task<AuthTokens> LoginAsync(LoginRequest request, ClientInfo client, CancellationToken ct = default);

    Task<AuthTokens> RefreshAsync(string refreshToken, ClientInfo client, CancellationToken ct = default);

    /// <summary>Ends the session family of the given refresh token. Safe to call with an unknown or missing token.</summary>
    Task LogoutAsync(string? refreshToken, CancellationToken ct = default);

    Task<CurrentUserDto> GetCurrentUserAsync(Guid userId, CancellationToken ct = default);
}