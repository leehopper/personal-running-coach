using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace RunCoach.Api.Infrastructure;

/// <summary>
/// Global exception handler registered via
/// <c>services.AddExceptionHandler&lt;ErrorHandlingMiddleware&gt;()</c> +
/// <c>app.UseExceptionHandler()</c>. Converts any unhandled exception into an
/// RFC 7807 <see cref="ProblemDetails"/> response with HTTP 500 and
/// <c>Content-Type: application/problem+json</c> (the content type is set by
/// <see cref="IProblemDetailsService"/>). The <c>detail</c> field is verbose
/// in Development and generic in every other environment so a stack trace
/// never leaks to a non-local client.
/// </summary>
public sealed partial class ErrorHandlingMiddleware(
    IProblemDetailsService problemDetailsService,
    IHostEnvironment environment,
    ILogger<ErrorHandlingMiddleware> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        LogUnhandledException(logger, httpContext.Request.Path, exception);

        httpContext.Response.StatusCode = StatusCodes.Status500InternalServerError;

        var problemDetails = new ProblemDetails
        {
            Status = StatusCodes.Status500InternalServerError,
            Title = "An unexpected error occurred.",
            Type = "https://tools.ietf.org/html/rfc9110#section-15.6.1",
            Detail = environment.IsDevelopment()
                ? exception.ToString()
                : "An unexpected error occurred while processing the request.",
            Instance = httpContext.Request.Path,
        };

        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails = problemDetails,
        });
    }

    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Error,
        Message = "Unhandled exception at {RequestPath}")]
    private static partial void LogUnhandledException(
        ILogger logger,
        string requestPath,
        Exception exception);
}
