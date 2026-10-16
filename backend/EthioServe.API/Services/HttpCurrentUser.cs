using System.Security.Claims;
using EthioServe.Application.Abstractions;
using EthioServe.Application.Common;

namespace EthioServe.API.Services;

public sealed class HttpCurrentUser(IHttpContextAccessor accessor) : ICurrentUser
{
    private ClaimsPrincipal? Principal => accessor.HttpContext?.User;

    public bool IsAuthenticated => Principal?.Identity?.IsAuthenticated == true;

    public Guid? UserId => ReadGuid(AppClaims.Subject);

    public Guid? CustomerId => ReadGuid(AppClaims.CustomerId);

    public Guid? ProviderId => ReadGuid(AppClaims.ProviderId);

    public string IpAddress => accessor.HttpContext?.Connection.RemoteIpAddress?.ToString() ?? "unknown";

    public string UserAgent => accessor.HttpContext?.Request.Headers.UserAgent.ToString() ?? string.Empty;

    public bool IsInRole(string role) => Principal?.IsInRole(role) == true;

    private Guid? ReadGuid(string claimType) =>
        Guid.TryParse(Principal?.FindFirst(claimType)?.Value, out var id) ? id : null;
}