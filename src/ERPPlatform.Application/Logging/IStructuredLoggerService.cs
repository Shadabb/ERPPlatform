using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;

namespace ERPPlatform.Logging;

public interface IStructuredLoggerService
{
    void LogInformation(string messageTemplate, params object[] propertyValues);
    void LogInformationWithProperties(string messageTemplate, Dictionary<string, object> properties);
    void LogWarning(string messageTemplate, params object[] propertyValues);
    void LogWarningWithProperties(string messageTemplate, Dictionary<string, object> properties);
    void LogError(Exception exception, string messageTemplate, params object[] propertyValues);
    void LogErrorWithProperties(Exception exception, string messageTemplate, Dictionary<string, object> properties);
    void LogDebug(string messageTemplate, params object[] propertyValues);
    void LogCritical(Exception exception, string messageTemplate, params object[] propertyValues);
    
    // Business operation logging
    void LogBusinessOperation(string operation, string entityType, object entityId, string? userId, Dictionary<string, object>? additionalProperties = null);
    void LogUserActivity(string? userId, string action, string details, Dictionary<string, object>? context = null);
    void LogPerformance(string operation, TimeSpan duration, Dictionary<string, object>? context = null);
    void LogSecurityEvent(string eventType, string? userId, string details, Dictionary<string, object>? context = null);
}