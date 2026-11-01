using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EthioServe.API.Controllers;

[ApiController]
[Route("api/customer/payments")]
[Authorize]
public sealed class CustomerPaymentsController : ControllerBase
{
    [HttpGet]
    public ActionResult Get() => Ok(new { message = "Customer payments endpoint is available." });
}
