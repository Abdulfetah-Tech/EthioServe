namespace EthioServe.Application.Auth;

/// <summary>Single source for the password rules: used by the validators and by the Identity options.</summary>
public static class PasswordPolicy
{
    public const int MinLength = 10;

    // Upper bound stops absurdly long inputs from burning CPU in the password hasher.
    public const int MaxLength = 128;
}