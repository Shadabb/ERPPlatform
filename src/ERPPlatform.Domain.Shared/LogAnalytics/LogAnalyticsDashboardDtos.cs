using System;
using System.Collections.Generic;

namespace ERPPlatform.LogAnalytics;

/// <summary>
/// Constants for log analytics dashboard
/// </summary>
public static class LogAnalyticsDashboardConstants
{
    public static class LogLevels
    {
        public const string Information = "Information";
        public const string Warning = "Warning";
        public const string Error = "Error";
        public const string Debug = "Debug";
        public const string Critical = "Critical";
    }

    public static class Applications
    {
        public const string HttpApiHost = "ERPPlatform.HttpApi.Host";
        public const string Application = "ERPPlatform.Application";
        public const string Web = "ERPPlatform.Web";
        public const string AuthServer = "ERPPlatform.AuthServer";
    }

    public static class ExportFormats
    {
        public const string Csv = "csv";
        public const string Json = "json";
        public const string Excel = "xlsx";
    }

    public static class HealthStatus
    {
        public const string Healthy = "Healthy";
        public const string Warning = "Warning";
        public const string Critical = "Critical";
        public const string Unknown = "Unknown";
    }

    public static class DefaultValues
    {
        public const int DefaultPageSize = 20;
        public const int MaxPageSize = 1000;
        public const int DefaultDashboardDays = 7;
        public const int DefaultTopCount = 10;
        public const int SlowOperationThreshold = 5000; // milliseconds
    }
}

/// <summary>
/// Base DTO for log analytics dashboard requests
/// </summary>
public abstract class LogAnalyticsDashboardBaseDto
{
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
}

/// <summary>
/// Main dashboard data response DTO
/// </summary>
public class LogAnalyticsDashboardDto
{
    public LogStatisticsDto Statistics { get; set; } = new();
    public AuditLogStatisticsDto AuditStatistics { get; set; } = new();
    public List<LogLevelCountDto> LogLevelCounts { get; set; } = new();
    public List<ApplicationLogCountDto> ApplicationCounts { get; set; } = new();
    public List<HourlyLogCountDto> HourlyCounts { get; set; } = new();
    public List<RecentLogEntryDto> RecentLogs { get; set; } = new();
    public List<TopErrorDto> TopErrors { get; set; } = new();
    public List<PerformanceMetricDto> PerformanceMetrics { get; set; } = new();
    public List<RecentAuditLogDto> RecentAuditLogs { get; set; } = new();
    public List<TopUserActivityDto> TopUserActivities { get; set; } = new();
    public List<AuditLogMethodCountDto> TopAuditMethods { get; set; } = new();
    public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Dashboard range request DTO
/// </summary>
public class DashboardRangeRequestDto : LogAnalyticsDashboardBaseDto
{
    public int TopCount { get; set; } = LogAnalyticsDashboardConstants.DefaultValues.DefaultTopCount;
    public bool IncludePerformanceMetrics { get; set; } = true;
    public bool IncludeHourlyTrends { get; set; } = true;
}

/// <summary>
/// Log statistics DTO
/// </summary>
public class LogStatisticsDto
{
    public int TotalLogs { get; set; }
    public int TodayLogs { get; set; }
    public int ErrorCount { get; set; }
    public int WarningCount { get; set; }
    public int InfoCount { get; set; }
    public double AvgResponseTime { get; set; }
    public int SlowOperations { get; set; }
    public int SecurityEvents { get; set; }
    public int TotalAuditLogs { get; set; }
    public int TodayAuditLogs { get; set; }
    public int FailedOperations { get; set; }
}

/// <summary>
/// Log level count DTO
/// </summary>
public class LogLevelCountDto
{
    public string Level { get; set; } = string.Empty;
    public int Count { get; set; }
    public double Percentage { get; set; }
}

/// <summary>
/// Application log count DTO
/// </summary>
public class ApplicationLogCountDto
{
    public string Application { get; set; } = string.Empty;
    public int Count { get; set; }
    public int ErrorCount { get; set; }
    public double ErrorPercentage => Count > 0 ? Math.Round((double)ErrorCount / Count * 100, 1) : 0;
}

/// <summary>
/// Hourly log count DTO
/// </summary>
public class HourlyLogCountDto
{
    public DateTime Hour { get; set; }
    public int TotalCount { get; set; }
    public int ErrorCount { get; set; }
    public int WarningCount { get; set; }
    public int InfoCount { get; set; }
}

/// <summary>
/// Recent log entry DTO
/// </summary>
public class RecentLogEntryDto
{
    public DateTime Timestamp { get; set; }
    public string Level { get; set; } = string.Empty;
    public string Application { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string? UserId { get; set; }
    public Dictionary<string, object> Properties { get; set; } = new();
    public string? Exception { get; set; }
    public bool HasException { get; set; }
    public int? HttpStatusCode { get; set; }
    public int ExecutionDuration { get; set; }
    public string? ServiceName { get; set; }
    public string? MethodName { get; set; }
}

/// <summary>
/// Top error DTO
/// </summary>
public class TopErrorDto
{
    public string ErrorMessage { get; set; } = string.Empty;
    public string? ExceptionType { get; set; }
    public int Count { get; set; }
    public DateTime LastOccurrence { get; set; }
    public List<string> AffectedApplications { get; set; } = new();
}

/// <summary>
/// Performance metric DTO
/// </summary>
public class PerformanceMetricDto
{
    public string Operation { get; set; } = string.Empty;
    public double AvgDuration { get; set; }
    public double MaxDuration { get; set; }
    public int ExecutionCount { get; set; }
    public int SlowExecutions { get; set; }
    public DateTime LastExecution { get; set; }
}

/// <summary>
/// Log search request DTO
/// </summary>
public class LogSearchRequestDto
{
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
    public List<string> LogLevels { get; set; } = new();
    public List<string> Applications { get; set; } = new();
    public string? SearchText { get; set; }
    public string? UserId { get; set; }
    public string? Category { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = LogAnalyticsDashboardConstants.DefaultValues.DefaultPageSize;

    public void ValidateAndSetDefaults()
    {
        if (Page <= 0) Page = 1;
        if (PageSize <= 0) PageSize = LogAnalyticsDashboardConstants.DefaultValues.DefaultPageSize;
        if (PageSize > LogAnalyticsDashboardConstants.DefaultValues.MaxPageSize) 
            PageSize = LogAnalyticsDashboardConstants.DefaultValues.MaxPageSize;
    }
}

/// <summary>
/// Log search response DTO
/// </summary>
public class LogSearchResponseDto
{
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalPages { get; set; }
    public List<RecentLogEntryDto> Items { get; set; } = new();
    public int TotalCount { get; set; }
    
