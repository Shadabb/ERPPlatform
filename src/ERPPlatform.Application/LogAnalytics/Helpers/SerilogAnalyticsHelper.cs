using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Volo.Abp;
using Volo.Abp.DependencyInjection;

namespace ERPPlatform.LogAnalytics.Helpers;

/// <summary>
/// Helper service for Serilog analytics operations
/// Provides utility methods for data processing, validation, and transformation
/// </summary>
public class SerilogAnalyticsHelper : ITransientDependency
{
    private readonly ILogger<SerilogAnalyticsHelper> _logger;

    public SerilogAnalyticsHelper(ILogger<SerilogAnalyticsHelper> logger)
    {
        _logger = logger;
    }

    #region Data Validation

    /// <summary>
    /// Validates and normalizes date range for Serilog queries
    /// </summary>
    public (DateTime fromDate, DateTime toDate) ValidateDateRange(DateTime? fromDate, DateTime? toDate, int defaultHours = 24)
    {
        var now = DateTime.UtcNow;
        
        var validFromDate = fromDate ?? now.AddHours(-defaultHours);
        var validToDate = toDate ?? now;

        if (validFromDate > validToDate)
        {
            (validFromDate, validToDate) = (validToDate, validFromDate);
        }

        if ((validToDate - validFromDate).TotalDays > 30)
        {
            validFromDate = validToDate.AddDays(-30);
        }

        if (validToDate > now)
        {
            validToDate = now;
        }

        if (validFromDate > now)
        {
            validFromDate = now.AddHours(-defaultHours);
        }

        return (validFromDate, validToDate);
    }

    /// <summary>
    /// Validates Serilog search request
    /// </summary>
    public SerilogSearchRequestDto ValidateSearchRequest(SerilogSearchRequestDto request)
    {
        Check.NotNull(request, nameof(request));
        
        request.ValidateAndSetDefaults();
        
        var (fromDate, toDate) = ValidateDateRange(request.FromDate, request.ToDate, 24);
        request.FromDate = fromDate;
        request.ToDate = toDate;
        
        return request;
    }

    /// <summary>
    /// Validates dashboard request
    /// </summary>
    public SerilogDashboardRequestDto ValidateDashboardRequest(SerilogDashboardRequestDto request)
    {
        Check.NotNull(request, nameof(request));
        
        var (fromDate, toDate) = ValidateDateRange(request.FromDate, request.ToDate, 24);
        request.FromDate = fromDate;
        request.ToDate = toDate;
        
        if (request.TopErrorsCount <= 0 || request.TopErrorsCount > 100)
            request.TopErrorsCount = 10;
            
        if (request.TopEndpointsCount <= 0 || request.TopEndpointsCount > 100)
            request.TopEndpointsCount = 10;
            
        if (request.SlowRequestsCount <= 0 || request.SlowRequestsCount > 100)
            request.SlowRequestsCount = 20;
        
        return request;
    }

    #endregion

    #region Data Transformation

    /// <summary>
    /// Maps ApplicationLog entity to SerilogEntryDto
    /// </summary>
    public SerilogEntryDto MapToSerilogEntryDto(ApplicationLog log)
    {
        Check.NotNull(log, nameof(log));
        
        return new SerilogEntryDto
        {
            Id = log.Id,
            Message = log.Message,
            Level = log.Level,
            TimeStamp = log.TimeStamp,
            Exception = log.Exception,
            Properties = log.Properties,
            LogEvent = log.LogEvent,
            UserId = log.UserId,
            RequestId = log.RequestId,
            CorrelationId = log.CorrelationId,
            HttpMethod = log.HttpMethod,
            RequestPath = log.RequestPath,
            ResponseStatusCode = log.ResponseStatusCode,
            Duration = log.Duration
        };
    }

    /// <summary>
    /// Maps ApplicationLog to RecentEntryDto
    /// </summary>
    public SerilogRecentEntryDto MapToRecentEntryDto(ApplicationLog log)
    {
        Check.NotNull(log, nameof(log));
        
        return new SerilogRecentEntryDto
        {
            TimeStamp = log.TimeStamp,
            Level = log.Level,
            Message = log.Message,
            RequestPath = log.RequestPath,
            HttpMethod = log.HttpMethod,
            Duration = log.Duration,
            ResponseStatusCode = log.ResponseStatusCode,
            UserId = log.UserId,
            HasException = !string.IsNullOrEmpty(log.Exception),
            Exception = log.Exception
        };
    }

