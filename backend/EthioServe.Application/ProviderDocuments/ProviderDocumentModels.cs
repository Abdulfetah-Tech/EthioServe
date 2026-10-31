using EthioServe.Application.Common;
using EthioServe.Domain.Enums;

namespace EthioServe.Application.ProviderDocuments;

public sealed record ProviderDocumentDto(
    Guid Id,
    DocumentType DocumentType,
    string OriginalFileName,
    string ContentType,
    long SizeBytes,
    DocumentStatus Status,
    string? RejectionReason,
    DateTimeOffset CreatedAt);

public sealed class ProviderDocumentQuery
{
    public int Page { get; set; } = 1;

    public int PageSize { get; set; } = Paging.DefaultPageSize;
}

/// <summary>A provider's verification documents. Only the owner can upload/list; admins can review all.</summary>
public interface IProviderDocumentService
{
    Task<PagedResult<ProviderDocumentDto>> ListAsync(Guid providerId, ProviderDocumentQuery query, CancellationToken ct = default);

    Task<Guid> UploadAsync(Guid providerId, DocumentType documentType, string fileName, Stream content, CancellationToken ct = default);

    Task ReviewAsync(Guid documentId, DocumentStatus status, string? rejectionReason, CancellationToken ct = default);
}
