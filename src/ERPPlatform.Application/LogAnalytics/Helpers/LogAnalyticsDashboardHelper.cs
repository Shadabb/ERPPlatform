using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Volo.Abp;
using Volo.Abp.DependencyInjection;

namespace ERPPlatform.LogAnalytics.Helpers;

/// <summary>
/// Helper service for log analytics dashboard operations
/// Provides utility methods for data transformation, validation, and formatting
/// </summary>
public class LogAnalyticsDashboardHelper : ITransientDependency
{
    private readonly ILogger<LogAnalyticsDashboardHelper> _logger;

    public LogAnalyticsDashboardHelper(ILogger<LogAnalyticsDashboardHelper> logger)
    {
        _logger = logger;
    }

    #region Data Validation

    /// <summary>
    /// Validates and normalizes date range for dashboard queries
    /// </summary>
    public (DateTime fromDate, DateTime toDate) ValidateDateRange(DateTime? fromDate, DateTime? toDate, int defaultDays = 7)
    {
        var now = DateTime.UtcNow;
        
        // Set defaults if not provided
        var validFromDate = fromDate ?? now.AddDays(-defaultDays);
        var validToDate = toDate ?? now;

        // Ensure from date is not after to date
        if (validFromDate > validToDate)
        {
            (validFromDate, validToDate) = (validToDate, validFromDate);
        }

        // Ensure reasonable date range (not more than 1 year)
        if ((validToDate - validFromDate).TotalDays > 365)
        {
            validFromDate = validToDate.AddDays(-365);
        }

        // Ensure dates are not in the future
        if (validToDate > now)
        {
            validToDate = now;
        }

        if (validFromDate > now)
        {
            validFromDate = now.AddDays(-defaultDays);
        }

        return (validFromDate, validToDate);
    }

    /// <summary>
    /// Validates pagination parameters
    /// </summary>
    public (int skip, int take) ValidatePagination(int skip, int take, int maxPageSize = 1000)
    {
        var validSkip = Math.Max(0, skip);
        var validTake = Math.Max(1, Math.Min(take, maxPageSize));
        
        return (validSkip, validTake);
    }

    /// <summary>
    /// Validates export parameters
    /// </summary>
    public ExportLogsRequestDto ValidateExportRequest(ExportLogsRequestDto request)
    {
        Check.NotNull(request, nameof(request));
        
        request.ValidateAndSetDefaults();
        
        var (fromDate, toDate) = ValidateDateRange(request.FromDate, request.ToDate, 30); // 30 days for exports
        request.FromDate = fromDate;
        request.ToDate = toDate;
        
        return request;
    }

    #endregion

    #region Data Transformation

    /// <summary>
    /// Extracts application name from service name
    /// </summary>
    public string GetApplicationNameFromService(string? serviceName)
    {
        if (string.IsNullOrWhiteSpace(serviceName))
            return LogAnalyticsDashboardConstants.Applications.Application;
            
        if (serviceName.Contains("Controllers") || serviceName.Contains("Controller"))
            return LogAnalyticsDashboardConstants.Applications.HttpApiHost;
        else if (serviceName.Contains("AppService") || serviceName.Contains("Application"))
            return LogAnalyticsDashboardConstants.Applications.Application;
        else if (serviceName.Contains("Web") || serviceName.Contains("Pages"))
            return LogAnalyticsDashboardConstants.Applications.Web;
        else if (serviceName.Contains("AuthServer"))
            return LogAnalyticsDashboardConstants.Applications.AuthServer;
        else
            return LogAnalyticsDashboardConstants.Applications.Application;
    }

    /// <summary>
    /// Determines log level from audit log data
    /// </summary>
    public string GetLogLevelFromAuditLog(bool hasException, int? httpStatusCode)
    {
        if (hasException) 
            return LogAnalyticsDashboardConstants.LogLevels.Error;
            
        if (httpStatusCode.HasValue)
        {
            if (httpStatusCode >= 500) 
                return LogAnalyticsDashboardConstants.LogLevels.Error;
            if (httpStatusCode >= 400) 
                return LogAnalyticsDashboardConstants.LogLevels.Warning;
        }
        
        return LogAnalyticsDashboardConstants.LogLevels.Information;
    }

    /// <summary>
    /// Determines if operation is slow based on duration
    /// </summary>
    public bool IsSlowOperation(int executionDuration, int threshold = 5000)
    {
        return executionDuration > threshold;
    }

    /// <summary>
    /// Gets performance level description
    /// </summary>
    public string GetPerformanceLevel(int executionDuration)
    {
        return executionDuration switch
        {
            <= 100 => "Excellent",
            <= 500 => "Good",
            <= 1000 => "Fair",
            <= 5000 => "Slow",
            _ => "Critical"
        };
    }

    #endregion

    #region Exception Handling

    /// <summary>
    /// Extracts exception type from exception string
    /// </summary>
    public string? ExtractExceptionType(string? exception)
    {
        if (string.IsNullOrWhiteSpace(exception)) 
            return null;
        
        try
        {
            var lines = exception.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            if (lines.Length == 0) return null;
            
            var firstLine = lines[0].Trim();
            var colonIndex = firstLine.IndexOf(':');
            
            return colonIndex > 0 ? firstLine.Substring(0, colonIndex).Trim() : firstLine;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to extract exception type from: {Exception}", exception);
            return "Unknown Exception";
        }
    }

