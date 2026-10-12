namespace EthioServe.Domain.Common;

public static class Roles
{
    public const string Admin = "Admin";
    public const string Provider = "Provider";
    public const string Customer = "Customer";

    public static readonly IReadOnlyList<string> All = [Admin, Provider, Customer];
}