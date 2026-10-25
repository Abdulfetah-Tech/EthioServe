using EthioServe.Application.Quotes;
using EthioServe.Domain.Enums;

namespace EthioServe.Tests.Unit;

public sealed class QuoteStatusRulesTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 26, 8, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Future = Now.AddDays(1);
    private static readonly DateTimeOffset Past = Now.AddDays(-1);

    [Theory]
    [InlineData(QuoteStatus.Accepted)]
    [InlineData(QuoteStatus.Rejected)]
    [InlineData(QuoteStatus.Expired)]
    [InlineData(QuoteStatus.Cancelled)]
    public void A_quote_that_is_no_longer_pending_keeps_its_status(QuoteStatus stored)
    {
        foreach (var requestStatus in Enum.GetValues<ServiceRequestStatus>())
        {
            Assert.Equal(stored, QuoteStatusRules.Effective(stored, Past, requestStatus, Now));
        }
    }

    [Theory]
    [InlineData(ServiceRequestStatus.Pending)]
    [InlineData(ServiceRequestStatus.Quoted)]
    public void An_open_pending_quote_on_an_open_request_stays_pending(ServiceRequestStatus requestStatus)
    {
        Assert.Equal(QuoteStatus.Pending, QuoteStatusRules.Effective(QuoteStatus.Pending, Future, requestStatus, Now));
    }

    [Theory]
    [InlineData(ServiceRequestStatus.Pending)]
    [InlineData(ServiceRequestStatus.Quoted)]
    public void A_pending_quote_past_its_deadline_reads_expired(ServiceRequestStatus requestStatus)
    {
        Assert.Equal(QuoteStatus.Expired, QuoteStatusRules.Effective(QuoteStatus.Pending, Past, requestStatus, Now));
        Assert.Equal(QuoteStatus.Expired, QuoteStatusRules.Effective(QuoteStatus.Pending, Now, requestStatus, Now));
    }

    [Theory]
    [InlineData(ServiceRequestStatus.Accepted)]
    [InlineData(ServiceRequestStatus.Scheduled)]
    [InlineData(ServiceRequestStatus.InProgress)]
    [InlineData(ServiceRequestStatus.Completed)]
    [InlineData(ServiceRequestStatus.Rejected)]
    public void A_pending_quote_on_a_request_that_moved_on_reads_rejected(ServiceRequestStatus requestStatus)
    {
        Assert.Equal(QuoteStatus.Rejected, QuoteStatusRules.Effective(QuoteStatus.Pending, Future, requestStatus, Now));
    }

    [Fact]
    public void A_pending_quote_on_a_cancelled_request_reads_cancelled()
    {
        Assert.Equal(
            QuoteStatus.Cancelled,
            QuoteStatusRules.Effective(QuoteStatus.Pending, Future, ServiceRequestStatus.Cancelled, Now));
    }
}