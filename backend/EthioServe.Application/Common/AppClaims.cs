namespace EthioServe.Application.Common;

/// <summary>Claim names used in our access tokens. One place, so no magic strings anywhere else.</summary>
public static class AppClaims
{
    public const string Subject = "sub";
    public const string Email = "email";
    public const string Role = "role";
    public const string TokenId = "jti";
    public const string CustomerId = "customer_id";
    public const string ProviderId = "provider_id";
}