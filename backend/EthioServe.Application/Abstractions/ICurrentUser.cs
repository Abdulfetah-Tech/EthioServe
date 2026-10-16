namespace EthioServe.Application.Abstractions;

/// <summary>The caller's identity, so application code never touches HttpContext or claims directly.</summary>
public interface ICurrentUser
{
    bool IsAuthenticated { get; }

    Guid? UserId { get; }

    Guid? CustomerId { get; }

    Guid? ProviderId { get; }

    string IpAddress { get; }

    string UserAgent { get; }

    bool IsInRole(string role);
}