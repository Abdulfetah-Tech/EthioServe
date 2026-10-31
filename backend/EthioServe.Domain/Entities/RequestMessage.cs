using EthioServe.Domain.Common;

namespace EthioServe.Domain.Entities;

/// <summary>
/// One message in the private thread between a customer and one provider on a single service request.
/// Threads are per provider so quoting providers never read each other's messages.
/// </summary>
public sealed class RequestMessage : BaseEntity
{
    public const int MaxBodyLength = 2000;

    private RequestMessage() { }

    public RequestMessage(Guid serviceRequestId, Guid providerId, Guid senderUserId, string body)
    {
        ServiceRequestId = serviceRequestId;
        ProviderId = providerId;
        SenderUserId = senderUserId;
        Body = CleanBody(body);
    }

    public Guid ServiceRequestId { get; private set; }

    public Guid ProviderId { get; private set; }

    public Guid SenderUserId { get; private set; }

    public string Body { get; private set; } = string.Empty;

    private static string CleanBody(string body)
    {
        var text = Guard.NotBlank(body, nameof(body));

        if (text.Length > MaxBodyLength)
        {
            throw new DomainException($"The message cannot be longer than {MaxBodyLength} characters.");
        }

        return text;
    }
}
