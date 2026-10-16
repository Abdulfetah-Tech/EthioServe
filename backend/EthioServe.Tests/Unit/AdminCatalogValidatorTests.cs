using EthioServe.Application.Catalog;
using EthioServe.Domain.Enums;

namespace EthioServe.Tests.Unit;

public sealed class AdminCatalogValidatorTests
{
    private static readonly CreateServiceRequestValidator CreateValidator = new();
    private static readonly UpdateServiceRequestValidator UpdateValidator = new();
    private static readonly CategoryRequestValidator CategoryValidator = new();

    public static IEnumerable<object?[]> ValidPricing()
    {
        yield return new object?[] { PricingType.Fixed, 150.50m };
        yield return new object?[] { PricingType.Hourly, 200m };
        yield return new object?[] { PricingType.StartingFrom, 0m };
        yield return new object?[] { PricingType.Negotiable, null };
    }

    public static IEnumerable<object?[]> InvalidPricing()
    {
        yield return new object?[] { PricingType.Fixed, null };
        yield return new object?[] { PricingType.Hourly, null };
        yield return new object?[] { PricingType.StartingFrom, null };
        yield return new object?[] { PricingType.Negotiable, 100m };
        yield return new object?[] { PricingType.Fixed, -5m };
        yield return new object?[] { PricingType.Fixed, 12.345m };
        yield return new object?[] { PricingType.Fixed, 20_000_000m };
    }

    private static CreateServiceRequest Create(PricingType pricing, decimal? price, string name = "Pipe repair") =>
        new(Guid.NewGuid(), name, null, price, pricing);

    private static UpdateServiceRequest Update(PricingType pricing, decimal? price) =>
        new("Pipe repair", null, price, pricing);

    [Theory]
    [MemberData(nameof(ValidPricing))]
    public void Valid_pricing_combinations_pass(PricingType pricing, decimal? price)
    {
        Assert.True(CreateValidator.Validate(Create(pricing, price)).IsValid);
        Assert.True(UpdateValidator.Validate(Update(pricing, price)).IsValid);
    }

    [Theory]
    [MemberData(nameof(InvalidPricing))]
    public void Invalid_pricing_combinations_fail_on_the_price(PricingType pricing, decimal? price)
    {
        var created = CreateValidator.Validate(Create(pricing, price));
        var updated = UpdateValidator.Validate(Update(pricing, price));

        Assert.Contains(created.Errors, e => e.PropertyName == nameof(CreateServiceRequest.BasePrice));
        Assert.Contains(updated.Errors, e => e.PropertyName == nameof(UpdateServiceRequest.BasePrice));
    }

    [Fact]
    public void Unknown_pricing_types_are_rejected()
    {
        var result = CreateValidator.Validate(Create((PricingType)99, 10m));

        Assert.Contains(result.Errors, e => e.PropertyName == nameof(CreateServiceRequest.PricingType));
    }

    [Fact]
    public void Service_names_must_not_be_blank_or_too_long()
    {
        Assert.False(CreateValidator.Validate(Create(PricingType.Fixed, 10m, name: "  ")).IsValid);
        Assert.False(CreateValidator.Validate(Create(PricingType.Fixed, 10m, name: new string('x', 151))).IsValid);
    }

    [Theory]
    [InlineData("", false)]
    [InlineData("   ", false)]
    [InlineData("Plumbing", true)]
    public void Category_names_must_not_be_blank(string name, bool expectedValid)
    {
        Assert.Equal(expectedValid, CategoryValidator.Validate(new CategoryRequest(name, null, null)).IsValid);
    }

    [Fact]
    public void Category_names_and_descriptions_have_length_limits()
    {
        Assert.False(CategoryValidator.Validate(new CategoryRequest(new string('x', 101), null, null)).IsValid);
        Assert.False(CategoryValidator.Validate(new CategoryRequest("Plumbing", new string('x', 501), null)).IsValid);
    }

    [Theory]
    [InlineData("icons/plumbing.svg", true)]
    [InlineData("plumbing_icon-2", true)]
    [InlineData("has spaces.svg", false)]
    [InlineData("<script>.svg", false)]
    public void Icon_keys_are_restricted_to_safe_characters(string iconKey, bool expectedValid)
    {
        Assert.Equal(expectedValid, CategoryValidator.Validate(new CategoryRequest("Plumbing", null, iconKey)).IsValid);
    }
}