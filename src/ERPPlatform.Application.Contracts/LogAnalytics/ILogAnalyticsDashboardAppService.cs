using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace ERPPlatform.LogAnalytics;

public interface ILogAnalyticsDashboardAppService : IApplicationService
{
    Task<LogAnalyticsDashboardDto> GetDashboardDataAsync();
    Task<LogAnalyticsDashboardDto> GetDashboardDataByRangeAsync(DateTime fromDate, DateTime toDate);
    Task<LogSearchResponseDto> SearchLogsAsync(LogSearchRequestDto request);
    Task<List<string>> GetApplicationsAsync();
    Task<List<string>> GetLogLevelsAsync();
    Task<List<TopErrorDto>> GetTopErrorsAsync(int count = 10);
    Task<List<PerformanceMetricDto>> GetPerformanceMetricsAsync(int count = 10);
    Task<List<HourlyLogCountDto>> GetHourlyTrendsAsync(DateTime fromDate, DateTime toDate);
    Task<Dictionary<string, object>> GetSystemHealthAsync();
    Task<byte[]> ExportLogsAsync(LogSearchRequestDto request, string format = "csv");
    
    // ABP Audit Log Methods
    Task<AuditLogStatisticsDto> GetAuditLogStatisticsAsync(DateTime fromDate, DateTime toDate);
    Task<AuditLogSearchResponseDto> SearchAuditLogsAsync(AuditLogSearchRequestDto request);
    Task<List<RecentAuditLogDto>> GetRecentAuditLogsAsync(int count = 20);
    Task<PaginatedResponse<RecentAuditLogDto>> GetRecentAuditLogsPaginatedAsync(int skip = 0, int take = 20);
    Task<List<TopUserActivityDto>> GetTopUserActivitiesAsync(int count = 10);
    Task<List<AuditLogMethodCountDto>> GetTopAuditMethodsAsync(int count = 10);
    Task<byte[]> ExportAuditLogsAsync(AuditLogSearchRequestDto request, string format = "csv");
    
    // Recent Log Pagination Methods
    Task<PaginatedResponse<RecentLogEntryDto>> GetRecentLogsPaginatedAsync(int skip = 0, int take = 20);
}