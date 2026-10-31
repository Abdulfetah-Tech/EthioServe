using EthioServe.Application.Abstractions;
using EthioServe.Application.Common;
using EthioServe.Application.Common.Exceptions;
using EthioServe.Application.Notifications;
using EthioServe.Domain.Entities;
using EthioServe.Domain.Enums;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace EthioServe.Application.Messages;

public sealed class MessageService(
    IApplicationDbContext db,
    ICurrentUser currentUser,
    IAuditTrail audit,
    INotifier notifier,
    IValidator<SendMessageRequest> validator) : IMessageService
{
    private Guid UserId =>
        currentUser.UserId ?? throw new ForbiddenException("You must be signed in.");

    public async Task<PagedResult<MessageDto>> ListAsync(
        Guid requestId, MessageQuery query, CancellationToken ct = default)
    {
        await EnsureParticipantAsync(requestId, UserId, ct);

        var (page, pageSize) = Paging.Normalize(query.Page, query.PageSize);

        var rows = db.RequestMessages.AsNoTracking()
            .Where(m => m.ServiceRequestId == requestId)
            .OrderByDescending(m => m.CreatedAt).ThenByDescending(m => m.Id);

        var total = await rows.CountAsync(ct);

        var items = await rows
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(m => new MessageDto(
                m.Id, m.ServiceRequestId, m.ProviderId, m.SenderUserId, m.Body, m.CreatedAt))
            .ToListAsync(ct);

        return new PagedResult<MessageDto>(items, page, pageSize, total);
    }

    public async Task<MessageDto> SendAsync(
        Guid requestId, SendMessageRequest request, CancellationToken ct = default)
    {
        await validator.ValidateAndThrowAsync(request, ct);

        var senderId = UserId;

        var participant = await FindParticipantAsync(requestId, senderId, ct)
            ?? throw new NotFoundException("Request not found.");

        var message = new RequestMessage(
            requestId, participant.ProviderId, senderId, request.Body);

        db.RequestMessages.Add(message);
        audit.Record(
            "MessageSent", nameof(RequestMessage), message.Id, null,
            new { message.ServiceRequestId, message.ProviderId, message.SenderUserId });
        await db.SaveChangesAsync(ct);

        var recipient = participant.ProviderId == senderId ? participant.CustomerId : participant.ProviderId;
        var recipientUser = await RecipientUserIdAsync(recipient, ct);

        if (recipientUser is { } id)
        {
            await notifier.ToUserAsync(
                id, NotificationType.NewMessage, "New message",
                $"You have a new message on a service request.", "ServiceRequest", requestId, ct);
            await db.SaveChangesAsync(ct);
        }

        return new MessageDto(
            message.Id, message.ServiceRequestId, message.ProviderId, message.SenderUserId,
            message.Body, message.CreatedAt);
    }

    private async Task EnsureParticipantAsync(Guid requestId, Guid userId, CancellationToken ct)
    {
        var isParticipant = await (
                from r in db.ServiceRequests.AsNoTracking()
                join q in db.Quotes.AsNoTracking() on r.Id equals q.ServiceRequestId
                where r.Id == requestId
                      && (r.CustomerId == userId || q.ProviderId == userId)
                select 1)
            .AnyAsync(ct);

        if (!isParticipant)
        {
            throw new NotFoundException("Request not found.");
        }
    }

    private async Task<(Guid CustomerId, Guid ProviderId)?> FindParticipantAsync(
        Guid requestId, Guid userId, CancellationToken ct)
    {
        return await (
                from r in db.ServiceRequests.AsNoTracking()
                join q in db.Quotes.AsNoTracking() on r.Id equals q.ServiceRequestId
                where r.Id == requestId
                      && (r.CustomerId == userId || q.ProviderId == userId)
                select new { r.CustomerId, q.ProviderId })
            .FirstOrDefaultAsync(ct) is { } row
            ? (row.CustomerId, row.ProviderId)
            : null;
    }

    private async Task<Guid?> RecipientUserIdAsync(Guid profileId, CancellationToken ct)
    {
        return await db.CustomerProfiles.AsNoTracking()
            .Where(c => c.Id == profileId)
            .Select(c => (Guid?)c.UserId)
            .FirstOrDefaultAsync(ct)
            ?? await db.ProviderProfiles.AsNoTracking()
            .Where(p => p.Id == profileId)
            .Select(p => (Guid?)p.UserId)
            .FirstOrDefaultAsync(ct);
    }
}
