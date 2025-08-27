using ERPPlatform.LogAnalytics;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Volo.Abp.AspNetCore.Mvc;

namespace ERPPlatform.Controllers;

[ApiController]
[Route("api/log-analytics")]
public class LogAnalyticsDashboardController : AbpControllerBase
{
    private readonly ILogAnalyticsDashboardAppService _logAnalyticsService;

    public LogAnalyticsDashboardController(ILogAnalyticsDashboardAppService logAnalyticsService)
    {
        _logAnalyticsService = logAnalyticsService;
    }

    /// <summary>
    /// Get dashboard data for the last 7 days
    /// </summary>
    [HttpGet("dashboard")]
    public async Task<ActionResult<LogAnalyticsDashboardDto>> GetDashboardAsync()
    {
        var dashboard = await _logAnalyticsService.GetDashboardDataAsync();
        return Ok(dashboard);
    }

    /// <summary>
    /// Get dashboard data for a specific date range
    /// </summary>
    [HttpGet("dashboard/range")]
    public async Task<ActionResult<LogAnalyticsDashboardDto>> GetDashboardByRangeAsync(
        [FromQuery] DateTime fromDate,
        [FromQuery] DateTime toDate)
    {
        var request = new DashboardRangeRequestDto { FromDate = fromDate, ToDate = toDate };
        var dashboard = await _logAnalyticsService.GetDashboardDataByRangeAsync(request);
        return Ok(dashboard);
    }

    /// <summary>
    /// Search logs with filters
    /// </summary>
    [HttpPost("search")]
    [Microsoft.AspNetCore.Mvc.IgnoreAntiforgeryToken]
    public async Task<ActionResult<LogSearchResponseDto>> SearchLogsAsync([FromBody] LogSearchRequestDto request)
    {
        var result = await _logAnalyticsService.SearchLogsAsync(request);
        return Ok(result);
    }

    /// <summary>
    /// Get list of applications that have logs
    /// </summary>
    [HttpGet("applications")]
    public async Task<ActionResult<List<string>>> GetApplicationsAsync()
    {
        var applications = await _logAnalyticsService.GetApplicationsAsync();
        return Ok(applications);
    }


    /// <summary>
    /// Get system health status
    /// </summary>
    [HttpGet("system-health")]
    public async Task<ActionResult<Dictionary<string, object>>> GetSystemHealthAsync()
    {
        var health = await _logAnalyticsService.GetSystemHealthAsync();
        return Ok(health);
    }

    /// <summary>
    /// Get recent log entries with pagination
    /// </summary>
    [HttpGet("recent-logs/paginated")]
    public async Task<ActionResult<PaginatedResponse<RecentLogEntryDto>>> GetRecentLogsPaginatedAsync(
        [FromQuery] int skip = 0,
        [FromQuery] int take = 20)
    {
        var request = new RecentLogsRequestDto { Skip = skip, Take = take };
        var result = await _logAnalyticsService.GetRecentLogsAsync(request);
        return Ok(result);
    }

    /// <summary>
    /// Export logs to CSV or JSON
    /// </summary>
    [HttpPost("export")]
    [Microsoft.AspNetCore.Mvc.IgnoreAntiforgeryToken]
    public async Task<ActionResult> ExportLogsAsync(
        [FromBody] LogSearchRequestDto request,
        [FromQuery] string format = "csv")
    {
        var exportRequest = new ExportLogsRequestDto 
        { 
            FromDate = request.FromDate,
            ToDate = request.ToDate,
            LogLevels = request.LogLevels,
            Applications = request.Applications,
            SearchText = request.SearchText,
            UserId = request.UserId,
            Category = request.Category,
            Format = format 
        };
        var data = await _logAnalyticsService.ExportLogsAsync(exportRequest);
        
        var fileName = $"logs_export_{DateTime.UtcNow:yyyyMMdd_HHmmss}.{format.ToLower()}";
        var contentType = format.ToLower() == "json" ? "application/json" : "text/csv";
        
        return File(data, contentType, fileName);
    }

    #region ABP Audit Log Endpoints

