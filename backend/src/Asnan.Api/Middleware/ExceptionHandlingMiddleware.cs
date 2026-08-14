using Asnan.Domain.Exceptions;
using Microsoft.AspNetCore.Mvc;

namespace Asnan.Api.Middleware;

/// <summary>
/// Maps all unhandled exceptions to a consistent RFC 7807 ProblemDetails envelope.
/// Domain-specific exceptions should be mapped to specific status codes here as they're introduced;
/// anything unrecognized falls back to 500 with no internal detail leaked to the client.
/// </summary>
public class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (InvalidAppointmentTransitionException ex)
        {
            _logger.LogWarning(ex, "Invalid appointment transition attempted processing {Method} {Path}", context.Request.Method, context.Request.Path);

            await WriteProblemAsync(context, StatusCodes.Status409Conflict, "This appointment cannot make that transition.", "https://tools.ietf.org/html/rfc7231#section-6.5.8");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception processing {Method} {Path}", context.Request.Method, context.Request.Path);

            await WriteProblemAsync(context, StatusCodes.Status500InternalServerError, "An unexpected error occurred.", "https://tools.ietf.org/html/rfc7231#section-6.6.1");
        }
    }

    private static async Task WriteProblemAsync(HttpContext context, int statusCode, string title, string type)
    {
        var problem = new ProblemDetails
        {
            Status = statusCode,
            Title = title,
            Type = type,
            Instance = context.Request.Path,
        };
        problem.Extensions["traceId"] = context.TraceIdentifier;

        context.Response.ContentType = "application/problem+json";
        context.Response.StatusCode = statusCode;

        await context.Response.WriteAsJsonAsync(problem);
    }
}