    /// <summary>
    /// Extracts exception message from exception string
    /// </summary>
    public string? ExtractExceptionMessage(string? exception)
    {
        if (string.IsNullOrWhiteSpace(exception)) 
            return null;
        
        try
        {
            var lines = exception.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            if (lines.Length == 0) return null;
            
            var firstLine = lines[0].Trim();
            var colonIndex = firstLine.IndexOf(':');
            
            return colonIndex > 0 ? firstLine.Substring(colonIndex + 1).Trim() : firstLine;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to extract exception message from: {Exception}", exception);
            return "Unknown error occurred";
        }
    }

    #endregion

    #region JSON Handling

    /// <summary>
    /// Safely parses JSON properties
    /// </summary>
    public Dictionary<string, object> ParseJsonProperties(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) 
            return new Dictionary<string, object>();
        
        try
        {
            return JsonSerializer.Deserialize<Dictionary<string, object>>(json) ?? new Dictionary<string, object>();
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Failed to parse JSON properties: {Json}", json);
            return new Dictionary<string, object>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error parsing JSON properties: {Json}", json);
            return new Dictionary<string, object>();
        }
    }

    /// <summary>
    /// Extracts string value from JSON properties
    /// </summary>
    public string? ExtractStringFromJson(string? json, string key)
    {
        if (string.IsNullOrWhiteSpace(json)) 
            return null;
        
        try
        {
            using var document = JsonDocument.Parse(json);
            if (document.RootElement.TryGetProperty(key, out var element))
            {
                return element.GetString();
            }
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Failed to extract {Key} from JSON: {Json}", key, json);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error extracting {Key} from JSON: {Json}", key, json);
        }
        
        return null;
    }

    /// <summary>
    /// Extracts numeric value from JSON properties
    /// </summary>
    public double ExtractNumberFromJson(string? json, string key, double defaultValue = 0)
    {
        if (string.IsNullOrWhiteSpace(json)) 
            return defaultValue;
        
        try
        {
            using var document = JsonDocument.Parse(json);
            if (document.RootElement.TryGetProperty(key, out var element))
            {
                if (element.ValueKind == JsonValueKind.Number)
                {
                    return element.GetDouble();
                }
            }
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Failed to extract numeric {Key} from JSON: {Json}", key, json);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error extracting numeric {Key} from JSON: {Json}", key, json);
        }
        
        return defaultValue;
    }

    #endregion

    #region Export Formatting

    /// <summary>
    /// Escapes string value for CSV export
    /// </summary>
    public string EscapeCsvValue(string? value)
    {
        if (string.IsNullOrEmpty(value)) 
            return string.Empty;
        
        // Replace quotes and newlines
        return value
            .Replace("\"", "\"\"")
            .Replace("\n", " ")
            .Replace("\r", "")
            .Replace("\t", " ");
    }

    /// <summary>
    /// Formats date for export
    /// </summary>
    public string FormatDateForExport(DateTime dateTime)
    {
        return dateTime.ToString("yyyy-MM-dd HH:mm:ss");
    }

    /// <summary>
    /// Gets system health status based on metrics
    /// </summary>
    public string GetHealthStatus(int recentErrors, int recentCritical, double avgResponseTime)
    {
        if (recentCritical > 0 || avgResponseTime > 10000) // 10 seconds
            return LogAnalyticsDashboardConstants.HealthStatus.Critical;
            
        if (recentErrors > 10 || avgResponseTime > 5000) // 5 seconds
            return LogAnalyticsDashboardConstants.HealthStatus.Warning;
            
        return LogAnalyticsDashboardConstants.HealthStatus.Healthy;
    }

    #endregion

    #region Statistics Calculations

    /// <summary>
    /// Calculates percentage with proper rounding
    /// </summary>
    public double CalculatePercentage(int value, int total, int decimalPlaces = 1)
    {
        if (total <= 0) return 0;
        return Math.Round((double)value / total * 100, decimalPlaces);
    }

    /// <summary>
    /// Calculates success rate from counts
    /// </summary>
    public double CalculateSuccessRate(int total, int failures, int decimalPlaces = 1)
    {
        if (total <= 0) return 0;
        var successes = Math.Max(0, total - failures);
        return CalculatePercentage(successes, total, decimalPlaces);
    }

    /// <summary>
    /// Groups data by time intervals (hourly)
    /// </summary>
    public List<HourlyLogCountDto> GroupByHour<T>(
        IEnumerable<T> data, 
        Func<T, DateTime> dateSelector,
        Func<T, bool> errorSelector,
        int maxHours = 24)
    {
        var hourlyGroups = data
            .GroupBy(x => new DateTime(
                dateSelector(x).Year,
                dateSelector(x).Month,
                dateSelector(x).Day,
                dateSelector(x).Hour,
                0, 0))
            .Select(g => new HourlyLogCountDto
            {
                Hour = g.Key,
                TotalCount = g.Count(),
                ErrorCount = g.Count(errorSelector),
                WarningCount = 0, // No warning concept in audit logs
                InfoCount = g.Count(x => !errorSelector(x))
            })
            .OrderByDescending(x => x.Hour)
            .Take(maxHours)
            .OrderBy(x => x.Hour)
            .ToList();

        return hourlyGroups;
    }

    #endregion
}