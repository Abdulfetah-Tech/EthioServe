using EthioServe.Application.Auth;
using EthioServe.Domain.Entities;
using FluentValidation;

namespace EthioServe.Application.Providers;

public sealed class UpdateProviderProfileRequestValidator : AbstractValidator<UpdateProviderProfileRequest>
{
    public UpdateProviderProfileRequestValidator()
    {
        RuleFor(x => x.BusinessName).NotEmpty().MaximumLength(150);
        RuleFor(x => x.Description).NotNull().MaximumLength(2000);
        RuleFor(x => x.PhoneNumber).ValidPhone();
        RuleFor(x => x.YearsOfExperience).InclusiveBetween(0, ProviderProfile.MaxYearsOfExperience);
        RuleFor(x => x.RegionId).GreaterThan(0);
        RuleFor(x => x.CityId).GreaterThan(0);
        RuleFor(x => x.SubCityId).GreaterThan(0).When(x => x.SubCityId.HasValue);
        RuleFor(x => x.Address).NotNull().MaximumLength(300);

        RuleFor(x => x.Latitude).InclusiveBetween(-90m, 90m).PrecisionScale(9, 6, true).When(x => x.Latitude.HasValue);
        RuleFor(x => x.Longitude).InclusiveBetween(-180m, 180m).PrecisionScale(9, 6, true).When(x => x.Longitude.HasValue);
        RuleFor(x => x.Longitude)
            .Must((request, longitude) => request.Latitude.HasValue == longitude.HasValue)
            .WithMessage("Latitude and longitude must be given together.");
    }
}

public sealed class AddOfferRequestValidator : AbstractValidator<AddOfferRequest>
{
    public AddOfferRequestValidator()
    {
        RuleFor(x => x.ServiceId).NotEmpty();
        RuleFor(x => x.Price).InclusiveBetween(0m, 10_000_000m).PrecisionScale(12, 2, true);
        RuleFor(x => x.Description).MaximumLength(1000);
    }
}

public sealed class UpdateOfferRequestValidator : AbstractValidator<UpdateOfferRequest>
{
    public UpdateOfferRequestValidator()
    {
        RuleFor(x => x.Price).InclusiveBetween(0m, 10_000_000m).PrecisionScale(12, 2, true);
        RuleFor(x => x.Description).MaximumLength(1000);
    }
}