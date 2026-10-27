using EthioServe.Domain.Common;
using EthioServe.Domain.Entities;
using EthioServe.Domain.Enums;

namespace EthioServe.Tests.Unit;

public sealed class AppointmentDomainTests
{
    private static readonly DateTimeOffset Start = new(2026, 12, 1, 8, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset End = Start.AddHours(2);
    private static readonly DateTimeOffset NewStart = Start.AddDays(1);
    private static readonly DateTimeOffset NewEnd = NewStart.AddHours(2);

    private static readonly Dictionary<string, Action<Appointment>> Actions = new()
    {
        ["Confirm"] = a => a.Confirm(),
        ["Reschedule"] = a => a.Reschedule(NewStart, NewEnd, "Moved"),
        ["Cancel"] = a => a.Cancel(),
        ["Start"] = a => a.Start(),
        ["Complete"] = a => a.Complete(),
    };

    // Every move the state machine allows. Anything not listed here must be refused.
    private static readonly Dictionary<(AppointmentStatus From, string Action), AppointmentStatus> Allowed = new()
    {
        [(AppointmentStatus.Scheduled, "Confirm")] = AppointmentStatus.Confirmed,
        [(AppointmentStatus.Scheduled, "Reschedule")] = AppointmentStatus.Scheduled,
        [(AppointmentStatus.Scheduled, "Cancel")] = AppointmentStatus.Cancelled,
        [(AppointmentStatus.Confirmed, "Reschedule")] = AppointmentStatus.Scheduled,
        [(AppointmentStatus.Confirmed, "Cancel")] = AppointmentStatus.Cancelled,
        [(AppointmentStatus.Confirmed, "Start")] = AppointmentStatus.InProgress,
        [(AppointmentStatus.InProgress, "Complete")] = AppointmentStatus.Completed,
    };

    public static IEnumerable<object[]> AllCombinations()
    {
        foreach (var status in Enum.GetValues<AppointmentStatus>())
        {
            foreach (var action in Actions.Keys)
            {
                yield return new object[] { status, action };
            }
        }
    }

    private static Appointment NewAppointment() =>
        new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Start, End, "Bole, near the main road", "Original note");

    private static Appointment InStatus(AppointmentStatus status)
    {
        var appointment = NewAppointment();

        switch (status)
        {
            case AppointmentStatus.Confirmed:
                appointment.Confirm();
                break;
            case AppointmentStatus.InProgress:
                appointment.Confirm();
                appointment.Start();
                break;
            case AppointmentStatus.Completed:
                appointment.Confirm();
                appointment.Start();
                appointment.Complete();
                break;
            case AppointmentStatus.Cancelled:
                appointment.Cancel();
                break;
            case AppointmentStatus.NoShow:
                // No domain method sets this yet; the status is reserved for a later phase.
                typeof(Appointment).GetProperty(nameof(Appointment.Status))!.SetValue(appointment, AppointmentStatus.NoShow);
                break;
        }

        return appointment;
    }

    [Theory]
    [MemberData(nameof(AllCombinations))]
    public void Appointments_move_through_the_state_machine_exactly(AppointmentStatus from, string action)
    {
        var appointment = InStatus(from);

        if (Allowed.TryGetValue((from, action), out var expected))
        {
            Actions[action](appointment);

            Assert.Equal(expected, appointment.Status);
        }
        else
        {
            Assert.Throws<DomainException>(() => Actions[action](appointment));
            Assert.Equal(from, appointment.Status);
        }
    }

    [Fact]
    public void A_new_appointment_is_scheduled()
    {
        Assert.Equal(AppointmentStatus.Scheduled, NewAppointment().Status);
    }

    [Fact]
    public void An_appointment_must_end_after_it_starts()
    {
        Assert.Throws<DomainException>(() => new Appointment(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Start, Start, "Bole"));
        Assert.Throws<DomainException>(() => new Appointment(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Start, Start.AddMinutes(-1), "Bole"));
    }

    [Fact]
    public void Rescheduling_changes_the_time_and_note_and_asks_the_customer_to_agree_again()
    {
        var appointment = InStatus(AppointmentStatus.Confirmed);

        appointment.Reschedule(NewStart, NewEnd, "A different note");

        Assert.Equal(AppointmentStatus.Scheduled, appointment.Status);
        Assert.Equal(NewStart, appointment.StartAt);
        Assert.Equal(NewEnd, appointment.EndAt);
        Assert.Equal("A different note", appointment.Notes);
    }

    [Fact]
    public void Rescheduling_without_a_note_clears_it()
    {
        var appointment = NewAppointment();

        appointment.Reschedule(NewStart, NewEnd, null);

        Assert.Null(appointment.Notes);
    }

    [Fact]
    public void An_invalid_new_time_changes_nothing()
    {
        var appointment = InStatus(AppointmentStatus.Confirmed);

        Assert.Throws<DomainException>(() => appointment.Reschedule(NewStart, NewStart, "x"));
        Assert.Throws<DomainException>(() => appointment.Reschedule(NewStart, NewStart.AddMinutes(-5), "x"));

        Assert.Equal(AppointmentStatus.Confirmed, appointment.Status);
        Assert.Equal(Start, appointment.StartAt);
        Assert.Equal("Original note", appointment.Notes);
    }
}