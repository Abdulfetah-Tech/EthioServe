using EthioServe.Domain.Common;
using EthioServe.Domain.Enums;

namespace EthioServe.Domain.Entities;

/// <summary>Verification document. Only a generated storage key is stored, never a public URL or user filename.</summary>
public sealed class ProviderDocument : BaseEntity
{
    private ProviderDocument() { }

    public ProviderDocument(
        Guid providerId, DocumentType documentType, string storageKey,
        string originalFileName, string contentType, long sizeBytes)
    {
        if (sizeBytes <= 0)
        {
            throw new DomainException("sizeBytes must be greater than zero.");
        }

        ProviderId = providerId;
        DocumentType = documentType;
        StorageKey = Guard.NotBlank(storageKey, nameof(storageKey));
        OriginalFileName = Guard.NotBlank(originalFileName, nameof(originalFileName));
        ContentType = Guard.NotBlank(contentType, nameof(contentType));
        SizeBytes = sizeBytes;
        Status = DocumentStatus.Pending;
    }

    public Guid ProviderId { get; private set; }

    public DocumentType DocumentType { get; private set; }

    public string StorageKey { get; private set; } = string.Empty;

    public string OriginalFileName { get; private set; } = string.Empty;

    public string ContentType { get; private set; } = string.Empty;

    public long SizeBytes { get; private set; }

    public DocumentStatus Status { get; private set; }

    public Guid? ReviewedBy { get; private set; }

    public DateTimeOffset? ReviewedAt { get; private set; }

    public string? RejectionReason { get; private set; }

    public void Review(DocumentStatus status, string? rejectionReason, Guid? reviewedBy, DateTimeOffset reviewedAt)
    {
        if (Status != DocumentStatus.Pending)
        {
            throw new DomainException($"A document that is {Status} cannot be reviewed again.");
        }

        if (status == DocumentStatus.Rejected && string.IsNullOrWhiteSpace(rejectionReason))
        {
            throw new DomainException("A rejection reason is required.");
        }

        Status = status;
        ReviewedBy = reviewedBy;
        ReviewedAt = reviewedAt;
        RejectionReason = status == DocumentStatus.Rejected ? rejectionReason?.Trim() : null;
    }
}