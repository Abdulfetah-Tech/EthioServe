namespace EthioServe.Domain.Common;

/// <summary>Thrown when a business rule is violated (maps to HTTP 422).</summary>
public class DomainException(string message) : Exception(message);