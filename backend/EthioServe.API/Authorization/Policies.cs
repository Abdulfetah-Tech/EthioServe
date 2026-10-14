namespace EthioServe.API.Authorization;

public static class Policies
{
    public const string AdminOnly = "AdminOnly";
    public const string ProviderOnly = "ProviderOnly";
    public const string CustomerOnly = "CustomerOnly";
}