    public LogSearchResponseDto()
    {
        Items = new List<RecentLogEntryDto>();
    }
    
    public LogSearchResponseDto(int totalCount, List<RecentLogEntryDto> items, int page, int pageSize)
    {
        TotalCount = totalCount;
        Items = items ?? new List<RecentLogEntryDto>();
        Page = page;
        PageSize = pageSize;
        TotalPages = (int)Math.Ceiling((double)totalCount / pageSize);
    }
}

/// <summary>
/// System health response DTO
/// </summary>
public class SystemHealthDto
{
    public string Status { get; set; } = LogAnalyticsDashboardConstants.HealthStatus.Unknown;
    public int RecentErrors { get; set; }
    public int RecentCritical { get; set; }
    public double AvgResponseTime { get; set; }
    public DateTime LastCheck { get; set; } = DateTime.UtcNow;
    public Dictionary<string, object> AdditionalInfo { get; set; } = new();
}

/// <summary>
/// Export logs request DTO
/// </summary>
public class ExportLogsRequestDto
{
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
    public List<string> LogLevels { get; set; } = new();
    public List<string> Applications { get; set; } = new();
    public string? SearchText { get; set; }
    public string? UserId { get; set; }
    public string? Category { get; set; }
    public string Format { get; set; } = LogAnalyticsDashboardConstants.ExportFormats.Csv;
    public int MaxRecords { get; set; } = 10000;

    public void ValidateAndSetDefaults()
    {
        if (string.IsNullOrWhiteSpace(Format))
            Format = LogAnalyticsDashboardConstants.ExportFormats.Csv;
            
        Format = Format.ToLowerInvariant();
        
        if (MaxRecords <= 0 || MaxRecords > 50000)
            MaxRecords = 10000;
    }
}

/// <summary>
/// Applications list response DTO
/// </summary>
public class ApplicationsListDto
{
    public List<string> Applications { get; set; } = new();
    public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
}