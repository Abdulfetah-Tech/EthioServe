using EthioServe.Application.Providers;

namespace EthioServe.Tests.Unit;

public sealed class ProviderValidatorTests
{
    private static readonly UpdateProviderProfileRequestValidator Profile = new();
    private static readonly AddOfferRequestValidator Add = new();
    private static readonly UpdateOfferRequestValidator Update = new();

    private static UpdateProviderProfileRequest ValidProfile() =>
        new("Abebe Plumbing", "Experienced plumber.", "0911556677", 10, 1, 1, null, "Bole", 9.0192m, 38.7525m);

    [Fact]
    public void A_valid_profile_passes()
    {
        Assert.True(Profile.Validate(ValidProfile()).IsValid);
    }

    [Fact]
    public void Coordinates_are_optional_but_must_come_together()
    {
        Assert.True(Profile.Validate(ValidProfile() with { Latitude = null, Longitude = null }).IsValid);
        Assert.False(Profile.Validate(ValidProfile() with { Longitude = null }).IsValid);
        Assert.False(Profile.Validate(ValidProfile() with { Latitude = null }).IsValid);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void The_business_name_is_required(string name)
    {
        Assert.False(Profile.Validate(ValidProfile() with { BusinessName = name }).IsValid);
    }

    [Fact]
    public void Text_fields_have_length_limits()
    {
        Assert.False(Profile.Validate(ValidProfile() with { BusinessName = new string('x', 151) }).IsValid);
        Assert.False(Profile.Validate(ValidProfile() with { Description = new string('x', 2001) }).IsValid);
        Assert.False(Profile.Validate(ValidProfile() with { Address = new string('x', 301) }).IsValid);
    }

    [Theory]
    [InlineData("09112abc44")]
    [InlineData("091 122 3344")]
    [InlineData("123")]
    public void Phone_numbers_must_follow_the_pattern(string phone)
    {
        Assert.False(Profile.Validate(ValidProfile() with { PhoneNumber = phone }).IsValid);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(61)]
    public void Years_of_experience_must_be_between_0_and_60(int years)
    {
        Assert.False(Profile.Validate(ValidProfile() with { YearsOfExperience = years }).IsValid);
    }

    [Theory]
    [InlineData(95.0, 38.0)]
    [InlineData(9.0, 190.0)]
    [InlineData(9.1234567, 38.0)]
    public void Coordinates_must_be_in_range_with_limited_precision(double latitude, double longitude)
    {
        Assert.False(Profile.Validate(ValidProfile() with { Latitude = (decimal)latitude, Longitude = (decimal)longitude }).IsValid);
    }

    [Theory]
    [InlineData(0, true)]
    [InlineData(150.50, true)]
    [InlineData(-1, false)]
    [InlineData(12.345, false)]
    [InlineData(20000000, false)]
    public void Offer_prices_must_be_sensible(double price, bool expectedValid)
    {
        var amount = (decimal)price;

        Assert.Equal(expectedValid, Add.Validate(new AddOfferRequest(Guid.NewGuid(), amount, null)).IsValid);
        Assert.Equal(expectedValid, Update.Validate(new UpdateOfferRequest(amount, null, true)).IsValid);
    }

    [Fact]
    public void Adding_an_offer_needs_a_service()
    {
        Assert.False(Add.Validate(new AddOfferRequest(Guid.Empty, 100m, null)).IsValid);
    }

    [Fact]
    public void Offer_descriptions_have_a_length_limit()
    {
        Assert.False(Add.Validate(new AddOfferRequest(Guid.NewGuid(), 100m, new string('x', 1001))).IsValid);
    }
}