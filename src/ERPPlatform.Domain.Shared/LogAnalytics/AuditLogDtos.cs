using System;
using System.Collections.Generic;

namespace ERPPlatform.LogAnalytics;

/// <summary>
/// Constants for audit log analytics
/// </summary>
public static class AuditLogConstants
{
    public static class Filters
    {
        public const string All = "All";
        public const string Success = "Success";
        public const string Failed = "Failed";
        public const string WithException = "WithException";
        public const string WithoutException = "WithoutException";
    }

    public static class HttpMethods
    {
        public const string Get = "GET";
        public const string Post = "POST";
        public const string Put = "PUT";
        public const string Delete = "DELETE";
        public const string Patch = "PATCH";
    }

    public static class DefaultValues
    {
        public const int DefaultPageSize = 20;
        public const int MaxPageSize = 1000;
        public const int DefaultTopCount = 10;
        public const int SlowExecutionThreshold = 5000; // milliseconds
    }
}

/// <summary>
/// Base DTO for audit log requests
/// </summary>
public abstract class AuditLogBaseRequestDto
{
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
}

/// <summary>
/// Audit log statistics DTO
/// </summary>
public class AuditLogStatisticsDto
{
    public int TotalAuditLogs { get; set; }
    public int TodayAuditLogs { get; set; }
    public int SuccessfulOperations { get; set; }
    public int FailedOperations { get; set; }
    public double AvgExecutionDuration { get; set; }
    public int UniqueUsers { get; set; }
    public int UniqueServices { get; set; }
    public double SuccessRate => TotalAuditLogs > 0 ? Math.Round((double)SuccessfulOperations / TotalAuditLogs * 100, 1) : 0;
    public double FailureRate => TotalAuditLogs > 0 ? Math.Round((double)FailedOperations / TotalAuditLogs * 100, 1) : 0;
}

/// <summary>
/// Audit log search request DTO
/// </summary>
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
    public int PageSize { get; set; } = AuditLogConstants.DefaultValues.DefaultPageSize;

    public void ValidateAndSetDefaults()
    {
        if (Page <= 0) Page = 1;
        if (PageSize <= 0) PageSize = AuditLogConstants.DefaultValues.DefaultPageSize;
        if (PageSize > AuditLogConstants.DefaultValues.MaxPageSize) 
            PageSize = AuditLogConstants.DefaultValues.MaxPageSize;
            
        // Validate duration range
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
/// Audit log search response DTO
/// </summary>
public class AuditLogSearchResponseDto
{
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalPages { get; set; }
    
    public List<RecentAuditLogDto> Items { get; set; } = new();
    public int TotalCount { get; set; }
    
    public AuditLogSearchResponseDto()
    {
        Items = new List<RecentAuditLogDto>();
    }
    
    public AuditLogSearchResponseDto(int totalCount, List<RecentAuditLogDto> items, int page, int pageSize)
    {
        TotalCount = totalCount;
        Items = items ?? new List<RecentAuditLogDto>();
        Page = page;
        PageSize = pageSize;
        TotalPages = (int)Math.Ceiling((double)totalCount / pageSize);
    }
}

/// <summary>
/// Recent audit log DTO
/// </summary>
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
    
    /// <summary>
    /// Indicates if this is a slow operation
    /// </summary>
    public bool IsSlowOperation => ExecutionDuration > AuditLogConstants.DefaultValues.SlowExecutionThreshold;
    
    /// <summary>
    /// Gets the status category based on HTTP status code and exceptions
    /// </summary>
    public string StatusCategory
    {
        get
        {
            if (HasException) return "Error";
            if (HttpStatusCode.HasValue && HttpStatusCode >= 400) return "ClientError";
            if (HttpStatusCode.HasValue && HttpStatusCode >= 500) return "ServerError";
            return "Success";
        }
    }
}

/// <summary>
/// Top user activity DTO
/// </summary>
public class TopUserActivityDto
{
    public string UserId { get; set; } = string.Empty;
    public string? UserName { get; set; }
    public int ActivityCount { get; set; }
    public int SuccessfulOperations { get; set; }
    public int FailedOperations { get; set; }
    public DateTime LastActivity { get; set; }
    public double AvgExecutionTime { get; set; }
    
    /// <summary>
    /// Success rate percentage
    /// </summary>
    public double SuccessRate => ActivityCount > 0 ? Math.Round((double)SuccessfulOperations / ActivityCount * 100, 1) : 0;
    
    /// <summary>
    /// Failure rate percentage
    /// </summary>
    public double FailureRate => ActivityCount > 0 ? Math.Round((double)FailedOperations / ActivityCount * 100, 1) : 0;
}

