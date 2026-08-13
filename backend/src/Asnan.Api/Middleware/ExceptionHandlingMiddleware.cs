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
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception processing {Method} {Path}", context.Request.Method, context.Request.Path);

            var problem = new ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Title = "An unexpected error occurred.",
                Type = "https://tools.ietf.org/html/rfc7231#section-6.6.1",
                Instance = context.Request.Path,
            };
            problem.Extensions["traceId"] = context.TraceIdentifier;

            context.Response.ContentType = "application/problem+json";
            context.Response.StatusCode = problem.Status.Value;

            await context.Response.WriteAsJsonAsync(problem);
        }
    }
}
