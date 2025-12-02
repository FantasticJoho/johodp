namespace Johodp.Api.Middleware;

using System.Diagnostics;

/// <summary>
/// Middleware for logging HTTP requests and responses with timing
/// Captures all HTTP traffic including routing, serialization, and controller execution
/// </summary>
public class RequestLoggingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<RequestLoggingMiddleware> _logger;

    public RequestLoggingMiddleware(RequestDelegate next, ILogger<RequestLoggingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var request = context.Request;
        var requestId = Guid.NewGuid().ToString("N")[..8]; // Short request ID for correlation
        
        // Log incoming request
        _logger.LogInformation(
            "[{RequestId}] HTTP {Method} {Path}{QueryString} started",
            requestId,
            request.Method,
            request.Path,
            request.QueryString);

        var sw = Stopwatch.StartNew();
        
        try
        {
            await _next(context);
            sw.Stop();

            var statusCode = context.Response.StatusCode;
            var logLevel = statusCode >= 500 ? LogLevel.Error
                         : statusCode >= 400 ? LogLevel.Warning
                         : LogLevel.Information;

            _logger.Log(
                logLevel,
                "[{RequestId}] HTTP {Method} {Path}{QueryString} completed with status {StatusCode} in {ElapsedMs}ms",
                requestId,
                request.Method,
                request.Path,
                request.QueryString,
                statusCode,
                sw.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            sw.Stop();
            
            _logger.LogError(
                ex,
                "[{RequestId}] HTTP {Method} {Path}{QueryString} failed after {ElapsedMs}ms",
                requestId,
                request.Method,
                request.Path,
                request.QueryString,
                sw.ElapsedMilliseconds);
            
            throw;
        }
    }
}

/// <summary>
/// Extension methods for registering middleware
/// </summary>
public static class RequestLoggingMiddlewareExtensions
{
    public static IApplicationBuilder UseRequestLogging(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<RequestLoggingMiddleware>();
    }
}
