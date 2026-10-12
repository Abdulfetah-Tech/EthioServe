using EthioServe.Application.Common.Exceptions;
using EthioServe.Domain.Common;
using Microsoft.AspNetCore.Mvc;

namespace EthioServe.Tests.Support;

[ApiController]
[Route("test-support/errors")]
public sealed class ErrorsTestController : ControllerBase
{
    [HttpGet("{kind}")]
    public IActionResult Throw(string kind)
    {
        switch (kind)
        {
            case "not-found":
                throw new NotFoundException("Thing was not found.");
            case "forbidden":
                throw new ForbiddenException("You may not do that.");
            case "conflict":
                throw new ConflictException("Already exists.");
            case "domain":
                throw new DomainException("Rule violated.");
            default:
                throw new InvalidOperationException("SENSITIVE-INTERNAL-DETAIL");
        }
    }
}