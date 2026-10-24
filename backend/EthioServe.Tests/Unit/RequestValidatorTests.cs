using EthioServe.Application.Requests;

namespace EthioServe.Tests.Unit;

public sealed class RequestValidatorTests
{
    private static readonly CreateJobRequestValidator Create = new();
    private static readonly UpdateJobRequestValidator Update = new();
    private static readonly CancelJobRequestValidator Cancel = new();

    private static CreateJobRequest Valid() =>
        new(Guid.NewGuid(), "Leaking kitchen pipe", new string('x', 30), "Bole", 1, 1, null, null, null, null);

    private static bool HasError(CreateJobRequest request, string property) =>
        Create.Validate(request).Errors.Any(e => e.PropertyName == property);

    [Fact]
    public void A_valid_request_passes()
    {
        Assert.True(Create.Validate(Valid()).IsValid);
    }

    [Theory]
    [InlineData("Pipe", false)]
    [InlineData("  Pipe  ", false)]
    [InlineData("Pipes", true)]
    public void The_title_needs_at_least_five_characters_after_trimming(string title, bool expectedValid)
    {
        Assert.Equal(expectedValid, !HasError(Valid() with { Title = title }, "Title"));
    }

    [Fact]
    public void The_title_is_limited_to_150_characters()
    {
        Assert.False(HasError(Valid() with { Title = new string('x', 150) }, "Title"));
        Assert.True(HasError(Valid() with { Title = new string('x', 151) }, "Title"));
    }

    [Theory]
    [InlineData(19, false)]
    [InlineData(20, true)]
    [InlineData(4000, true)]
    [InlineData(4001, false)]
    public void The_description_must_be_20_to_4000_characters(int length, bool expectedValid)
    {
        Assert.Equal(expectedValid, !HasError(Valid() with { Description = new string('x', length) }, "Description"));
    }

    [Theory]
    [InlineData(2, false)]
    [InlineData(3, true)]
    [InlineData(300, true)]
    [InlineData(301, false)]
    public void The_address_must_be_3_to_300_characters(int length, bool expectedValid)
    {
        Assert.Equal(expectedValid, !HasError(Valid() with { Address = new string('x', length) }, "Address"));
    }

    [Fact]
    public void A_service_and_a_location_are_required()
    {
        Assert.True(HasError(Valid() with { ServiceId = Guid.Empty }, "ServiceId"));
        Assert.True(HasError(Valid() with { RegionId = 0 }, "RegionId"));
        Assert.True(HasError(Valid() with { CityId = 0 }, "CityId"));
    }

    [Theory]
    [InlineData(null, null, true)]
    [InlineData(0.0, 0.0, true)]
    [InlineData(100.0, null, true)]
    [InlineData(null, 500.0, true)]
    [InlineData(100.0, 500.0, true)]
    [InlineData(-1.0, null, false)]
    [InlineData(null, 10000001.0, false)]
    [InlineData(500.0, 100.0, false)]
    [InlineData(12.345, null, false)]
    public void Budgets_must_be_sensible(double? min, double? max, bool expectedValid)
    {
        var request = Valid() with { BudgetMin = (decimal?)min, BudgetMax = (decimal?)max };

        Assert.Equal(expectedValid, Create.Validate(request).IsValid);
    }

    [Fact]
    public void A_time_needs_a_date()
    {
        var date = new DateOnly(2026, 12, 1);
        var time = new TimeOnly(14, 30);

        Assert.True(HasError(Valid() with { PreferredTime = time }, "PreferredTime"));
        Assert.True(Create.Validate(Valid() with { PreferredDate = date, PreferredTime = time }).IsValid);
        Assert.True(Create.Validate(Valid() with { PreferredDate = date }).IsValid);
    }

    [Fact]
    public void Editing_follows_the_same_rules()
    {
        var valid = new UpdateJobRequest("Leaking kitchen pipe", new string('x', 30), "Bole", 1, 1, null, null, null, null);

        Assert.True(Update.Validate(valid).IsValid);
        Assert.False(Update.Validate(valid with { Title = "Pipe" }).IsValid);
        Assert.False(Update.Validate(valid with { BudgetMin = 900m, BudgetMax = 100m }).IsValid);
        Assert.False(Update.Validate(valid with { PreferredTime = new TimeOnly(9, 0) }).IsValid);
    }

    [Fact]
    public void A_cancellation_reason_is_optional_but_limited_to_500_characters()
    {
        Assert.True(Cancel.Validate(new CancelJobRequest(null)).IsValid);
        Assert.True(Cancel.Validate(new CancelJobRequest(new string('x', 500))).IsValid);
        Assert.False(Cancel.Validate(new CancelJobRequest(new string('x', 501))).IsValid);
    }
}