/// <summary>
/// Audit log method count DTO
/// </summary>
public class AuditLogMethodCountDto
{
    public string ServiceName { get; set; } = string.Empty;
    public string MethodName { get; set; } = string.Empty;
    public int CallCount { get; set; }
    public int FailureCount { get; set; }
    public double AvgDuration { get; set; }
    public double MaxDuration { get; set; }
    public DateTime LastCalled { get; set; }
    
    /// <summary>
    /// Success rate percentage
    /// </summary>
    public double SuccessRate => CallCount > 0 ? Math.Round((double)(CallCount - FailureCount) / CallCount * 100, 1) : 0;
    
    /// <summary>
    /// Failure rate percentage
    /// </summary>
    public double FailureRate => CallCount > 0 ? Math.Round((double)FailureCount / CallCount * 100, 1) : 0;
    
    /// <summary>
    /// Indicates if this method has performance issues
    /// </summary>
    public bool HasPerformanceIssues => AvgDuration > AuditLogConstants.DefaultValues.SlowExecutionThreshold || 
                                       FailureRate > 10;
    
    /// <summary>
    /// Full method signature
    /// </summary>
    public string FullMethodName => $"{ServiceName}.{MethodName}";
}

/// <summary>
/// Export audit logs request DTO
/// </summary>
public class ExportAuditLogsRequestDto
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
    public string Format { get; set; } = "csv";
    public int MaxRecords { get; set; } = 10000;

    public void ValidateAndSetDefaults()
    {
        if (string.IsNullOrWhiteSpace(Format))
            Format = "csv";
            
        Format = Format.ToLowerInvariant();
        
        if (MaxRecords <= 0 || MaxRecords > 50000)
            MaxRecords = 10000;
            
        // Validate duration range
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
/// Top user activities request DTO
/// </summary>
public class TopUserActivitiesRequestDto : AuditLogBaseRequestDto
{
    public int Count { get; set; } = AuditLogConstants.DefaultValues.DefaultTopCount;
    public bool IncludeSystemUsers { get; set; } = false;
    public string? FilterByServiceName { get; set; }

    public void ValidateAndSetDefaults()
    {
        if (Count <= 0) Count = AuditLogConstants.DefaultValues.DefaultTopCount;
        if (Count > 100) Count = 100; // Reasonable maximum
    }
}

/// <summary>
/// Top audit methods request DTO
/// </summary>
public class TopAuditMethodsRequestDto : AuditLogBaseRequestDto
{
    public int Count { get; set; } = AuditLogConstants.DefaultValues.DefaultTopCount;
    public bool IncludeFailuresOnly { get; set; } = false;
    public bool IncludeSlowOperationsOnly { get; set; } = false;
    public string? FilterByServiceName { get; set; }

    public void ValidateAndSetDefaults()
    {
        if (Count <= 0) Count = AuditLogConstants.DefaultValues.DefaultTopCount;
        if (Count > 100) Count = 100; // Reasonable maximum
    }
}

/// <summary>
/// Paginated response DTO for generic pagination
/// </summary>
public class PaginatedResponse<T> where T : class
{
    public List<T> Items { get; set; } = new();
    public int TotalCount { get; set; }
    public int Skip { get; set; }
    public int Take { get; set; }
    public bool HasMore { get; set; }
    
    /// <summary>
    /// Total pages based on take size
    /// </summary>
    public int TotalPages => Take > 0 ? (int)Math.Ceiling((double)TotalCount / Take) : 0;
    
    /// <summary>
    /// Current page number (1-based)
    /// </summary>
    public int CurrentPage => Take > 0 ? (Skip / Take) + 1 : 1;
}

/// <summary>
/// Recent audit logs request DTO with pagination
/// </summary>
public class RecentAuditLogsRequestDto
{
    public int Skip { get; set; } = 0;
    public int Take { get; set; } = AuditLogConstants.DefaultValues.DefaultPageSize;
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
    public string? UserId { get; set; }
    public bool? HasException { get; set; }

    public void ValidateAndSetDefaults()
    {
        if (Skip < 0) Skip = 0;
        if (Take <= 0) Take = AuditLogConstants.DefaultValues.DefaultPageSize;
        if (Take > AuditLogConstants.DefaultValues.MaxPageSize) 
            Take = AuditLogConstants.DefaultValues.MaxPageSize;
    }
}

/// <summary>
/// Recent logs request DTO with pagination
/// </summary>
public class RecentLogsRequestDto
{
    public int Skip { get; set; } = 0;
    public int Take { get; set; } = AuditLogConstants.DefaultValues.DefaultPageSize;
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
    public List<string> LogLevels { get; set; } = new();
    public List<string> Applications { get; set; } = new();

    public void ValidateAndSetDefaults()
    {
        if (Skip < 0) Skip = 0;
        if (Take <= 0) Take = AuditLogConstants.DefaultValues.DefaultPageSize;
        if (Take > AuditLogConstants.DefaultValues.MaxPageSize) 
            Take = AuditLogConstants.DefaultValues.MaxPageSize;
    }
}