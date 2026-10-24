using FluentValidation;

namespace EthioServe.Application.Requests;

internal static class JobRequestRules
{
    /// <summary>Length is measured after trimming, so a title of spaces never passes.</summary>
    public static IRuleBuilderOptions<T, string> TrimmedLength<T>(this IRuleBuilder<T, string> rule, int min, int max) =>
        rule.Must(value => value is not null && value.Trim().Length >= min && value.Trim().Length <= max)
            .WithMessage($"'{{PropertyName}}' must be between {min} and {max} characters.");
}

public abstract class JobRequestValidator<T> : AbstractValidator<T> where T : IJobRequestFields
{
    public const decimal MaxBudget = 10_000_000m;

    protected JobRequestValidator()
    {
        RuleFor(x => x.Title).TrimmedLength(5, 150);
        RuleFor(x => x.Description).TrimmedLength(20, 4000);
        RuleFor(x => x.Address).TrimmedLength(3, 300);
        RuleFor(x => x.RegionId).GreaterThan(0);
        RuleFor(x => x.CityId).GreaterThan(0);

        RuleFor(x => x.BudgetMin).InclusiveBetween(0m, MaxBudget).PrecisionScale(12, 2, true).When(x => x.BudgetMin.HasValue);
        RuleFor(x => x.BudgetMax).InclusiveBetween(0m, MaxBudget).PrecisionScale(12, 2, true).When(x => x.BudgetMax.HasValue);
        RuleFor(x => x.BudgetMax)
            .Must((request, max) => max is null || request.BudgetMin is null || max >= request.BudgetMin)
            .WithMessage("The maximum budget cannot be less than the minimum.");

        RuleFor(x => x.PreferredTime)
            .Null()
            .When(x => x.PreferredDate is null)
            .WithMessage("Choose a date as well as a time.");
    }
}

public sealed class CreateJobRequestValidator : JobRequestValidator<CreateJobRequest>
{
    public CreateJobRequestValidator()
    {
        RuleFor(x => x.ServiceId).NotEmpty();
    }
}

public sealed class UpdateJobRequestValidator : JobRequestValidator<UpdateJobRequest>
{
}

public sealed class CancelJobRequestValidator : AbstractValidator<CancelJobRequest>
{
    public CancelJobRequestValidator()
    {
        RuleFor(x => x.Reason).MaximumLength(500);
    }
}