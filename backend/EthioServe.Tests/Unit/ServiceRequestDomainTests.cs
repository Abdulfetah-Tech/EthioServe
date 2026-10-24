using EthioServe.Domain.Common;
using EthioServe.Domain.Entities;
using EthioServe.Domain.Enums;

namespace EthioServe.Tests.Unit;

public sealed class ServiceRequestDomainTests
{
    private static ServiceRequest NewRequest(decimal? min = null, decimal? max = null) =>
        new(Guid.NewGuid(), Guid.NewGuid(), "Leaking kitchen pipe", "The pipe under the sink leaks.", "Bole", 1, 1, min, max);

    private static ServiceRequest QuotedRequest()
    {
        var request = NewRequest();
        request.MarkQuoted();

        return request;
    }

    private static void Edit(ServiceRequest request, string title = "New title", decimal? min = null, decimal? max = null) =>
        request.UpdateDetails(title, "A new description of the job.", "Piassa", 2, 3, min, max, null, null);

    [Fact]
    public void New_requests_start_pending_with_trimmed_text()
    {
        var request = new ServiceRequest(Guid.NewGuid(), Guid.NewGuid(), "  Fix pipe  ", "  Leaking.  ", "  Bole  ", 1, 1);

        Assert.Equal(ServiceRequestStatus.Pending, request.Status);
        Assert.Equal("Fix pipe", request.Title);
        Assert.Equal("Leaking.", request.Description);
        Assert.Equal("Bole", request.Address);
    }

    [Theory]
    [InlineData(-1.0, null)]
    [InlineData(null, -1.0)]
    [InlineData(500.0, 100.0)]
    public void Invalid_budgets_are_rejected_when_creating(double? min, double? max)
    {
        Assert.Throws<DomainException>(() => NewRequest((decimal?)min, (decimal?)max));
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData(0.0, 0.0)]
    [InlineData(100.0, null)]
    [InlineData(null, 500.0)]
    [InlineData(100.0, 500.0)]
    public void Valid_budgets_are_accepted(double? min, double? max)
    {
        var request = NewRequest((decimal?)min, (decimal?)max);

        Assert.Equal((decimal?)min, request.BudgetMin);
        Assert.Equal((decimal?)max, request.BudgetMax);
    }

    [Fact]
    public void A_pending_request_can_be_edited()
    {
        var request = NewRequest();
        var serviceId = request.ServiceId;

        request.UpdateDetails(" New title ", "A new description of the job.", "Piassa", 2, 3, 100m, 400m, new DateOnly(2026, 12, 1), new TimeOnly(9, 30));

        Assert.Equal("New title", request.Title);
        Assert.Equal("Piassa", request.Address);
        Assert.Equal((2, 3), (request.RegionId, request.CityId));
        Assert.Equal((100m, 400m), (request.BudgetMin, request.BudgetMax));
        Assert.Equal(new DateOnly(2026, 12, 1), request.PreferredDate);
        Assert.Equal(new TimeOnly(9, 30), request.PreferredTime);
        Assert.Equal(serviceId, request.ServiceId); // the service cannot change
    }

    [Fact]
    public void An_invalid_edit_changes_nothing()
    {
        var request = NewRequest();

        Assert.Throws<DomainException>(() => Edit(request, min: 500m, max: 100m));
        Assert.Throws<DomainException>(() => Edit(request, title: "   "));

        Assert.Equal("Leaking kitchen pipe", request.Title);
        Assert.Equal("Bole", request.Address);
    }

    [Fact]
    public void The_first_quote_moves_a_pending_request_to_quoted_once()
    {
        var request = NewRequest();

        request.MarkQuoted();

        Assert.Equal(ServiceRequestStatus.Quoted, request.Status);
        Assert.Throws<DomainException>(() => request.MarkQuoted());
    }

    [Fact]
    public void A_request_that_has_been_quoted_can_no_longer_be_edited()
    {
        var request = QuotedRequest();

        Assert.Throws<DomainException>(() => Edit(request));
        Assert.Equal("Leaking kitchen pipe", request.Title);
    }

    [Fact]
    public void A_pending_or_quoted_request_can_be_cancelled_with_a_trimmed_reason()
    {
        var pending = NewRequest();
        var quoted = QuotedRequest();

        pending.Cancel("  Found someone else  ");
        quoted.Cancel(null);

        Assert.Equal(ServiceRequestStatus.Cancelled, pending.Status);
        Assert.Equal("Found someone else", pending.CancelledReason);
        Assert.Equal(ServiceRequestStatus.Cancelled, quoted.Status);
        Assert.Null(quoted.CancelledReason);
    }

    [Fact]
    public void A_blank_reason_is_stored_as_no_reason()
    {
        var request = NewRequest();

        request.Cancel("   ");

        Assert.Null(request.CancelledReason);
    }

    [Fact]
    public void A_reason_over_500_characters_is_rejected_and_the_request_stays_open()
    {
        var request = NewRequest();

        Assert.Throws<DomainException>(() => request.Cancel(new string('x', 501)));

        Assert.Equal(ServiceRequestStatus.Pending, request.Status);
        Assert.Null(request.CancelledReason);
    }

    [Fact]
    public void A_cancelled_request_cannot_be_cancelled_edited_or_quoted()
    {
        var request = NewRequest();
        request.Cancel("No longer needed");

        Assert.Throws<DomainException>(() => request.Cancel("Again"));
        Assert.Throws<DomainException>(() => Edit(request));
        Assert.Throws<DomainException>(() => request.MarkQuoted());
        Assert.Equal("No longer needed", request.CancelledReason);
    }
}