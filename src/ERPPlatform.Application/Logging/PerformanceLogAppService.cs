using Microsoft.Extensions.Logging;
using System;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Application.Services;
using ERPPlatform.Logging.Helpers;

namespace ERPPlatform.Logging;

/// <summary>
/// Service for logging performance metrics following ABP standards
/// </summary>
public class PerformanceLogAppService : ApplicationService, IPerformanceLogAppService
{
    private readonly LoggingContextProvider _contextProvider;
    private readonly StructuredLoggerHelper _loggerHelper;

    public PerformanceLogAppService(
        LoggingContextProvider contextProvider,
        StructuredLoggerHelper loggerHelper)
    {
        _contextProvider = contextProvider;
        _loggerHelper = loggerHelper;
    }

    public async Task LogPerformanceAsync(PerformanceLogDto logData)
    {
        Check.NotNull(logData, nameof(logData));

        // Validate required fields
        if (!_loggerHelper.ValidateLogEntry(logData, "Operation"))
        {
            Logger.LogWarning("Invalid performance log data provided");
            return;
        }

        try
        {
            // Enrich with context
            _contextProvider.EnrichWithContext(logData);

            // Create structured log properties
            var properties = _loggerHelper.CreateLogProperties(logData);
            
            // Add performance-specific properties
            properties[LoggingConstants.PropertyNames.Operation] = logData.Operation;
            properties[LoggingConstants.PropertyNames.Duration] = logData.DurationMilliseconds;
            properties[LoggingConstants.PropertyNames.Category] = LoggingConstants.Categories.Performance;
            properties["PerformanceLevel"] = _loggerHelper.GetPerformanceLevel(logData.Duration);
            properties["IsSlowOperation"] = logData.IsSlowOperation;
            
            if (!string.IsNullOrEmpty(logData.Component))
            {
                properties["Component"] = logData.Component;
            }

            if (!string.IsNullOrEmpty(logData.Method))
            {
                properties["Method"] = logData.Method;
            }

            if (!string.IsNullOrEmpty(logData.QueryDetails))
            {
                properties["QueryDetails"] = logData.QueryDetails;
            }

            // Determine log level based on performance
            var logLevel = logData.IsSlowOperation ? LogLevel.Warning : LogLevel.Information;

            // Log the performance data
            using var scope = _loggerHelper.CreateLogScope(Logger, properties);
            
            Logger.Log(logLevel,
                "Operation {Operation} completed in {Duration}ms by user {UserId} - Performance level: {PerformanceLevel}",
                logData.Operation,
                logData.DurationMilliseconds,
                logData.UserId,
                _loggerHelper.GetPerformanceLevel(logData.Duration));
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error occurred while logging performance data");
        }

        await Task.CompletedTask;
    }

    public async Task LogOperationPerformanceAsync(string operation, TimeSpan duration, string? component = null, string? method = null)
    {
        var logData = new PerformanceLogDto
        {
            Operation = Check.NotNullOrWhiteSpace(operation, nameof(operation)),
            Duration = duration,
            Component = component,
            Method = method
        };

        await LogPerformanceAsync(logData);
    }

    public async Task LogQueryPerformanceAsync(string queryType, TimeSpan duration, string entityType, int? recordCount = null)
    {
        var operation = $"{queryType} {entityType}";
        
        var logData = new PerformanceLogDto
        {
            Operation = operation,
            Duration = duration,
            Component = "Database",
            Method = queryType,
            QueryDetails = recordCount.HasValue ? $"Records affected: {recordCount}" : null
        };

        // Add query-specific properties
        logData.AdditionalProperties["QueryType"] = queryType;
        logData.AdditionalProperties["EntityType"] = entityType;
        
        if (recordCount.HasValue)
        {
            logData.AdditionalProperties["RecordCount"] = recordCount.Value;
        }

        await LogPerformanceAsync(logData);
    }

    public async Task LogApiPerformanceAsync(string endpoint, string httpMethod, TimeSpan duration, int statusCode)
    {
        var operation = $"{httpMethod} {endpoint}";
        
        var logData = new PerformanceLogDto
        {
            Operation = operation,
            Duration = duration,
            Component = "API",
            Method = httpMethod
        };

        // Add API-specific properties
        logData.AdditionalProperties["Endpoint"] = endpoint;
        logData.AdditionalProperties["HttpMethod"] = httpMethod;
        logData.AdditionalProperties["StatusCode"] = statusCode;
        logData.AdditionalProperties["IsSuccess"] = statusCode >= 200 && statusCode < 400;

        await LogPerformanceAsync(logData);
    }
}