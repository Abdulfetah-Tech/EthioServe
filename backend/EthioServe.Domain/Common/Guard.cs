namespace EthioServe.Domain.Common;

/// <summary>Small invariant checks used by entity constructors. Failures surface as HTTP 422 via DomainException.</summary>
internal static class Guard
{
    public static string NotBlank(string? value, string paramName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new DomainException($"{paramName} is required.");
        }

        return value.Trim();
    }

    public static decimal Positive(decimal value, string paramName)
    {
        if (value <= 0)
        {
            throw new DomainException($"{paramName} must be greater than zero.");
        }

        return value;
    }

    public static decimal NotNegative(decimal value, string paramName)
    {
        if (value < 0)
        {
            throw new DomainException($"{paramName} cannot be negative.");
        }

        return value;
    }

    public static int InRange(int value, int min, int max, string paramName)
    {
        if (value < min || value > max)
        {
            throw new DomainException($"{paramName} must be between {min} and {max}.");
        }

        return value;
    }
}