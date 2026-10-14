using EthioServe.API.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EthioServe.Tests.Support;

[ApiController]
[Route("test-support/secure")]
public sealed class SecureTestController : ControllerBase
{
    [HttpGet("public")]
    [AllowAnonymous]
    public IActionResult Public() => Ok();

    [HttpGet("any")]
    [Authorize]
    public IActionResult Any() => Ok(new { user = User.FindFirst("sub")?.Value });

    [HttpGet("admin")]
    [Authorize(Policy = Policies.AdminOnly)]
    public IActionResult Admin() => Ok();

    [HttpGet("provider")]
    [Authorize(Policy = Policies.ProviderOnly)]
    public IActionResult Provider() => Ok();

    [HttpGet("customer")]
    [Authorize(Policy = Policies.CustomerOnly)]
    public IActionResult Customer() => Ok();

    // Deliberately no attribute at all: the fallback policy must protect it.
    [HttpGet("unmarked")]
    public IActionResult Unmarked() => Ok();
}