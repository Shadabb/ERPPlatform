using Microsoft.Extensions.Logging;
using System;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Application.Services;
using ERPPlatform.Logging.Helpers;

namespace ERPPlatform.Logging;

/// <summary>
/// Service for logging security events following ABP standards
/// </summary>
public class SecurityEventLogAppService : ApplicationService, ISecurityEventLogAppService
{
    private readonly LoggingContextProvider _contextProvider;
    private readonly StructuredLoggerHelper _loggerHelper;

    public SecurityEventLogAppService(
        LoggingContextProvider contextProvider,
        StructuredLoggerHelper loggerHelper)
    {
        _contextProvider = contextProvider;
        _loggerHelper = loggerHelper;
    }

    public async Task LogSecurityEventAsync(SecurityEventLogDto logData)
    {
        Check.NotNull(logData, nameof(logData));

        // Validate required fields
        if (!_loggerHelper.ValidateLogEntry(logData, "EventType", "Description"))
        {
            Logger.LogWarning("Invalid security event log data provided");
            return;
        }

        try
        {
            // Enrich with context
            _contextProvider.EnrichWithContext(logData);

            // Create structured log properties
            var properties = _loggerHelper.CreateLogProperties(logData);
            
            // Add security-specific properties
            properties["EventType"] = logData.EventType;
            properties["Description"] = logData.Description;
            properties["Severity"] = logData.Severity;
            properties[LoggingConstants.PropertyNames.Category] = LoggingConstants.Categories.Security;
            properties["IsSuccessful"] = logData.IsSuccessful;
            
            if (!string.IsNullOrEmpty(logData.Resource))
            {
                properties["Resource"] = logData.Resource;
            }

            if (!string.IsNullOrEmpty(logData.FailureReason))
            {
                properties["FailureReason"] = logData.FailureReason;
            }

            // Determine log level based on severity and success
            var logLevel = GetLogLevelFromSeverity(logData.Severity, logData.IsSuccessful);

            // Log the security event
            using var scope = _loggerHelper.CreateLogScope(Logger, properties);
            
            Logger.Log(logLevel,
                "Security event {EventType} for user {UserId} - {Description} [Severity: {Severity}]",
                logData.EventType,
                logData.UserId,
                logData.Description,
                logData.Severity);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error occurred while logging security event");
        }

        await Task.CompletedTask;
    }

    public async Task LogAuthenticationSuccessAsync(string eventType, string description, string? resource = null)
    {
        var logData = new SecurityEventLogDto
        {
            EventType = Check.NotNullOrWhiteSpace(eventType, nameof(eventType)),
            Description = Check.NotNullOrWhiteSpace(description, nameof(description)),
            Resource = resource,
            Severity = "Low",
            IsSuccessful = true
        };

        await LogSecurityEventAsync(logData);
    }

    public async Task LogAuthenticationFailureAsync(string eventType, string description, string failureReason, string? resource = null)
    {
        var logData = new SecurityEventLogDto
        {
            EventType = Check.NotNullOrWhiteSpace(eventType, nameof(eventType)),
            Description = Check.NotNullOrWhiteSpace(description, nameof(description)),
            FailureReason = Check.NotNullOrWhiteSpace(failureReason, nameof(failureReason)),
            Resource = resource,
            Severity = "Medium",
            IsSuccessful = false
        };

        await LogSecurityEventAsync(logData);
    }

    public async Task LogUnauthorizedAccessAsync(string resource, string action, string reason)
    {
        var description = $"Unauthorized access attempt to {resource} - Action: {action}";
        
        var logData = new SecurityEventLogDto
        {
            EventType = LoggingConstants.SecurityEvents.UnauthorizedAccess,
            Description = description,
            FailureReason = reason,
            Resource = resource,
            Severity = "High",
            IsSuccessful = false
        };

        // Add specific properties for unauthorized access
        logData.AdditionalProperties["AttemptedAction"] = action;
        logData.AdditionalProperties["AccessDeniedReason"] = reason;

        await LogSecurityEventAsync(logData);
    }

    public async Task LogPermissionDeniedAsync(string permission, string resource, string action)
    {
        var description = $"Permission '{permission}' denied for action '{action}' on resource '{resource}'";
        
        var logData = new SecurityEventLogDto
        {
            EventType = LoggingConstants.SecurityEvents.PermissionDenied,
            Description = description,
            FailureReason = $"Missing permission: {permission}",
            Resource = resource,
            Severity = "Medium",
            IsSuccessful = false
        };

        // Add specific properties for permission denied
        logData.AdditionalProperties["RequiredPermission"] = permission;
        logData.AdditionalProperties["AttemptedAction"] = action;

        await LogSecurityEventAsync(logData);
    }

    public async Task LogSensitiveDataAccessAsync(string dataType, int recordCount, string purpose)
    {
        var description = $"Accessed {recordCount} {dataType} records for purpose: {purpose}";
        
        var logData = new SecurityEventLogDto
        {
            EventType = LoggingConstants.SecurityEvents.DataAccess,
            Description = description,
            Resource = dataType,
            Severity = "Medium",
            IsSuccessful = true
        };

        // Add specific properties for data access
        logData.AdditionalProperties["DataType"] = dataType;
        logData.AdditionalProperties["RecordCount"] = recordCount;
        logData.AdditionalProperties["AccessPurpose"] = purpose;

        await LogSecurityEventAsync(logData);
    }

    private LogLevel GetLogLevelFromSeverity(string severity, bool isSuccessful)
    {
        if (!isSuccessful)
        {
            return severity.ToUpper() switch
            {
                "HIGH" or "CRITICAL" => LogLevel.Error,
                "MEDIUM" => LogLevel.Warning,
                "LOW" => LogLevel.Information,
                _ => LogLevel.Warning
            };
        }

        return severity.ToUpper() switch
        {
            "HIGH" or "CRITICAL" => LogLevel.Warning,
            "MEDIUM" => LogLevel.Information,
            "LOW" => LogLevel.Debug,
            _ => LogLevel.Information
        };
    }
}