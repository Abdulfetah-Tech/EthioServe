using EthioServe.Application.Common.Exceptions;
using EthioServe.Domain.Common;
using FluentValidation;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace EthioServe.API.Middleware;

/// <summary>
/// Converts exceptions into RFC 7807 ProblemDetails responses.
/// Unexpected exceptions are logged in full but never exposed to the client.
/// </summary>
public sealed class GlobalExceptionHandler(
    ILogger<GlobalExceptionHandler> logger,
    IProblemDetailsService problemDetailsService) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        // Client disconnected: not an error worth a 500 and a stack trace.
        if (exception is OperationCanceledException && httpContext.RequestAborted.IsCancellationRequested)
        {
            return true;
        }

        var (status, title) = exception switch
        {
            ValidationException => (StatusCodes.Status400BadRequest, "Validation failed"),
            UnauthorizedException => (StatusCodes.Status401Unauthorized, "Authentication failed"),
            NotFoundException => (StatusCodes.Status404NotFound, "Resource not found"),
            ForbiddenException => (StatusCodes.Status403Forbidden, "Access denied"),
            ConflictException => (StatusCodes.Status409Conflict, "Conflict"),
            DomainException => (StatusCodes.Status422UnprocessableEntity, "Business rule violation"),
            _ => (StatusCodes.Status500InternalServerError, "An unexpected error occurred")
        };

        if (status == StatusCodes.Status500InternalServerError)
        {
            logger.LogError(exception, "Unhandled exception on {Method} {Path}",
                httpContext.Request.Method, httpContext.Request.Path);
        }
        else
        {
            logger.LogWarning("Handled {ExceptionType}: {Message}", exception.GetType().Name, exception.Message);
        }

        httpContext.Response.StatusCode = status;

        ProblemDetails problem = exception is ValidationException validation
            ? new HttpValidationProblemDetails(ToErrors(validation))
            {
                Status = status,
                Title = title,
                Detail = "One or more validation errors occurred."
            }
            : new ProblemDetails
            {
                Status = status,
                Title = title,
                // Our own exceptions carry safe messages; anything else gets a generic one.
                Detail = status == StatusCodes.Status500InternalServerError
                    ? "Something went wrong on our side. Please try again later."
                    : exception.Message
            };

        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails = problem
        });
    }

    private static Dictionary<string, string[]> ToErrors(ValidationException exception) =>
        exception.Errors
            .GroupBy(e => e.PropertyName)
            .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).Distinct().ToArray());
}