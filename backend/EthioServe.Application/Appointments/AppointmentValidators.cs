using FluentValidation;

namespace EthioServe.Application.Appointments;

public sealed class ScheduleAppointmentRequestValidator : AbstractValidator<ScheduleAppointmentRequest>
{
    public ScheduleAppointmentRequestValidator()
    {
        RuleFor(x => x.StartAt).NotEmpty();
        RuleFor(x => x.Notes).MaximumLength(1000);
    }
}

public sealed class RescheduleAppointmentRequestValidator : AbstractValidator<RescheduleAppointmentRequest>
{
    public RescheduleAppointmentRequestValidator()
    {
        RuleFor(x => x.StartAt).NotEmpty();
        RuleFor(x => x.Notes).MaximumLength(1000);
    }
}