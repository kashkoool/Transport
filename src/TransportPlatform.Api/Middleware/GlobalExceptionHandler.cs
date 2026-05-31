using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using TransportPlatform.Application.Common;
using TransportPlatform.Domain.Common;

namespace TransportPlatform.Api.Middleware;

/// <summary>
/// Translates exceptions into RFC 7807 ProblemDetails. Domain/validation/conflict errors
/// become safe 4xx responses with a stable error code; anything else is a 500 with no
/// internal details leaked to the client (but fully logged server-side).
/// </summary>
public sealed class GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    // Cached, allocation-free logging delegates (satisfies CA1848).
    private static readonly Action<ILogger, Exception?> LogUnhandled =
        LoggerMessage.Define(LogLevel.Error, new EventId(1, nameof(LogUnhandled)), "Unhandled exception");

    private static readonly Action<ILogger, string, string, Exception?> LogHandled =
        LoggerMessage.Define<string, string>(
            LogLevel.Information, new EventId(2, nameof(LogHandled)), "Handled {Code}: {Message}");

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        var (status, code, detail) = exception switch
        {
            DomainException dex => (StatusCodes.Status400BadRequest, dex.Code, dex.Message),
            ConflictException cex => (StatusCodes.Status409Conflict, cex.Code, cex.Message),
            NotFoundException nex => (StatusCodes.Status404NotFound, nex.Code, nex.Message),
            UnauthorizedException aex => (StatusCodes.Status401Unauthorized, aex.Code, aex.Message),
            FluentValidation.ValidationException vex => (StatusCodes.Status400BadRequest, "validation_failed", vex.Message),
            _ => (StatusCodes.Status500InternalServerError, "internal_error", "An unexpected error occurred."),
        };

        if (status >= 500)
            LogUnhandled(logger, exception);
        else
            LogHandled(logger, code, exception.Message, null);

        var problem = new ProblemDetails
        {
            Status = status,
            Title = code,
            Detail = detail,
            Instance = httpContext.Request.Path,
        };
        problem.Extensions["code"] = code;
        problem.Extensions["traceId"] = httpContext.TraceIdentifier;

        httpContext.Response.StatusCode = status;
        await httpContext.Response.WriteAsJsonAsync(problem, cancellationToken);
        return true;
    }
}
