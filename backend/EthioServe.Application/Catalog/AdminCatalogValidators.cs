using EthioServe.Domain.Enums;
using FluentValidation;

namespace EthioServe.Application.Catalog;

public sealed class CategoryRequestValidator : AbstractValidator<CategoryRequest>
{
    public CategoryRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Description).MaximumLength(500);
        RuleFor(x => x.IconKey)
            .MaximumLength(300)
            .Matches(@"^[A-Za-z0-9_\-./]+$")
            .WithMessage("The icon key may only contain letters, digits, '_', '-', '.' and '/'.")
            .When(x => !string.IsNullOrEmpty(x.IconKey));
    }
}

public sealed class CreateServiceRequestValidator : AbstractValidator<CreateServiceRequest>
{
    public CreateServiceRequestValidator()
    {
        RuleFor(x => x.CategoryId).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(150);
        RuleFor(x => x.Description).MaximumLength(1000);
        RuleFor(x => x.PricingType).IsInEnum();

        RuleFor(x => x.BasePrice).NotNull()
            .WithMessage("A base price is required unless the price is negotiable.")
            .When(x => x.PricingType != PricingType.Negotiable);
        RuleFor(x => x.BasePrice).Null()
            .WithMessage("A negotiable service cannot have a base price.")
            .When(x => x.PricingType == PricingType.Negotiable);
        RuleFor(x => x.BasePrice)
            .GreaterThanOrEqualTo(0m)
            .LessThanOrEqualTo(10_000_000m)
            .PrecisionScale(12, 2, true)
            .When(x => x.BasePrice.HasValue);
    }
}

public sealed class UpdateServiceRequestValidator : AbstractValidator<UpdateServiceRequest>
{
    public UpdateServiceRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(150);
        RuleFor(x => x.Description).MaximumLength(1000);
        RuleFor(x => x.PricingType).IsInEnum();

        RuleFor(x => x.BasePrice).NotNull()
            .WithMessage("A base price is required unless the price is negotiable.")
            .When(x => x.PricingType != PricingType.Negotiable);
        RuleFor(x => x.BasePrice).Null()
            .WithMessage("A negotiable service cannot have a base price.")
            .When(x => x.PricingType == PricingType.Negotiable);
        RuleFor(x => x.BasePrice)
            .GreaterThanOrEqualTo(0m)
            .LessThanOrEqualTo(10_000_000m)
            .PrecisionScale(12, 2, true)
            .When(x => x.BasePrice.HasValue);
    }
}