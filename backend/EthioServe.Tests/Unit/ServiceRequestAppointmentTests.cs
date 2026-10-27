using EthioServe.Domain.Common;
using EthioServe.Domain.Entities;
using EthioServe.Domain.Enums;

namespace EthioServe.Tests.Unit;

public sealed class ServiceRequestAppointmentTests
{
    private static readonly DateTimeOffset Now = new(2026, 12, 1, 8, 0, 0, TimeSpan.Zero);

    // Rejected is left out: no domain method sets it yet.
    private static readonly ServiceRequestStatus[] Statuses =
    [
        ServiceRequestStatus.Pending, ServiceRequestStatus.Quoted, ServiceRequestStatus.Accepted,
        ServiceRequestStatus.Scheduled, ServiceRequestStatus.InProgress, ServiceRequestStatus.Completed,
        ServiceRequestStatus.Cancelled
    ];

    private static readonly Dictionary<string, Action<ServiceRequest>> Actions = new()
    {
        ["MarkScheduled"] = r => r.MarkScheduled(),
        ["ReturnToAccepted"] = r => r.ReturnToAccepted(),
        ["StartWork"] = r => r.StartWork(),
        ["Complete"] = r => r.Complete(Now),
    };

    private static readonly Dictionary<(ServiceRequestStatus From, string Action), ServiceRequestStatus> Allowed = new()
    {
        [(ServiceRequestStatus.Accepted, "MarkScheduled")] = ServiceRequestStatus.Scheduled,
        [(ServiceRequestStatus.Scheduled, "ReturnToAccepted")] = ServiceRequestStatus.Accepted,
        [(ServiceRequestStatus.Scheduled, "StartWork")] = ServiceRequestStatus.InProgress,
        [(ServiceRequestStatus.InProgress, "Complete")] = ServiceRequestStatus.Completed,
    };

    public static IEnumerable<object[]> AllCombinations()
    {
        foreach (var status in Statuses)
        {
            foreach (var action in Actions.Keys)
            {
                yield return new object[] { status, action };
            }
        }
    }

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
            case ServiceRequestStatus.Scheduled:
                request.MarkQuoted();
                request.AcceptQuote();
                request.MarkScheduled();
                break;
            case ServiceRequestStatus.InProgress:
                request.MarkQuoted();
                request.AcceptQuote();
                request.MarkScheduled();
                request.StartWork();
                break;
            case ServiceRequestStatus.Completed:
                request.MarkQuoted();
                request.AcceptQuote();
                request.MarkScheduled();
                request.StartWork();
                request.Complete(Now);
                break;
            case ServiceRequestStatus.Cancelled:
                request.Cancel("No longer needed");
                break;
        }

        return request;
    }

    [Theory]
    [MemberData(nameof(AllCombinations))]
    public void The_job_moves_through_its_stages_exactly(ServiceRequestStatus from, string action)
    {
        var request = InStatus(from);

        if (Allowed.TryGetValue((from, action), out var expected))
        {
            Actions[action](request);

            Assert.Equal(expected, request.Status);
        }
        else
        {
            Assert.Throws<DomainException>(() => Actions[action](request));
            Assert.Equal(from, request.Status);
        }
    }

    [Fact]
    public void Completing_records_when_the_job_was_finished()
    {
        var request = InStatus(ServiceRequestStatus.InProgress);

        request.Complete(Now);

        Assert.Equal(Now, request.CompletedAt);
    }

    [Theory]
    [InlineData(ServiceRequestStatus.Scheduled)]
    [InlineData(ServiceRequestStatus.InProgress)]
    [InlineData(ServiceRequestStatus.Completed)]
    public void A_scheduled_or_running_job_can_still_not_be_edited_or_cancelled_by_the_customer(ServiceRequestStatus status)
    {
        var request = InStatus(status);

        Assert.Throws<DomainException>(() => request.Cancel("Changed my mind"));
        Assert.Throws<DomainException>(() => request.UpdateDetails("New title", "A new description of the job.", "Piassa", 2, 3, null, null, null, null));
        Assert.Equal(status, request.Status);
    }
}