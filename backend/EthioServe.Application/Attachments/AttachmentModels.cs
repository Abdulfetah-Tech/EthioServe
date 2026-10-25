namespace EthioServe.Application.Attachments;

public sealed record AttachmentDto(Guid Id, string FileName, string ContentType, long SizeBytes, DateTimeOffset CreatedAt);

/// <summary>An open file. The caller must dispose the stream.</summary>
public sealed record AttachmentContent(Stream Stream, string ContentType, string FileName);

public interface IRequestAttachmentService
{
    /// <summary>Customer only: add a photo to one of their own pending requests.</summary>
    Task<AttachmentDto> AddAsync(Guid requestId, string? fileName, Stream content, CancellationToken ct = default);

    /// <summary>The owner, or a provider who can see the request in their feed.</summary>
    Task<IReadOnlyList<AttachmentDto>> ListAsync(Guid requestId, CancellationToken ct = default);

    /// <summary>The owner, or a provider who can see the request in their feed.</summary>
    Task<AttachmentContent> OpenAsync(Guid requestId, Guid attachmentId, CancellationToken ct = default);

    /// <summary>Customer only: remove a photo from one of their own pending requests.</summary>
    Task RemoveAsync(Guid requestId, Guid attachmentId, CancellationToken ct = default);
}