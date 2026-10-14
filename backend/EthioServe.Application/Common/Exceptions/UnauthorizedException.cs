namespace EthioServe.Application.Common.Exceptions;

/// <summary>Authentication failed (HTTP 401). Messages stay generic: never reveal which part was wrong.</summary>
public sealed class UnauthorizedException(string message) : Exception(message);