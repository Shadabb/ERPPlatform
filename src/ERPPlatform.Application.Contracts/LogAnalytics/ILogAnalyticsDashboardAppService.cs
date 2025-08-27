using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace ERPPlatform.LogAnalytics;

/// <summary>
/// Application service for log analytics dashboard operations
/// Provides comprehensive logging analytics, audit log insights, and system health monitoring
/// </summary>
public interface ILogAnalyticsDashboardAppService : IApplicationService
{
    #region Dashboard Operations
    
    /// <summary>
    /// Gets dashboard data for the default time range (last 7 days)
    /// </summary>
    /// <returns>Complete dashboard data including statistics, trends, and recent activities</returns>
    Task<LogAnalyticsDashboardDto> GetDashboardDataAsync();
    
    /// <summary>
    /// Gets dashboard data for a specific date range with optional customizations
    /// </summary>
    /// <param name="request">Dashboard range request with date filters and options</param>
    /// <returns>Complete dashboard data for the specified range</returns>
    Task<LogAnalyticsDashboardDto> GetDashboardDataByRangeAsync(DashboardRangeRequestDto request);
    
    /// <summary>
    /// Gets system health status and metrics
    /// </summary>
    /// <returns>Current system health information</returns>
    Task<SystemHealthDto> GetSystemHealthAsync();
    
    /// <summary>
    /// Gets list of applications that have generated logs
    /// </summary>
    /// <returns>List of application names with metadata</returns>
    Task<ApplicationsListDto> GetApplicationsAsync();
    
    #endregion
    
    #region Log Search and Export
    
    /// <summary>
    /// Searches logs with advanced filtering and pagination
    /// </summary>
    /// <param name="request">Search request with filters and pagination</param>
    /// <returns>Paginated search results</returns>
    Task<LogSearchResponseDto> SearchLogsAsync(LogSearchRequestDto request);
    
    /// <summary>
    /// Exports logs to specified format (CSV, JSON, Excel)
    /// </summary>
    /// <param name="request">Export request with filters and format</param>
    /// <returns>Exported data as byte array</returns>
    Task<byte[]> ExportLogsAsync(ExportLogsRequestDto request);
    
    /// <summary>
    /// Gets recent log entries with pagination
    /// </summary>
    /// <param name="request">Recent logs request with pagination</param>
    /// <returns>Paginated recent log entries</returns>
    Task<PaginatedResponse<RecentLogEntryDto>> GetRecentLogsAsync(RecentLogsRequestDto request);
    
    #endregion
    
    #region Audit Log Operations
    
    /// <summary>
    /// Gets audit log statistics for a date range
    /// </summary>
    /// <param name="request">Statistics request with date range</param>
    /// <returns>Comprehensive audit log statistics</returns>
    Task<AuditLogStatisticsDto> GetAuditLogStatisticsAsync(AuditLogSearchRequestDto request);
    
    /// <summary>
    /// Searches audit logs with advanced filtering
    /// </summary>
    /// <param name="request">Audit log search request with filters</param>
    /// <returns>Paginated audit log search results</returns>
    Task<AuditLogSearchResponseDto> SearchAuditLogsAsync(AuditLogSearchRequestDto request);
    
    /// <summary>
    /// Gets recent audit logs with pagination
    /// </summary>
    /// <param name="request">Recent audit logs request with pagination</param>
    /// <returns>Paginated recent audit log entries</returns>
    Task<PaginatedResponse<RecentAuditLogDto>> GetRecentAuditLogsAsync(RecentAuditLogsRequestDto request);
    
    /// <summary>
    /// Exports audit logs to specified format
    /// </summary>
    /// <param name="request">Export audit logs request with filters and format</param>
    /// <returns>Exported audit log data as byte array</returns>
    Task<byte[]> ExportAuditLogsAsync(ExportAuditLogsRequestDto request);
    
    #endregion
    
    #region Analytics and Insights
    
    /// <summary>
    /// Gets top user activities with filtering options
    /// </summary>
    /// <param name="request">Top user activities request</param>
    /// <returns>List of top user activities with metrics</returns>
    Task<PaginatedResponse<TopUserActivityDto>> GetTopUserActivitiesAsync(TopUserActivitiesRequestDto request);
    
    /// <summary>
    /// Gets top audit methods with performance metrics
    /// </summary>
    /// <param name="request">Top audit methods request</param>
    /// <returns>List of top audit methods with performance data</returns>
    Task<PaginatedResponse<AuditLogMethodCountDto>> GetTopAuditMethodsAsync(TopAuditMethodsRequestDto request);
    
    #endregion
}