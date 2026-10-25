using EthioServe.Domain.Enums;

namespace EthioServe.Application.Quotes;

public static class QuoteStatusRules
{
    /// <summary>
    /// The status a person should see. A pending quote whose deadline passed is Expired, and a pending quote on a
    /// request that is closed (cancelled, or already accepted elsewhere) is no longer open either.
    /// </summary>
    public static QuoteStatus Effective(
        QuoteStatus stored, DateTimeOffset validUntil, ServiceRequestStatus requestStatus, DateTimeOffset now)
    {
        if (stored != QuoteStatus.Pending)
        {
            return stored;
        }

        if (requestStatus == ServiceRequestStatus.Cancelled)
        {
            return QuoteStatus.Cancelled;
        }

        if (requestStatus is not (ServiceRequestStatus.Pending or ServiceRequestStatus.Quoted))
        {
            return QuoteStatus.Rejected;
        }

        return validUntil <= now ? QuoteStatus.Expired : QuoteStatus.Pending;
    }
}