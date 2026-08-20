using System.Text.Json;

namespace SecureStatements.Api.Middleware;

// Catches anything that blows up, logs the full detail server-side, and returns a generic 500 so we never leak stack traces or internals to callers.
public sealed class GlobalExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<GlobalExceptionMiddleware> _logger;

    public GlobalExceptionMiddleware(
        RequestDelegate next, ILogger<GlobalExceptionMiddleware> logger)
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
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
        {
            // Client disconnected; nothing to return.
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex, "Unhandled exception processing {Method} {Path}",
                context.Request.Method, context.Request.Path);

            if (context.Response.HasStarted)
            {
                // Response already going out.... can't rewrite it, so let it bubble up.
                throw;
            }

            context.Response.Clear();
            context.Response.StatusCode = StatusCodes.Status500InternalServerError;
            context.Response.ContentType = "application/problem+json";

            var payload = JsonSerializer.Serialize(new
            {
                type = "about:blank",
                title = "An unexpected error occurred.",
                status = 500
            });

            await context.Response.WriteAsync(payload);
        }
    }
}