using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Users;

namespace ERPPlatform.Logging;

public class StructuredLoggerService : IStructuredLoggerService, ITransientDependency
{
    private readonly ILogger<StructuredLoggerService> _logger;
    private readonly ICurrentUser _currentUser;

    public StructuredLoggerService(ILogger<StructuredLoggerService> logger, ICurrentUser currentUser)
    {
        _logger = logger;
        _currentUser = currentUser;
    }

    public void LogInformation(string messageTemplate, params object[] propertyValues)
    {
        _logger.LogInformation(messageTemplate, propertyValues);
    }

    public void LogInformationWithProperties(string messageTemplate, Dictionary<string, object> properties)
    {
        using var scope = CreateLogScope(properties);
        _logger.LogInformation(messageTemplate);
    }

    public void LogWarning(string messageTemplate, params object[] propertyValues)
    {
        _logger.LogWarning(messageTemplate, propertyValues);
    }

    public void LogWarningWithProperties(string messageTemplate, Dictionary<string, object> properties)
    {
        using var scope = CreateLogScope(properties);
        _logger.LogWarning(messageTemplate);
    }

    public void LogError(Exception exception, string messageTemplate, params object[] propertyValues)
    {
        _logger.LogError(exception, messageTemplate, propertyValues);
    }

    public void LogErrorWithProperties(Exception exception, string messageTemplate, Dictionary<string, object> properties)
    {
        using var scope = CreateLogScope(properties);
        _logger.LogError(exception, messageTemplate);
    }

    public void LogDebug(string messageTemplate, params object[] propertyValues)
    {
        _logger.LogDebug(messageTemplate, propertyValues);
    }

    public void LogCritical(Exception exception, string messageTemplate, params object[] propertyValues)
    {
        _logger.LogCritical(exception, messageTemplate, propertyValues);
    }

    public void LogBusinessOperation(string operation, string entityType, object entityId, string? userId, Dictionary<string, object>? additionalProperties = null)
    {
        var properties = new Dictionary<string, object>
        {
            ["Operation"] = operation,
            ["EntityType"] = entityType,
            ["EntityId"] = entityId?.ToString(),
            ["UserId"] = userId ?? _currentUser.Id?.ToString(),
            ["UserName"] = _currentUser.UserName,
            ["TenantId"] = _currentUser.TenantId?.ToString(),
            ["Timestamp"] = DateTimeOffset.UtcNow,
            ["Category"] = "BusinessOperation"
        };

        if (additionalProperties != null)
        {
            foreach (var prop in additionalProperties)
            {
                properties[prop.Key] = prop.Value;
            }
        }

        LogInformationWithProperties("Business operation {Operation} performed on {EntityType} {EntityId} by user {UserId}", properties);
    }

    public void LogUserActivity(string? userId, string action, string details, Dictionary<string, object>? context = null)
    {
        var properties = new Dictionary<string, object>
        {
            ["UserId"] = userId ?? _currentUser.Id?.ToString(),
            ["UserName"] = _currentUser.UserName,
            ["Action"] = action,
            ["Details"] = details,
            ["TenantId"] = _currentUser.TenantId?.ToString(),
            ["Timestamp"] = DateTimeOffset.UtcNow,
            ["Category"] = "UserActivity",
            ["IPAddress"] = GetClientIPAddress(),
            ["UserAgent"] = GetUserAgent()
        };

        if (context != null)
        {
            foreach (var prop in context)
            {
                properties[prop.Key] = prop.Value;
            }
        }

        LogInformationWithProperties("User activity: {Action} - {Details} by user {UserId}", properties);
    }

    public void LogPerformance(string operation, TimeSpan duration, Dictionary<string, object>? context = null)
    {
        var properties = new Dictionary<string, object>
        {
            ["Operation"] = operation,
            ["DurationMs"] = duration.TotalMilliseconds,
            ["UserId"] = _currentUser.Id?.ToString(),
            ["TenantId"] = _currentUser.TenantId?.ToString(),
            ["Timestamp"] = DateTimeOffset.UtcNow,
            ["Category"] = "Performance"
        };

        if (context != null)
        {
            foreach (var prop in context)
            {
                properties[prop.Key] = prop.Value;
            }
        }

        var logLevel = duration.TotalMilliseconds > 5000 ? LogLevel.Warning : LogLevel.Information;
        var messageTemplate = duration.TotalMilliseconds > 5000 
            ? "SLOW OPERATION: {Operation} took {DurationMs}ms" 
            : "Operation {Operation} completed in {DurationMs}ms";

        using var scope = CreateLogScope(properties);
        _logger.Log(logLevel, messageTemplate);
    }

    public void LogSecurityEvent(string eventType, string? userId, string details, Dictionary<string, object>? context = null)
    {
        var properties = new Dictionary<string, object>
        {
            ["EventType"] = eventType,
            ["UserId"] = userId ?? _currentUser.Id?.ToString(),
            ["UserName"] = _currentUser.UserName,
            ["Details"] = details,
            ["TenantId"] = _currentUser.TenantId?.ToString(),
            ["Timestamp"] = DateTimeOffset.UtcNow,
            ["Category"] = "Security",
            ["IPAddress"] = GetClientIPAddress(),
            ["UserAgent"] = GetUserAgent()
        };

        if (context != null)
        {
            foreach (var prop in context)
            {
                properties[prop.Key] = prop.Value;
            }
        }

        LogWarningWithProperties("SECURITY EVENT: {EventType} - {Details} for user {UserId}", properties);
    }

    private IDisposable CreateLogScope(Dictionary<string, object> properties)
    {
        return _logger.BeginScope(properties);
    }

    private string GetClientIPAddress()
    {
        // In a real implementation, you would inject IHttpContextAccessor
        // and get the IP from HttpContext.Connection.RemoteIpAddress
        return "Unknown";
    }

    private string GetUserAgent()
    {
        // In a real implementation, you would inject IHttpContextAccessor
        // and get the User-Agent from HttpContext.Request.Headers
        return "Unknown";
    }
}