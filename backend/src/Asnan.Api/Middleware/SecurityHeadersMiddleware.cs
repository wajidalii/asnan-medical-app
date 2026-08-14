namespace Asnan.Api.Middleware;

/// <summary>
/// Standard security response headers (ARCHITECTURE.md §13). HSTS is handled
/// separately via <c>app.UseHsts()</c> since ASP.NET Core already ships that
/// as a dedicated, environment-gated middleware.
/// </summary>
public class SecurityHeadersMiddleware
{
    private readonly RequestDelegate _next;

    public SecurityHeadersMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        context.Response.OnStarting(() =>
        {
            var headers = context.Response.Headers;
            headers["X-Content-Type-Options"] = "nosniff";
            headers["Referrer-Policy"] = "no-referrer";
            headers["X-Frame-Options"] = "DENY";
            headers["Permissions-Policy"] = "camera=(), microphone=(), geolocation=()";
            return Task.CompletedTask;
        });

        await _next(context);
    }
}
