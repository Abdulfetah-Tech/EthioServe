using EthioServe.Domain.Common;
using EthioServe.Domain.Entities;
using EthioServe.Domain.Enums;

namespace EthioServe.Tests.Unit;

public sealed class ServiceRequestQuoteTests
{
    private static ServiceRequest NewRequest() =>
        new(Guid.NewGuid(), Guid.NewGuid(), "Leaking kitchen pipe", "The pipe under the sink leaks.", "Bole", 1, 1);

    private static ServiceRequest InStatus(ServiceRequestStatus status)
    {
        var request = NewRequest();

        switch (status)
        {
            case ServiceRequestStatus.Quoted:
                request.MarkQuoted();
                break;
            case ServiceRequestStatus.Accepted:
                request.MarkQuoted();
                request.AcceptQuote();
                break;
            case ServiceRequestStatus.Cancelled:
                request.Cancel("No longer needed");
                break;
        }

        return request;
    }

    [Theory]
    [InlineData(ServiceRequestStatus.Pending)]
    [InlineData(ServiceRequestStatus.Quoted)]
    public void A_pending_or_quoted_request_can_accept_a_quote(ServiceRequestStatus from)
    {
        var request = InStatus(from);

        request.AcceptQuote();

        Assert.Equal(ServiceRequestStatus.Accepted, request.Status);
    }

    [Theory]
    [InlineData(ServiceRequestStatus.Accepted)]
    [InlineData(ServiceRequestStatus.Cancelled)]
    public void A_closed_request_cannot_accept_a_quote(ServiceRequestStatus from)
    {
        var request = InStatus(from);

        Assert.Throws<DomainException>(() => request.AcceptQuote());
        Assert.Equal(from, request.Status);
    }

    [Fact]
    public void A_quoted_request_can_wait_for_quotes_again()
    {
        var request = InStatus(ServiceRequestStatus.Quoted);

        request.ReopenForQuotes();

        Assert.Equal(ServiceRequestStatus.Pending, request.Status);
    }

    [Theory]
    [InlineData(ServiceRequestStatus.Pending)]
    [InlineData(ServiceRequestStatus.Accepted)]
    [InlineData(ServiceRequestStatus.Cancelled)]
    public void Only_a_quoted_request_can_be_reopened(ServiceRequestStatus from)
    {
        var request = InStatus(from);

        Assert.Throws<DomainException>(() => request.ReopenForQuotes());
        Assert.Equal(from, request.Status);
    }

    [Fact]
    public void An_accepted_request_can_no_longer_be_edited_cancelled_or_quoted()
    {
        var request = InStatus(ServiceRequestStatus.Accepted);

        Assert.Throws<DomainException>(() => request.Cancel("Changed my mind"));
        Assert.Throws<DomainException>(() => request.UpdateDetails("New title", "A new description of the job.", "Piassa", 2, 3, null, null, null, null));
        Assert.Throws<DomainException>(() => request.MarkQuoted());
    }

    [Fact]
    public void A_reopened_request_can_be_edited_again()
    {
        var request = InStatus(ServiceRequestStatus.Quoted);
        request.ReopenForQuotes();

        request.UpdateDetails("New title", "A new description of the job.", "Piassa", 2, 3, null, null, null, null);

        Assert.Equal("New title", request.Title);
    }
}