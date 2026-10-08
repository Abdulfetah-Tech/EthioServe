namespace EthioServe.Application.Common.Exceptions;

/// <summary>Resource does not exist OR the caller is not allowed to know it exists (HTTP 404).</summary>
public sealed class NotFoundException(string message) : Exception(message);