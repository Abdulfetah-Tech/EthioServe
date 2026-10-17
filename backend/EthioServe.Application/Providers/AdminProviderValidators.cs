using FluentValidation;

namespace EthioServe.Application.Providers;

public sealed class ReviewDecisionRequestValidator : AbstractValidator<ReviewDecisionRequest>
{
    public ReviewDecisionRequestValidator()
    {
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(500);
    }
}