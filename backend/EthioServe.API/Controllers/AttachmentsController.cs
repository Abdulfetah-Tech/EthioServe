using EthioServe.API.Authorization;
using EthioServe.Application.Attachments;
using EthioServe.Domain.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EthioServe.API.Controllers;

/// <summary>
/// Photos on a service request. The owner can add, list, view and remove them. A provider who can see the request in
/// their feed can list and view them. Nobody else can do anything.
/// </summary>
[ApiController]
[Route("api/requests/{requestId:guid}/attachments")]
public sealed class AttachmentsController(IRequestAttachmentService attachments) : ControllerBase
{
    private const string CustomerOrProvider = Roles.Customer + "," + Roles.Provider;

    [HttpPost]
    [Authorize(Policy = Policies.CustomerOnly)]
    [RequestSizeLimit(AttachmentRules.MaxBytes + 524_288)]
    public async Task<ActionResult<AttachmentDto>> Upload(Guid requestId, IFormFile file, CancellationToken ct)
    {
        await using var stream = file.OpenReadStream();
        var created = await attachments.AddAsync(requestId, file.FileName, stream, ct);

        return Created($"/api/requests/{requestId}/attachments/{created.Id}", created);
    }

    [HttpGet]
    [Authorize(Roles = CustomerOrProvider)]
    public async Task<ActionResult<IReadOnlyList<AttachmentDto>>> List(Guid requestId, CancellationToken ct) =>
        Ok(await attachments.ListAsync(requestId, ct));

    [HttpGet("{attachmentId:guid}")]
    [Authorize(Roles = CustomerOrProvider)]
    public async Task<IActionResult> Download(Guid requestId, Guid attachmentId, CancellationToken ct)
    {
        var content = await attachments.OpenAsync(requestId, attachmentId, ct);

        // The file is shown as an image and can do nothing else: no scripts, no framing, no sniffing.
        Response.Headers["Content-Security-Policy"] = "default-src 'none'; sandbox";
        Response.Headers["Cache-Control"] = "private, max-age=300";

        return File(content.Stream, content.ContentType);
    }

    [HttpDelete("{attachmentId:guid}")]
    [Authorize(Policy = Policies.CustomerOnly)]
    public async Task<IActionResult> Remove(Guid requestId, Guid attachmentId, CancellationToken ct)
    {
        await attachments.RemoveAsync(requestId, attachmentId, ct);

        return NoContent();
    }
}