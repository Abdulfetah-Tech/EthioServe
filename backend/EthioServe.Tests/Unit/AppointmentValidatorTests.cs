using EthioServe.Application.Appointments;

namespace EthioServe.Tests.Unit;

public sealed class AppointmentValidatorTests
{
    private static readonly ScheduleAppointmentRequestValidator Schedule = new();
    private static readonly RescheduleAppointmentRequestValidator Reschedule = new();

    private static readonly DateTimeOffset Start = new(2026, 12, 1, 8, 0, 0, TimeSpan.Zero);

    [Fact]
    public void A_start_with_or_without_a_note_passes()
    {
        Assert.True(Schedule.Validate(new ScheduleAppointmentRequest(Start, null)).IsValid);
        Assert.True(Schedule.Validate(new ScheduleAppointmentRequest(Start, "Bring spare pipes")).IsValid);
        Assert.True(Reschedule.Validate(new RescheduleAppointmentRequest(Start, null)).IsValid);
    }

    [Fact]
    public void A_missing_start_is_refused()
    {
        Assert.False(Schedule.Validate(new ScheduleAppointmentRequest(default, null)).IsValid);
        Assert.False(Reschedule.Validate(new RescheduleAppointmentRequest(default, null)).IsValid);
    }

    [Theory]
    [InlineData(1000, true)]
    [InlineData(1001, false)]
    public void The_note_is_limited_to_1000_characters(int length, bool expectedValid)
    {
        var note = new string('x', length);

        Assert.Equal(expectedValid, Schedule.Validate(new ScheduleAppointmentRequest(Start, note)).IsValid);
        Assert.Equal(expectedValid, Reschedule.Validate(new RescheduleAppointmentRequest(Start, note)).IsValid);
    }
}