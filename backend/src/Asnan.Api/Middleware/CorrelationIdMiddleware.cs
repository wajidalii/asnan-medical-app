using Serilog.Context;

namespace Asnan.Api.Middleware;

public class CorrelationIdMiddleware
{
    private const string HeaderName = "X-Trace-Id";

    private readonly RequestDelegate _next;

    public CorrelationIdMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var traceId = context.TraceIdentifier;

        context.Response.OnStarting(() =>
        {
            context.Response.Headers[HeaderName] = traceId;
            return Task.CompletedTask;
        });

        using (LogContext.PushProperty("TraceId", traceId))
        {
            await _next(context);
        }
    }
}
