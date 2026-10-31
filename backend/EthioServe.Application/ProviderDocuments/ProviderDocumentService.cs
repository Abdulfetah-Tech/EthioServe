using EthioServe.Application.Abstractions;
using EthioServe.Application.Common;
using EthioServe.Application.Common.Exceptions;
using EthioServe.Domain.Common;
using EthioServe.Domain.Entities;
using EthioServe.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace EthioServe.Application.ProviderDocuments;

public sealed class ProviderDocumentService(
    IApplicationDbContext db,
    ICurrentUser currentUser,
    IAuditTrail audit,
    IFileStorage storage) : IProviderDocumentService
{
    private const long MaxBytes = 5 * 1024 * 1024; // 5 MB

    private static readonly string[] AllowedContentTypes =
    [
        "image/jpeg", "image/png", "image/webp",
        "application/pdf",
    ];

    private Guid ProviderId =>
        currentUser.ProviderId ?? throw new ForbiddenException("This account has no provider profile.");

    public async Task<PagedResult<ProviderDocumentDto>> ListAsync(
        Guid providerId, ProviderDocumentQuery query, CancellationToken ct = default)
    {
        var isAdmin = currentUser.IsInRole(Roles.Admin);
        var isOwner = currentUser.ProviderId is { } pid && pid == providerId;

        if (!isAdmin && !isOwner)
        {
            throw new ForbiddenException("You cannot view this provider's documents.");
        }

        var (page, pageSize) = Paging.Normalize(query.Page, query.PageSize);

        var rows = db.ProviderDocuments.AsNoTracking()
            .Where(d => d.ProviderId == providerId)
            .OrderByDescending(d => d.CreatedAt).ThenByDescending(d => d.Id);

        var total = await rows.CountAsync(ct);

        var items = await rows
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(d => new ProviderDocumentDto(
                d.Id, d.DocumentType, d.OriginalFileName, d.ContentType, d.SizeBytes,
                d.Status, d.RejectionReason, d.CreatedAt))
            .ToListAsync(ct);

        return new PagedResult<ProviderDocumentDto>(items, page, pageSize, total);
    }

    public async Task<Guid> UploadAsync(
        Guid providerId, DocumentType documentType, string fileName, Stream content, CancellationToken ct = default)
    {
        var isAdmin = currentUser.IsInRole(Roles.Admin);
        var isOwner = currentUser.ProviderId is { } pid && pid == providerId;

        if (!isAdmin && !isOwner)
        {
            throw new ForbiddenException("You cannot upload documents for this provider.");
        }

        if (!await db.ProviderProfiles.AsNoTracking().AnyAsync(p => p.Id == providerId, ct))
        {
            throw new NotFoundException("Provider not found.");
        }

        using var buffer = new MemoryStream();
        var chunk = new byte[81_920];
        int read;

        while ((read = await content.ReadAsync(chunk.AsMemory(), ct)) > 0)
        {
            if (buffer.Length + read > MaxBytes)
            {
                throw new DomainException($"The file is larger than {MaxBytes / (1024 * 1024)} MB.");
            }

            buffer.Write(chunk, 0, read);
        }

        if (buffer.Length == 0)
        {
            throw new DomainException("The file is empty.");
        }

        var contentType = DetectContentType(fileName);

        if (!AllowedContentTypes.Contains(contentType))
        {
            throw new DomainException("Only JPEG, PNG, WebP images and PDF documents are allowed.");
        }

        var key = Guid.NewGuid().ToString("N") + System.IO.Path.GetExtension(fileName).ToLowerInvariant();

        await using (var stream = new MemoryStream(buffer.ToArray(), writable: false))
        {
            await storage.SaveAsync(key, stream, ct);
        }

        var document = new ProviderDocument(
            providerId, documentType, key, fileName, contentType, buffer.Length);

        db.ProviderDocuments.Add(document);
        audit.Record(
            "ProviderDocumentUploaded", nameof(ProviderDocument), document.Id, null,
            new { document.ProviderId, document.DocumentType, document.ContentType, document.SizeBytes });
        await db.SaveChangesAsync(ct);

        return document.Id;
    }

    public async Task ReviewAsync(
        Guid documentId, DocumentStatus status, string? rejectionReason, CancellationToken ct = default)
    {
        if (!currentUser.IsInRole(Roles.Admin))
        {
            throw new ForbiddenException("Only administrators can review documents.");
        }

        var document = await db.ProviderDocuments
            .FirstOrDefaultAsync(d => d.Id == documentId, ct)
            ?? throw new NotFoundException("Document not found.");

        if (document.Status != DocumentStatus.Pending)
        {
            throw new DomainException($"A document that is {document.Status} cannot be reviewed again.");
        }

        if (status == DocumentStatus.Rejected && string.IsNullOrWhiteSpace(rejectionReason))
        {
            throw new DomainException("A rejection reason is required.");
        }

        var before = new { document.Status, document.RejectionReason };

        document.Review(status, rejectionReason, currentUser.UserId, DateTimeOffset.UtcNow);

        audit.Record(
            "ProviderDocumentReviewed", nameof(ProviderDocument), document.Id,
            before, new { document.Status, document.RejectionReason, document.ReviewedBy, document.ReviewedAt });
        await db.SaveChangesAsync(ct);
    }

    private static string DetectContentType(string fileName)
    {
        var ext = System.IO.Path.GetExtension(fileName).ToLowerInvariant();

        return ext switch
        {
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            ".webp" => "image/webp",
            ".pdf" => "application/pdf",
            _ => "application/octet-stream",
        };
    }
}
