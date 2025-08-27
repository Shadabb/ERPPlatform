using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Volo.Abp.Users;

namespace ERPPlatform.Web.Middleware;

/// <summary>
/// Middleware to enrich Serilog with structured HTTP request data
/// </summary>
public class StructuredLoggingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<StructuredLoggingMiddleware> _logger;

    public StructuredLoggingMiddleware(RequestDelegate next, ILogger<StructuredLoggingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context, ICurrentUser currentUser)
    {
        // Skip logging for static files and health checks
        if (ShouldSkipLogging(context.Request.Path))
        {
            await _next(context);
            return;
        }

        var stopwatch = Stopwatch.StartNew();
        var requestId = context.TraceIdentifier;
        var correlationId = Guid.NewGuid().ToString("N");

        // Enrich log context with request information
        using var logContext = _logger.BeginScope(new Dictionary<string, object?>
        {
            ["RequestId"] = requestId,
            ["CorrelationId"] = correlationId,
            ["UserId"] = currentUser.Id?.ToString(),
            ["HttpMethod"] = context.Request.Method,
            ["RequestPath"] = context.Request.Path.Value,
            ["UserAgent"] = context.Request.Headers["User-Agent"].ToString(),
            ["ClientIp"] = GetClientIpAddress(context),
            ["RequestTime"] = DateTimeOffset.UtcNow
        });

        // Log request start
        _logger.LogInformation("HTTP {HttpMethod} {RequestPath} started for user {UserId}",
            context.Request.Method, 
            context.Request.Path.Value,
            currentUser.Id?.ToString() ?? "Anonymous");

        try
        {
            await _next(context);
            stopwatch.Stop();

            // Log successful request completion
            LogRequestCompletion(context, stopwatch.ElapsedMilliseconds, currentUser.Id?.ToString());
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            
            // Log request failure
            _logger.LogError(ex, 
                "HTTP {HttpMethod} {RequestPath} failed after {Duration}ms with error: {ErrorMessage}",
                context.Request.Method,
                context.Request.Path.Value,
                stopwatch.ElapsedMilliseconds,
                ex.Message);

            // Add error details to log context
            using var errorContext = _logger.BeginScope(new Dictionary<string, object?>
            {
                ["ExceptionType"] = ex.GetType().Name,
                ["ExceptionMessage"] = ex.Message,
                ["StackTrace"] = ex.StackTrace,
                ["Duration"] = stopwatch.ElapsedMilliseconds,
                ["ResponseStatusCode"] = 500
            });

            throw;
        }
    }

    private void LogRequestCompletion(HttpContext context, long durationMs, string? userId)
    {
        var statusCode = context.Response.StatusCode;
        var logLevel = GetLogLevelForStatusCode(statusCode, durationMs);

        // Add response details to log context
        using var responseContext = _logger.BeginScope(new Dictionary<string, object?>
        {
            ["Duration"] = durationMs,
            ["ResponseStatusCode"] = statusCode,
            ["ContentLength"] = context.Response.ContentLength,
            ["ContentType"] = context.Response.ContentType
        });

        var message = "HTTP {HttpMethod} {RequestPath} responded {StatusCode} in {Duration}ms for user {UserId}";
        
        _logger.Log(logLevel, message,
            context.Request.Method,
            context.Request.Path.Value,
            statusCode,
            durationMs,
            userId ?? "Anonymous");

        // Log performance warnings for slow requests
        if (durationMs > 5000) // 5 seconds
        {
            _logger.LogWarning("Slow request detected: {HttpMethod} {RequestPath} took {Duration}ms",
                context.Request.Method,
                context.Request.Path.Value,
                durationMs);
        }

        // Log error responses
        if (statusCode >= 400)
        {
            var errorLevel = statusCode >= 500 ? LogLevel.Error : LogLevel.Warning;
            _logger.Log(errorLevel, "HTTP error response: {StatusCode} for {HttpMethod} {RequestPath}",
                statusCode,
                context.Request.Method,
                context.Request.Path.Value);
        }
    }

    private static LogLevel GetLogLevelForStatusCode(int statusCode, long durationMs)
    {
        // Slow requests are warnings regardless of status
        if (durationMs > 5000) return LogLevel.Warning;

        return statusCode switch
        {
            >= 500 => LogLevel.Error,      // Server errors
            >= 400 => LogLevel.Warning,    // Client errors  
            >= 300 => LogLevel.Information, // Redirects
            _ => LogLevel.Information       // Success
        };
    }

    private static bool ShouldSkipLogging(PathString path)
    {
        var pathValue = path.Value?.ToLowerInvariant() ?? "";
        
        return pathValue.Contains("/health") ||
               pathValue.Contains("/metrics") ||
               pathValue.Contains("/swagger") ||
               pathValue.Contains("/.well-known") ||
               pathValue.Contains("/favicon.ico") ||
               pathValue.EndsWith(".css") ||
               pathValue.EndsWith(".js") ||
               pathValue.EndsWith(".map") ||
               pathValue.EndsWith(".ico") ||
               pathValue.EndsWith(".png") ||
               pathValue.EndsWith(".jpg") ||
               pathValue.EndsWith(".gif");
    }

    private static string GetClientIpAddress(HttpContext context)
    {
        // Check for forwarded IP first (for load balancers/proxies)
        var forwardedFor = context.Request.Headers["X-Forwarded-For"].FirstOrDefault();
        if (!string.IsNullOrEmpty(forwardedFor))
        {
            return forwardedFor.Split(',')[0].Trim();
        }

        var realIp = context.Request.Headers["X-Real-IP"].FirstOrDefault();
        if (!string.IsNullOrEmpty(realIp))
        {
            return realIp;
        }

        return context.Connection.RemoteIpAddress?.ToString() ?? "Unknown";
    }
}