using EthioServe.Application.Requests;
using FluentValidation;

namespace EthioServe.Application.Quotes;

public sealed class SubmitQuoteRequestValidator : AbstractValidator<SubmitQuoteRequest>
{
    public const decimal MaxAmount = 10_000_000m;
    public const int MinDurationMinutes = 15;
    public const int MaxDurationMinutes = 43_200; // 30 days
    public const int MaxValidDays = 30;

    public SubmitQuoteRequestValidator()
    {
        RuleFor(x => x.Amount).InclusiveBetween(0.01m, MaxAmount).PrecisionScale(12, 2, true);
        RuleFor(x => x.Description).TrimmedLength(10, 2000);
        RuleFor(x => x.EstimatedDurationMinutes).InclusiveBetween(MinDurationMinutes, MaxDurationMinutes);
        RuleFor(x => x.ValidForDays).InclusiveBetween(1, MaxValidDays);
    }
}