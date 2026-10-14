using EthioServe.Application.Auth;

namespace EthioServe.Tests.Unit;

public sealed class AuthValidatorTests
{
    private static readonly RegisterCustomerRequestValidator CustomerValidator = new();
    private static readonly LoginRequestValidator LoginValidator = new();

    private static RegisterCustomerRequest Valid() =>
        new("abebe@test.local", "Str0ngPassw0rd", "Abebe", "Kebede", "0911223344");

    [Fact]
    public void A_valid_request_passes()
    {
        Assert.True(CustomerValidator.Validate(Valid()).IsValid);
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-an-email")]
    [InlineData("abebe@")]
    public void Invalid_emails_are_rejected(string email)
    {
        Assert.False(CustomerValidator.Validate(Valid() with { Email = email }).IsValid);
    }

    [Theory]
    [InlineData("short1A")]
    [InlineData("alllowercase123")]
    [InlineData("ALLUPPERCASE123")]
    [InlineData("NoDigitsHereAtAll")]
    public void Weak_passwords_are_rejected(string password)
    {
        var result = CustomerValidator.Validate(Valid() with { Password = password });

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(RegisterCustomerRequest.Password));
    }

    [Fact]
    public void Phone_numbers_with_letters_are_rejected()
    {
        Assert.False(CustomerValidator.Validate(Valid() with { PhoneNumber = "09112abc44" }).IsValid);
    }

    [Fact]
    public void Login_accepts_any_non_empty_password_so_the_policy_is_not_leaked()
    {
        Assert.True(LoginValidator.Validate(new LoginRequest("abebe@test.local", "x")).IsValid);
        Assert.False(LoginValidator.Validate(new LoginRequest("abebe@test.local", "")).IsValid);
    }
}