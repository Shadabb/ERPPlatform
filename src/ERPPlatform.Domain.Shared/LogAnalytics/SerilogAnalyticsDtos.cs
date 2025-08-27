using System;
using System.Collections.Generic;

namespace ERPPlatform.LogAnalytics;

/// <summary>
/// Constants for Serilog analytics
/// </summary>
public static class SerilogAnalyticsConstants
{
    public static class LogLevels
    {
        public const string Verbose = "Verbose";
        public const string Debug = "Debug";
        public const string Information = "Information";
        public const string Warning = "Warning";
        public const string Error = "Error";
        public const string Fatal = "Fatal";
    }

    public static class Metrics
    {
        public const int SlowResponseThreshold = 5000; // ms
        public const int VerySlowResponseThreshold = 10000; // ms
        public const int ErrorSpikeTreshold = 10; // errors per minute
    }
}

/// <summary>
/// Serilog entry DTO representing ApplicationLogs table
/// </summary>
public class SerilogEntryDto
{
    public int Id { get; set; }
    public string Message { get; set; } = string.Empty;
    public string Level { get; set; } = string.Empty;
    public DateTime TimeStamp { get; set; }
    public string? Exception { get; set; }
    public string Properties { get; set; } = "{}";
    public string? LogEvent { get; set; }
    
    // Custom columns
    public string? UserId { get; set; }
    public string? RequestId { get; set; }
    public string? CorrelationId { get; set; }
    public string? HttpMethod { get; set; }
    public string? RequestPath { get; set; }
    public int? ResponseStatusCode { get; set; }
    public long? Duration { get; set; }
    
    // Computed properties
    public bool IsError => Level == SerilogAnalyticsConstants.LogLevels.Error || 
                          Level == SerilogAnalyticsConstants.LogLevels.Fatal;
    
    public bool IsSlowRequest => Duration.HasValue && 
                                Duration > SerilogAnalyticsConstants.Metrics.SlowResponseThreshold;
    
    public string PerformanceLevel
    {
        get
        {
            if (!Duration.HasValue) return "Unknown";
            return Duration.Value switch
            {
                <= 100 => "Excellent",
                <= 500 => "Good", 
                <= 1000 => "Fair",
                <= 5000 => "Slow",
                _ => "Critical"
            };
        }
    }
}

