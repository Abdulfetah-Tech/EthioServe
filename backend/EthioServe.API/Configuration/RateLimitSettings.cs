namespace EthioServe.API.Configuration;

public sealed class RateLimitSettings
{
    public const string SectionName = "RateLimiting";

    public int GlobalPermitsPerMinute { get; set; } = 100;

    public int AuthPermitsPerMinute { get; set; } = 10;
}

public static class RateLimitPolicies
{
    public const string Auth = "auth";
}