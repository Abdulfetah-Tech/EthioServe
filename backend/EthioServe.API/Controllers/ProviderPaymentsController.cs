using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EthioServe.API.Controllers;

[ApiController]
[Route("api/provider/payments")]
[Authorize]
public sealed class ProviderPaymentsController : ControllerBase
{
    [HttpGet]
    public ActionResult Get() => Ok(new { message = "Provider payments endpoint is available." });
}
