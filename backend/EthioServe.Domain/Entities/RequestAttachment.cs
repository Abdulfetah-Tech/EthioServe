using EthioServe.Domain.Common;

namespace EthioServe.Domain.Entities;

public sealed class RequestAttachment : BaseEntity
{
    private RequestAttachment() { }

    public RequestAttachment(
        Guid serviceRequestId, string storageKey, string originalFileName, string contentType, long sizeBytes)
    {
        if (sizeBytes <= 0)
        {
            throw new DomainException("sizeBytes must be greater than zero.");
        }

        ServiceRequestId = serviceRequestId;
        StorageKey = Guard.NotBlank(storageKey, nameof(storageKey));
        OriginalFileName = Guard.NotBlank(originalFileName, nameof(originalFileName));
        ContentType = Guard.NotBlank(contentType, nameof(contentType));
        SizeBytes = sizeBytes;
    }

    public Guid ServiceRequestId { get; private set; }

    public string StorageKey { get; private set; } = string.Empty;

    public string OriginalFileName { get; private set; } = string.Empty;

    public string ContentType { get; private set; } = string.Empty;

    public long SizeBytes { get; private set; }
}