    /// <summary>
    /// Extracts error message from exception string
    /// </summary>
    public string? ExtractErrorMessage(string? exception)
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
            _logger.LogWarning(ex, "Failed to extract error message from exception: {Exception}", exception);
            return "Unknown error";
        }
    }

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

    #endregion

    #region Performance Analysis

    /// <summary>
    /// Gets performance level description
    /// </summary>
    public string GetPerformanceLevel(long? durationMs)
    {
        if (!durationMs.HasValue) return "Unknown";
        
        return durationMs.Value switch
        {
            <= 100 => "Excellent",
            <= 500 => "Good",
            <= 1000 => "Fair",
            <= 5000 => "Slow",
            _ => "Critical"
        };
    }

    /// <summary>
    /// Determines if request is slow based on duration
    /// </summary>
    public bool IsSlowRequest(long? durationMs, int threshold = 5000)
    {
        return durationMs.HasValue && durationMs.Value > threshold;
    }

    /// <summary>
    /// Gets log level priority for sorting
    /// </summary>
    public int GetLogLevelPriority(string level)
    {
        return level switch
        {
            "Fatal" => 5,
            "Error" => 4,
            "Warning" => 3,
            "Information" => 2,
            "Debug" => 1,
            "Verbose" => 0,
            _ => 0
        };
    }

    /// <summary>
    /// Gets system health status based on metrics
    /// </summary>
    public string GetSystemHealthStatus(double errorRate, double avgResponseTime, int totalRequests)
    {
        if (totalRequests == 0) return "Unknown";
        
        if (errorRate > 10 || avgResponseTime > 10000) // 10% error rate or 10s avg response
            return "Critical";
            
        if (errorRate > 5 || avgResponseTime > 5000) // 5% error rate or 5s avg response
            return "Warning";
            
        if (errorRate > 1 || avgResponseTime > 2000) // 1% error rate or 2s avg response
            return "Fair";
            
        return "Healthy";
    }

    #endregion

    #region Statistics Calculations

    /// <summary>
    /// Calculates percentile values from duration data
    /// </summary>
    public (double p95, double p99) CalculatePercentiles(IEnumerable<long> durations)
    {
        var sortedDurations = durations.OrderBy(x => x).ToArray();
        if (sortedDurations.Length == 0) return (0, 0);

        var p95Index = (int)Math.Ceiling(sortedDurations.Length * 0.95) - 1;
        var p99Index = (int)Math.Ceiling(sortedDurations.Length * 0.99) - 1;

        p95Index = Math.Max(0, Math.Min(p95Index, sortedDurations.Length - 1));
        p99Index = Math.Max(0, Math.Min(p99Index, sortedDurations.Length - 1));

        return (sortedDurations[p95Index], sortedDurations[p99Index]);
    }

    /// <summary>
    /// Calculates rate per minute
    /// </summary>
    public double CalculateRatePerMinute(int count, DateTime fromDate, DateTime toDate)
    {
        var totalMinutes = Math.Max(1, (toDate - fromDate).TotalMinutes);
        return Math.Round(count / totalMinutes, 2);
    }

    /// <summary>
    /// Calculates percentage with proper rounding
    /// </summary>
    public double CalculatePercentage(int value, int total, int decimalPlaces = 2)
    {
        if (total <= 0) return 0;
        return Math.Round((double)value / total * 100, decimalPlaces);
    }

    /// <summary>
    /// Groups logs by time intervals (hourly)
    /// </summary>
    public List<SerilogHourlyTrendDto> GroupByHour(
        IEnumerable<ApplicationLog> logs, 
        int maxHours = 24)
    {
        var hourlyGroups = logs
            .GroupBy(log => new DateTime(
                log.TimeStamp.Year,
                log.TimeStamp.Month, 
                log.TimeStamp.Day,
                log.TimeStamp.Hour,
                0, 0))
            .Select(g => new SerilogHourlyTrendDto
            {
                Hour = g.Key,
                TotalRequests = g.Count(x => x.IsHttpRequest),
                ErrorCount = g.Count(x => x.IsError),
                WarningCount = g.Count(x => x.IsWarning),
                AvgResponseTime = g.Where(x => x.Duration.HasValue)
                                  .Select(x => x.Duration!.Value)
                                  .DefaultIfEmpty(0)
                                  .Average(),
                SlowRequestCount = g.Count(x => x.IsSlowRequest)
            })
            .OrderByDescending(x => x.Hour)
            .Take(maxHours)
            .OrderBy(x => x.Hour)
            .ToList();

        return hourlyGroups;
    }

    #endregion

    #region JSON Processing

    /// <summary>
    /// Safely parses log properties JSON
    /// </summary>
    public Dictionary<string, object> ParseLogProperties(string? properties)
    {
        if (string.IsNullOrWhiteSpace(properties))
            return new Dictionary<string, object>();

        try
        {
            return JsonSerializer.Deserialize<Dictionary<string, object>>(properties) ?? 
                   new Dictionary<string, object>();
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Failed to parse log properties JSON: {Properties}", properties);
            return new Dictionary<string, object>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error parsing log properties JSON: {Properties}", properties);
            return new Dictionary<string, object>();
        }
    }

    /// <summary>
    /// Extracts property value from JSON
    /// </summary>
    public T? ExtractPropertyValue<T>(string? properties, string key)
    {
        var props = ParseLogProperties(properties);
        
        if (props.TryGetValue(key, out var value))
        {
            try
            {
                if (value is JsonElement jsonElement)
                {
                    return JsonSerializer.Deserialize<T>(jsonElement.GetRawText());
                }
                
                return (T?)Convert.ChangeType(value, typeof(T));
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to extract property {Key} as {Type} from: {Properties}", 
                    key, typeof(T).Name, properties);
            }
        }

        return default(T);
    }

    #endregion

    #region Export Utilities

    /// <summary>
    /// Escapes CSV values properly
    /// </summary>
    public string EscapeCsvValue(string? value)
    {
        if (string.IsNullOrEmpty(value))
            return string.Empty;

        return value
            .Replace("\"", "\"\"")
            .Replace("\n", " ")
            .Replace("\r", "")
            .Replace("\t", " ");
    }

    /// <summary>
    /// Formats timestamp for export
    /// </summary>
    public string FormatTimestampForExport(DateTime timestamp)
    {
        return timestamp.ToString("yyyy-MM-dd HH:mm:ss.fff");
    }

    /// <summary>
    /// Gets friendly duration string
    /// </summary>
    public string GetFriendlyDuration(long? durationMs)
    {
        if (!durationMs.HasValue) return "N/A";
        
        if (durationMs < 1000) return $"{durationMs}ms";
        
        var seconds = durationMs / 1000.0;
        return $"{seconds:F2}s";
    }

    #endregion

    #region Filtering Helpers

    /// <summary>
    /// Applies search filters to log query
    /// </summary>
    public IQueryable<ApplicationLog> ApplySearchFilters(
        IQueryable<ApplicationLog> query, 
        SerilogSearchRequestDto request)
    {
        if (request.FromDate.HasValue)
            query = query.Where(x => x.TimeStamp >= request.FromDate.Value);

        if (request.ToDate.HasValue)
            query = query.Where(x => x.TimeStamp <= request.ToDate.Value);

        if (request.LogLevels.Any())
            query = query.Where(x => request.LogLevels.Contains(x.Level));

        if (!string.IsNullOrWhiteSpace(request.SearchText))
        {
            var searchText = request.SearchText.ToLower();
            query = query.Where(x => x.Message.ToLower().Contains(searchText) ||
                                   (x.Exception != null && x.Exception.ToLower().Contains(searchText)));
        }

        if (!string.IsNullOrWhiteSpace(request.UserId))
            query = query.Where(x => x.UserId == request.UserId);

        if (!string.IsNullOrWhiteSpace(request.RequestPath))
            query = query.Where(x => x.RequestPath != null && x.RequestPath.Contains(request.RequestPath));

        if (!string.IsNullOrWhiteSpace(request.HttpMethod))
            query = query.Where(x => x.HttpMethod == request.HttpMethod);

        if (request.MinDuration.HasValue)
            query = query.Where(x => x.Duration >= request.MinDuration.Value);

        if (request.MaxDuration.HasValue)
            query = query.Where(x => x.Duration <= request.MaxDuration.Value);

        if (request.HasException.HasValue)
        {
            if (request.HasException.Value)
                query = query.Where(x => x.Exception != null && x.Exception != "");
            else
                query = query.Where(x => x.Exception == null || x.Exception == "");
        }

        return query;
    }

    #endregion
}