using EthioServe.API.Authorization;
using EthioServe.Application.Common;
using EthioServe.Application.ProviderDocuments;
using EthioServe.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EthioServe.API.Controllers;

/// <summary>A provider's verification documents. Owners upload; admins review.</summary>
[ApiController]
[Authorize]
[Route("api/providers/{providerId:guid}/documents")]
public sealed class ProviderDocumentsController(IProviderDocumentService documents) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<PagedResult<ProviderDocumentDto>>> List(
        Guid providerId, [FromQuery] ProviderDocumentQuery query, CancellationToken ct) =>
        Ok(await documents.ListAsync(providerId, query, ct));

    [HttpPost]
    [RequestSizeLimit(5 * 1024 * 1024 + 524_288)]
    public async Task<ActionResult<Guid>> Upload(
        Guid providerId,
        [FromForm] DocumentType documentType,
        IFormFile file,
        CancellationToken ct)
    {
        await using var stream = file.OpenReadStream();

        var id = await documents.UploadAsync(providerId, documentType, file.FileName, stream, ct);

        return Created($"/api/providers/{providerId}/documents/{id}", id);
    }

    [HttpPost("{documentId:guid}/review")]
    [Authorize(Policy = Policies.AdminOnly)]
    public async Task<IActionResult> Review(
        Guid providerId, Guid documentId,
        [FromBody] ReviewDocumentRequest request, CancellationToken ct)
    {
        await documents.ReviewAsync(documentId, request.Status, request.RejectionReason, ct);

        return NoContent();
    }
}

public sealed record ReviewDocumentRequest(DocumentStatus Status, string? RejectionReason);
