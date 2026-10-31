using EthioServe.Domain.Entities;
using FluentValidation;

namespace EthioServe.Application.Reviews;

public sealed class CreateReviewRequestValidator : AbstractValidator<CreateReviewRequest>
{
    public CreateReviewRequestValidator()
    {
        RuleFor(x => x.Rating).InclusiveBetween(1, 5);
        RuleFor(x => x.Comment).MaximumLength(Review.MaxCommentLength);
    }
}