    /// <summary>
    /// Get audit log statistics for a date range
    /// </summary>
    [HttpGet("audit-statistics")]
    public async Task<ActionResult<AuditLogStatisticsDto>> GetAuditLogStatisticsAsync(
        [FromQuery] DateTime fromDate,
        [FromQuery] DateTime toDate)
    {
        var request = new AuditLogSearchRequestDto { FromDate = fromDate, ToDate = toDate };
        var statistics = await _logAnalyticsService.GetAuditLogStatisticsAsync(request);
        return Ok(statistics);
    }

    /// <summary>
    /// Search audit logs with filters
    /// </summary>
    [HttpPost("audit-logs/search")]
    public async Task<ActionResult<AuditLogSearchResponseDto>> SearchAuditLogsAsync([FromBody] AuditLogSearchRequestDto request)
    {
        var result = await _logAnalyticsService.SearchAuditLogsAsync(request);
        return Ok(result);
    }

    /// <summary>
    /// Get recent audit log entries
    /// </summary>
    [HttpGet("audit-logs/recent")]
    public async Task<ActionResult<List<RecentAuditLogDto>>> GetRecentAuditLogsAsync([FromQuery] int count = 20)
    {
        var request = new RecentAuditLogsRequestDto { Take = count };
        var auditLogs = await _logAnalyticsService.GetRecentAuditLogsAsync(request);
        return Ok(auditLogs);
    }

    /// <summary>
    /// Get recent audit log entries with pagination
    /// </summary>
    [HttpGet("audit-logs/recent/paginated")]
    public async Task<ActionResult<PaginatedResponse<RecentAuditLogDto>>> GetRecentAuditLogsPaginatedAsync(
        [FromQuery] int skip = 0,
        [FromQuery] int take = 20)
    {
        var request = new RecentAuditLogsRequestDto { Skip = skip, Take = take };
        var result = await _logAnalyticsService.GetRecentAuditLogsAsync(request);
        return Ok(result);
    }

    /// <summary>
    /// Get top user activities
    /// </summary>
    [HttpGet("audit-logs/top-users")]
    public async Task<ActionResult<List<TopUserActivityDto>>> GetTopUserActivitiesAsync([FromQuery] int count = 10)
    {
        var request = new TopUserActivitiesRequestDto { Count = count };
        var activities = await _logAnalyticsService.GetTopUserActivitiesAsync(request);
        return Ok(activities);
    }

    /// <summary>
    /// Get top audit methods/operations
    /// </summary>
    [HttpGet("audit-logs/top-methods")]
    public async Task<ActionResult<List<AuditLogMethodCountDto>>> GetTopAuditMethodsAsync([FromQuery] int count = 10)
    {
        var request = new TopAuditMethodsRequestDto { Count = count };
        var methods = await _logAnalyticsService.GetTopAuditMethodsAsync(request);
        return Ok(methods);
    }

    /// <summary>
    /// Export audit logs to CSV or JSON
    /// </summary>
    [HttpPost("audit-logs/export")]
    public async Task<ActionResult> ExportAuditLogsAsync(
        [FromBody] AuditLogSearchRequestDto request,
        [FromQuery] string format = "csv")
    {
        var exportRequest = new ExportAuditLogsRequestDto 
        { 
            FromDate = request.FromDate,
            ToDate = request.ToDate,
            UserId = request.UserId,
            ServiceName = request.ServiceName,
            MethodName = request.MethodName,
            HttpMethod = request.HttpMethod,
            MinDuration = request.MinDuration,
            MaxDuration = request.MaxDuration,
            HasException = request.HasException,
            ClientIp = request.ClientIp,
            Format = format 
        };
        var data = await _logAnalyticsService.ExportAuditLogsAsync(exportRequest);
        
        var fileName = $"audit_logs_export_{DateTime.UtcNow:yyyyMMdd_HHmmss}.{format.ToLower()}";
        var contentType = format.ToLower() == "json" ? "application/json" : "text/csv";
        
        return File(data, contentType, fileName);
    }

    #endregion
}