using EthioServe.Application.Abstractions;
using EthioServe.Application.Common;
using EthioServe.Application.Common.Exceptions;
using EthioServe.Application.Requests;
using EthioServe.Domain.Common;
using EthioServe.Domain.Entities;
using EthioServe.Domain.Enums;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.EntityFrameworkCore;

namespace EthioServe.Application.Attachments;

public sealed class RequestAttachmentService(
    IApplicationDbContext db,
    ICurrentUser currentUser,
    IFileStorage storage,
    IAuditTrail audit,
    IProviderFeedService feed) : IRequestAttachmentService
{
    private const int ChunkSize = 81_920;

    // The customer comes from the signed-in user's token, never from the request.
    private Guid CustomerId =>
        currentUser.CustomerId ?? throw new ForbiddenException("This account has no customer profile.");

    public async Task<AttachmentDto> AddAsync(Guid requestId, string? fileName, Stream content, CancellationToken ct = default)
    {
        var customerId = CustomerId;
        var status = await OwnedRequestStatusAsync(requestId, customerId, ct);

        if (status != ServiceRequestStatus.Pending)
        {
            throw new DomainException("Photos can only be added while the request is waiting for quotes.");
        }

        var existing = await db.RequestAttachments.CountAsync(a => a.ServiceRequestId == requestId, ct);

        if (existing >= AttachmentRules.MaxFiles)
        {
            throw new DomainException($"A request can have at most {AttachmentRules.MaxFiles} photos.");
        }

        var bytes = await ReadAllowedAsync(content, ct);
        var kind = ImageSniffer.Detect(bytes) ?? throw Invalid("Only JPEG, PNG and WebP photos are allowed.");

        // The stored name is random. The user's file name is only ever kept for display.
        var key = Guid.NewGuid().ToString("N") + kind.Extension;

        await using (var stream = new MemoryStream(bytes, writable: false))
        {
            await storage.SaveAsync(key, stream, ct);
        }

        var attachment = new RequestAttachment(
            requestId, key, AttachmentRules.CleanName(fileName, kind.Extension), kind.ContentType, bytes.Length);

        db.RequestAttachments.Add(attachment);
        audit.Record(
            "RequestAttachmentAdded", nameof(RequestAttachment), attachment.Id, null,
            new { attachment.ServiceRequestId, attachment.ContentType, attachment.SizeBytes });

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch
        {
            await storage.DeleteAsync(key, CancellationToken.None); // never leave a file nobody points to
            throw;
        }

        return ToDto(attachment);
    }

    public async Task<IReadOnlyList<AttachmentDto>> ListAsync(Guid requestId, CancellationToken ct = default)
    {
        await EnsureCanViewAsync(requestId, ct);

        return await db.RequestAttachments.AsNoTracking()
            .Where(a => a.ServiceRequestId == requestId)
            .OrderBy(a => a.CreatedAt).ThenBy(a => a.Id)
            .Select(a => new AttachmentDto(a.Id, a.OriginalFileName, a.ContentType, a.SizeBytes, a.CreatedAt))
            .ToListAsync(ct);
    }

    public async Task<AttachmentContent> OpenAsync(Guid requestId, Guid attachmentId, CancellationToken ct = default)
    {
        await EnsureCanViewAsync(requestId, ct);

        // Matched on the request AND the attachment, so a visible request can never expose another request's photo.
        var attachment = await db.RequestAttachments.AsNoTracking()
                             .FirstOrDefaultAsync(a => a.Id == attachmentId && a.ServiceRequestId == requestId, ct)
                         ?? throw new NotFoundException("Photo not found.");

        var stream = await storage.OpenReadAsync(attachment.StorageKey, ct)
                     ?? throw new NotFoundException("Photo not found.");

        return new AttachmentContent(stream, attachment.ContentType, attachment.OriginalFileName);
    }

    public async Task RemoveAsync(Guid requestId, Guid attachmentId, CancellationToken ct = default)
    {
        var customerId = CustomerId;
        var status = await OwnedRequestStatusAsync(requestId, customerId, ct);

        if (status != ServiceRequestStatus.Pending)
        {
            throw new DomainException("Photos can only be removed while the request is waiting for quotes.");
        }

        var attachment = await db.RequestAttachments
                             .FirstOrDefaultAsync(a => a.Id == attachmentId && a.ServiceRequestId == requestId, ct)
                         ?? throw new NotFoundException("Photo not found.");

        db.RequestAttachments.Remove(attachment);
        audit.Record(
            "RequestAttachmentRemoved", nameof(RequestAttachment), attachment.Id,
            new { attachment.ServiceRequestId, attachment.ContentType, attachment.SizeBytes }, null);
        await db.SaveChangesAsync(ct);

        try
        {
            await storage.DeleteAsync(attachment.StorageKey, ct);
        }
        catch (IOException)
        {
            // The record is gone, so nobody can reach the file. A leftover file is cleaned up by a later maintenance job.
        }
    }

    private async Task<ServiceRequestStatus> OwnedRequestStatusAsync(Guid requestId, Guid customerId, CancellationToken ct)
    {
        var status = await db.ServiceRequests.AsNoTracking()
            .Where(r => r.Id == requestId && r.CustomerId == customerId)
            .Select(r => (ServiceRequestStatus?)r.Status)
            .FirstOrDefaultAsync(ct);

        return status ?? throw new NotFoundException("Request not found.");
    }

    private async Task EnsureCanViewAsync(Guid requestId, CancellationToken ct)
    {
        if (currentUser.IsInRole(Roles.Customer))
        {
            var customerId = CustomerId;

            if (!await db.ServiceRequests.AnyAsync(r => r.Id == requestId && r.CustomerId == customerId, ct))
            {
                throw new NotFoundException("Request not found.");
            }

            return;
        }

        if (currentUser.IsInRole(Roles.Provider))
        {
            // The same rule as the feed: a verified provider, an open request, their city, a service they offer.
            _ = await feed.GetAsync(requestId, ct);

            return;
        }

        throw new ForbiddenException("You cannot view these photos.");
    }

    /// <summary>Reads the upload into memory, refusing anything larger than the limit without trusting the declared size.</summary>
    private static async Task<byte[]> ReadAllowedAsync(Stream content, CancellationToken ct)
    {
        using var buffer = new MemoryStream();
        var chunk = new byte[ChunkSize];
        int read;

        while ((read = await content.ReadAsync(chunk.AsMemory(), ct)) > 0)
        {
            if (buffer.Length + read > AttachmentRules.MaxBytes)
            {
                throw Invalid($"The photo is larger than {AttachmentRules.MaxBytes / (1024 * 1024)} MB.");
            }

            buffer.Write(chunk, 0, read);
        }

        if (buffer.Length == 0)
        {
            throw Invalid("The file is empty.");
        }

        return buffer.ToArray();
    }

    private static ValidationException Invalid(string message) =>
        new(new[] { new ValidationFailure("file", message) });

    private static AttachmentDto ToDto(RequestAttachment a) =>
        new(a.Id, a.OriginalFileName, a.ContentType, a.SizeBytes, a.CreatedAt);
}