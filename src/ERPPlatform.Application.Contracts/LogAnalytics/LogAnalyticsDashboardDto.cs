using System;
using System.Collections.Generic;

namespace ERPPlatform.LogAnalytics;

public class LogAnalyticsDashboardDto
{
    public LogStatisticsDto Statistics { get; set; } = new();
    public List<LogLevelCountDto> LogLevelCounts { get; set; } = new();
    public List<ApplicationLogCountDto> ApplicationCounts { get; set; } = new();
    public List<HourlyLogCountDto> HourlyCounts { get; set; } = new();
    public List<RecentLogEntryDto> RecentLogs { get; set; } = new();
    public List<TopErrorDto> TopErrors { get; set; } = new();
    public List<PerformanceMetricDto> PerformanceMetrics { get; set; } = new();
    public AuditLogStatisticsDto AuditStatistics { get; set; } = new();
    public List<RecentAuditLogDto> RecentAuditLogs { get; set; } = new();
    public List<TopUserActivityDto> TopUserActivities { get; set; } = new();
    public List<AuditLogMethodCountDto> TopAuditMethods { get; set; } = new();
}

public class LogStatisticsDto
{
    public long TotalLogs { get; set; }
    public long TodayLogs { get; set; }
    public long ErrorCount { get; set; }
    public long WarningCount { get; set; }
    public long InfoCount { get; set; }
    public double AvgResponseTime { get; set; }
    public long SlowOperations { get; set; }
    public long SecurityEvents { get; set; }
    public long TotalAuditLogs { get; set; }
    public long TodayAuditLogs { get; set; }
    public long FailedOperations { get; set; }
}

public class LogLevelCountDto
{
    public string Level { get; set; } = string.Empty;
    public long Count { get; set; }
    public double Percentage { get; set; }
}

public class ApplicationLogCountDto
{
    public string Application { get; set; } = string.Empty;
    public long Count { get; set; }
    public long ErrorCount { get; set; }
}

public class HourlyLogCountDto
{
    public DateTime Hour { get; set; }
    public long TotalCount { get; set; }
    public long ErrorCount { get; set; }
    public long WarningCount { get; set; }
    public long InfoCount { get; set; }
}

public class RecentLogEntryDto
{
    public DateTime Timestamp { get; set; }
    public string Level { get; set; } = string.Empty;
    public string Application { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string? Exception { get; set; }
    public string? UserId { get; set; }
    public Dictionary<string, object> Properties { get; set; } = new();
    public bool HasException { get; set; }
    public int? HttpStatusCode { get; set; }
    public int ExecutionDuration { get; set; }
    public string? ServiceName { get; set; }
    public string? MethodName { get; set; }
}

public class TopErrorDto
{
    public string ErrorMessage { get; set; } = string.Empty;
    public string? ExceptionType { get; set; }
    public long Count { get; set; }
    public DateTime LastOccurrence { get; set; }
    public List<string> AffectedApplications { get; set; } = new();
}

public class PerformanceMetricDto
{
    public string Operation { get; set; } = string.Empty;
    public double AvgDuration { get; set; }
    public double MaxDuration { get; set; }
    public long ExecutionCount { get; set; }
    public long SlowExecutions { get; set; }
    public DateTime LastExecution { get; set; }
}

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
    public int PageSize { get; set; } = 50;
}

public class LogSearchResponseDto
{
    public List<RecentLogEntryDto> Logs { get; set; } = new();
    public long TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalPages { get; set; }
}

// ABP Audit Log DTOs
public class AuditLogStatisticsDto
{
    public long TotalAuditLogs { get; set; }
    public long TodayAuditLogs { get; set; }
    public long SuccessfulOperations { get; set; }
    public long FailedOperations { get; set; }
    public double AvgExecutionDuration { get; set; }
    public long UniqueUsers { get; set; }
    public long UniqueServices { get; set; }
}

public class RecentAuditLogDto
{
    public DateTime ExecutionTime { get; set; }
    public string? UserId { get; set; }
    public string? UserName { get; set; }
    public string ServiceName { get; set; } = string.Empty;
    public string MethodName { get; set; } = string.Empty;
    public int ExecutionDuration { get; set; }
    public string? ClientIpAddress { get; set; }
    public string? BrowserInfo { get; set; }
    public string? HttpMethod { get; set; }
    public string? Url { get; set; }
    public int? HttpStatusCode { get; set; }
    public bool HasException { get; set; }
    public string? Exception { get; set; }
}

public class TopUserActivityDto
{
    public string? UserId { get; set; }
    public string? UserName { get; set; }
    public long ActivityCount { get; set; }
    public long SuccessfulOperations { get; set; }
    public long FailedOperations { get; set; }
    public DateTime LastActivity { get; set; }
    public double AvgExecutionTime { get; set; }
}

public class AuditLogMethodCountDto
{
    public string ServiceName { get; set; } = string.Empty;
    public string MethodName { get; set; } = string.Empty;
    public long CallCount { get; set; }
    public long FailureCount { get; set; }
    public double AvgDuration { get; set; }
    public double MaxDuration { get; set; }
    public DateTime LastCalled { get; set; }
}

public class AuditLogSearchRequestDto
{
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
    public string? UserId { get; set; }
    public string? ServiceName { get; set; }
    public string? MethodName { get; set; }
    public string? HttpMethod { get; set; }
    public int? MinDuration { get; set; }
    public int? MaxDuration { get; set; }
    public bool? HasException { get; set; }
    public string? ClientIp { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 50;
}

public class AuditLogSearchResponseDto
{
    public List<RecentAuditLogDto> AuditLogs { get; set; } = new();
    public long TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalPages { get; set; }
}

public class PaginatedResponse<T>
{
    public List<T> Items { get; set; } = new();
    public long TotalCount { get; set; }
    public int Skip { get; set; }
    public int Take { get; set; }
    public bool HasMore { get; set; }
}