/// <summary>
/// Serilog dashboard main DTO
/// </summary>
public class SerilogDashboardDto
{
    public SerilogStatisticsDto Statistics { get; set; } = new();
    public List<SerilogLevelCountDto> LogLevelDistribution { get; set; } = new();
    public List<SerilogHourlyTrendDto> HourlyTrends { get; set; } = new();
    public List<SerilogTopErrorDto> TopErrors { get; set; } = new();
    public List<SerilogSlowRequestDto> SlowRequests { get; set; } = new();
    public List<SerilogEndpointStatsDto> TopEndpoints { get; set; } = new();
    public List<SerilogRecentEntryDto> RecentLogs { get; set; } = new();
    public SystemPerformanceDto Performance { get; set; } = new();
    public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Serilog statistics summary
/// </summary>
public class SerilogStatisticsDto
{
    public int TotalLogs { get; set; }
    public int TodayLogs { get; set; }
    public int ErrorCount { get; set; }
    public int WarningCount { get; set; }
    public int InfoCount { get; set; }
    public int TotalRequests { get; set; }
    public double AvgResponseTime { get; set; }
    public int SlowRequestCount { get; set; }
    public int Http4xxCount { get; set; }
    public int Http5xxCount { get; set; }
    public double ErrorRate => TotalRequests > 0 ? Math.Round((double)ErrorCount / TotalRequests * 100, 2) : 0;
    public double SuccessRate => TotalRequests > 0 ? Math.Round((double)(TotalRequests - ErrorCount) / TotalRequests * 100, 2) : 0;
}

/// <summary>
/// Log level distribution
/// </summary>
public class SerilogLevelCountDto
{
    public string Level { get; set; } = string.Empty;
    public int Count { get; set; }
    public double Percentage { get; set; }
}

/// <summary>
/// Hourly trends for Serilog data
/// </summary>
public class SerilogHourlyTrendDto
{
    public DateTime Hour { get; set; }
    public int TotalRequests { get; set; }
    public int ErrorCount { get; set; }
    public int WarningCount { get; set; }
    public double AvgResponseTime { get; set; }
    public int SlowRequestCount { get; set; }
}

/// <summary>
/// Top error details from Serilog
/// </summary>
public class SerilogTopErrorDto
{
    public string ErrorMessage { get; set; } = string.Empty;
    public string? ExceptionType { get; set; }
    public int Count { get; set; }
    public DateTime FirstOccurrence { get; set; }
    public DateTime LastOccurrence { get; set; }
    public List<string> AffectedEndpoints { get; set; } = new();
    public string Level { get; set; } = string.Empty;
}

/// <summary>
/// Slow request analytics
/// </summary>
public class SerilogSlowRequestDto
{
    public string RequestPath { get; set; } = string.Empty;
    public string HttpMethod { get; set; } = string.Empty;
    public long Duration { get; set; }
    public DateTime TimeStamp { get; set; }
    public string? UserId { get; set; }
    public int? ResponseStatusCode { get; set; }
    public string PerformanceLevel { get; set; } = string.Empty;
}

/// <summary>
/// Endpoint performance statistics
/// </summary>
public class SerilogEndpointStatsDto
{
    public string Endpoint { get; set; } = string.Empty;
    public string HttpMethod { get; set; } = string.Empty;
    public int RequestCount { get; set; }
    public double AvgDuration { get; set; }
    public long MaxDuration { get; set; }
    public long MinDuration { get; set; }
    public int ErrorCount { get; set; }
    public int SuccessCount { get; set; }
    public double ErrorRate => RequestCount > 0 ? Math.Round((double)ErrorCount / RequestCount * 100, 2) : 0;
    public double SuccessRate => RequestCount > 0 ? Math.Round((double)SuccessCount / RequestCount * 100, 2) : 0;
}

/// <summary>
/// Recent log entry for display
/// </summary>
public class SerilogRecentEntryDto
{
    public DateTime TimeStamp { get; set; }
    public string Level { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string? RequestPath { get; set; }
    public string? HttpMethod { get; set; }
    public long? Duration { get; set; }
    public int? ResponseStatusCode { get; set; }
    public string? UserId { get; set; }
    public bool HasException { get; set; }
    public string? Exception { get; set; }
}

/// <summary>
/// System performance metrics
/// </summary>
public class SystemPerformanceDto
{
    public double AvgResponseTime { get; set; }
    public double P95ResponseTime { get; set; }
    public double P99ResponseTime { get; set; }
    public int RequestsPerMinute { get; set; }
    public int ErrorsPerMinute { get; set; }
    public double Throughput { get; set; }
    public string HealthStatus { get; set; } = "Unknown";
    public Dictionary<string, object> AdditionalMetrics { get; set; } = new();
}

/// <summary>
/// Serilog search request DTO
/// </summary>
public class SerilogSearchRequestDto
{
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
    public List<string> LogLevels { get; set; } = new();
    public string? SearchText { get; set; }
    public string? UserId { get; set; }
    public string? RequestPath { get; set; }
    public string? HttpMethod { get; set; }
    public int? MinDuration { get; set; }
    public int? MaxDuration { get; set; }
    public bool? HasException { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 50;

    public void ValidateAndSetDefaults()
    {
        if (Page <= 0) Page = 1;
        if (PageSize <= 0) PageSize = 50;
        if (PageSize > 1000) PageSize = 1000;
        
        if (MinDuration.HasValue && MinDuration < 0) MinDuration = 0;
        if (MaxDuration.HasValue && MaxDuration < 0) MaxDuration = null;
        if (MinDuration.HasValue && MaxDuration.HasValue && MinDuration > MaxDuration)
        {
            var temp = MinDuration;
            MinDuration = MaxDuration;
            MaxDuration = temp;
        }
    }
}

/// <summary>
/// Serilog search response DTO
/// </summary>
public class SerilogSearchResponseDto
{
    public List<SerilogEntryDto> Items { get; set; } = new();
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalPages { get; set; }
    
    public SerilogSearchResponseDto()
    {
        Items = new List<SerilogEntryDto>();
    }
    
    public SerilogSearchResponseDto(int totalCount, List<SerilogEntryDto> items, int page, int pageSize)
    {
        TotalCount = totalCount;
        Items = items ?? new List<SerilogEntryDto>();
        Page = page;
        PageSize = pageSize;
        TotalPages = (int)Math.Ceiling((double)totalCount / pageSize);
    }
}

/// <summary>
/// Dashboard range request for Serilog analytics
/// </summary>
public class SerilogDashboardRequestDto
{
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
    public int TopErrorsCount { get; set; } = 10;
    public int TopEndpointsCount { get; set; } = 10;
    public int SlowRequestsCount { get; set; } = 20;
    public bool IncludeHourlyTrends { get; set; } = true;
    public bool IncludePerformanceMetrics { get; set; } = true;
}