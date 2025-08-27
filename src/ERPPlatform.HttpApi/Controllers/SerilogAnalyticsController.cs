using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Volo.Abp;
using Volo.Abp.AspNetCore.Mvc;
using ERPPlatform.LogAnalytics;

namespace ERPPlatform.Controllers;

/// <summary>
/// REST API controller for Serilog Analytics operations
/// Provides endpoints for application performance monitoring and error analysis
/// </summary>
[ApiController]
[Route("api/serilog-analytics")]
[Authorize]
public class SerilogAnalyticsController : AbpControllerBase
{
    private readonly ISerilogAnalyticsAppService _serilogAnalyticsAppService;

    public SerilogAnalyticsController(ISerilogAnalyticsAppService serilogAnalyticsAppService)
    {
        _serilogAnalyticsAppService = serilogAnalyticsAppService;
    }

    #region Dashboard Endpoints

    /// <summary>
    /// Gets the main Serilog analytics dashboard data for the last 24 hours
    /// </summary>
    /// <returns>Complete dashboard data with performance metrics and error analytics</returns>
    [HttpGet("dashboard")]
    public async Task<SerilogDashboardDto> GetDashboardAsync()
    {
        return await _serilogAnalyticsAppService.GetSerilogDashboardAsync();
    }

    /// <summary>
    /// Gets Serilog analytics dashboard data for a custom date range
    /// </summary>
    /// <param name="request">Dashboard request with date filters and options</param>
    /// <returns>Complete dashboard data for the specified range</returns>
    [HttpPost("dashboard")]
    public async Task<SerilogDashboardDto> GetDashboardAsync([FromBody] SerilogDashboardRequestDto request)
    {
        return await _serilogAnalyticsAppService.GetSerilogDashboardByRangeAsync(request);
    }

    /// <summary>
    /// Gets current system performance metrics
    /// </summary>
    /// <returns>Real-time system performance data</returns>
    [HttpGet("performance")]
    public async Task<SystemPerformanceDto> GetSystemPerformanceAsync()
    {
        return await _serilogAnalyticsAppService.GetSystemPerformanceAsync();
    }

    #endregion

    #region Search and Analysis Endpoints

    /// <summary>
    /// Searches Serilog entries with advanced filtering and pagination
    /// </summary>
    /// <param name="request">Search request with filters and pagination parameters</param>
    /// <returns>Paginated search results</returns>
    [HttpPost("search")]
    public async Task<SerilogSearchResponseDto> SearchLogsAsync([FromBody] SerilogSearchRequestDto request)
    {
        return await _serilogAnalyticsAppService.SearchSerilogEntriesAsync(request);
    }

    /// <summary>
    /// Gets recent error logs for troubleshooting
    /// </summary>
    /// <param name="count">Number of recent errors to retrieve (max 200)</param>
    /// <returns>List of recent error entries</returns>
    [HttpGet("errors/recent")]
    public async Task<List<SerilogTopErrorDto>> GetRecentErrorsAsync([FromQuery] int count = 50)
    {
        if (count > 200) count = 200; // Limit to prevent performance issues
        return await _serilogAnalyticsAppService.GetRecentErrorsAsync(count);
    }

    /// <summary>
    /// Gets slow requests for performance analysis
    /// </summary>
    /// <param name="count">Number of slow requests to retrieve (max 200)</param>
    /// <param name="minDuration">Minimum duration in milliseconds to consider slow (default: 5000)</param>
    /// <returns>List of slow requests</returns>
    [HttpGet("performance/slow-requests")]
    public async Task<List<SerilogSlowRequestDto>> GetSlowRequestsAsync(
        [FromQuery] int count = 50, 
        [FromQuery] int minDuration = 5000)
    {
        if (count > 200) count = 200; // Limit to prevent performance issues
        if (minDuration < 0) minDuration = 5000;
        
        return await _serilogAnalyticsAppService.GetSlowRequestsAsync(count, minDuration);
    }

    #endregion

    #region Analytics Endpoints

    /// <summary>
    /// Gets endpoint performance statistics
    /// </summary>
    /// <param name="fromDate">Start date for analysis (optional)</param>
    /// <param name="toDate">End date for analysis (optional)</param>
    /// <param name="topCount">Number of top endpoints to return (default: 10, max: 50)</param>
    /// <returns>List of endpoint performance metrics</returns>
    [HttpGet("analytics/endpoints")]
    public async Task<List<SerilogEndpointStatsDto>> GetEndpointStatisticsAsync(
        [FromQuery] DateTime? fromDate = null,
        [FromQuery] DateTime? toDate = null,
        [FromQuery] int topCount = 10)
    {
        if (topCount > 50) topCount = 50; // Reasonable limit
        
        var request = new SerilogDashboardRequestDto
        {
            FromDate = fromDate,
            ToDate = toDate,
            TopEndpointsCount = topCount
        };
        
        return await _serilogAnalyticsAppService.GetEndpointStatisticsAsync(request);
    }

    /// <summary>
    /// Gets hourly trends for performance monitoring
    /// </summary>
    /// <param name="fromDate">Start date for trends (optional)</param>
    /// <param name="toDate">End date for trends (optional)</param>
    /// <returns>Hourly trend data</returns>
    [HttpGet("analytics/trends/hourly")]
    public async Task<List<SerilogHourlyTrendDto>> GetHourlyTrendsAsync(
        [FromQuery] DateTime? fromDate = null,
        [FromQuery] DateTime? toDate = null)
    {
        var request = new SerilogDashboardRequestDto
        {
            FromDate = fromDate,
            ToDate = toDate
        };
        
        return await _serilogAnalyticsAppService.GetHourlyTrendsAsync(request);
    }

    /// <summary>
    /// Gets log level distribution for the specified period
    /// </summary>
    /// <param name="fromDate">Start date for analysis (optional)</param>
    /// <param name="toDate">End date for analysis (optional)</param>
    /// <returns>Log level distribution data</returns>
    [HttpGet("analytics/log-levels")]
    public async Task<List<SerilogLevelCountDto>> GetLogLevelDistributionAsync(
        [FromQuery] DateTime? fromDate = null,
        [FromQuery] DateTime? toDate = null)
    {
        var request = new SerilogDashboardRequestDto
        {
            FromDate = fromDate,
            ToDate = toDate
        };
        
        return await _serilogAnalyticsAppService.GetLogLevelDistributionAsync(request);
    }

    #endregion

    #region Export Endpoints

    /// <summary>
    /// Exports Serilog data to CSV format
    /// </summary>
    /// <param name="request">Search request with filters</param>
    /// <returns>CSV file as byte array</returns>
    [HttpPost("export/csv")]
    public async Task<IActionResult> ExportToCsvAsync([FromBody] SerilogSearchRequestDto request)
    {
        var data = await _serilogAnalyticsAppService.ExportSerilogDataAsync(request, "csv");
        var fileName = $"serilog-analytics-{DateTime.UtcNow:yyyyMMdd-HHmmss}.csv";
        
        return File(data, "text/csv", fileName);
    }

    /// <summary>
    /// Exports Serilog data to JSON format
    /// </summary>
    /// <param name="request">Search request with filters</param>
    /// <returns>JSON file as byte array</returns>
    [HttpPost("export/json")]
    public async Task<IActionResult> ExportToJsonAsync([FromBody] SerilogSearchRequestDto request)
    {
        var data = await _serilogAnalyticsAppService.ExportSerilogDataAsync(request, "json");
        var fileName = $"serilog-analytics-{DateTime.UtcNow:yyyyMMdd-HHmmss}.json";
        
        return File(data, "application/json", fileName);
    }

    /// <summary>
    /// Generates and downloads a performance report
    /// </summary>
    /// <param name="request">Dashboard request with date range</param>
    /// <returns>Performance report as downloadable file</returns>
    [HttpPost("reports/performance")]
    public async Task<IActionResult> GeneratePerformanceReportAsync([FromBody] SerilogDashboardRequestDto request)
    {
        var data = await _serilogAnalyticsAppService.GeneratePerformanceReportAsync(request);
        var fileName = $"performance-report-{DateTime.UtcNow:yyyyMMdd-HHmmss}.txt";
        
        return File(data, "text/plain", fileName);
    }

    #endregion

    #region Health Check Endpoints

    /// <summary>
    /// Gets basic health status of the Serilog analytics system
    /// </summary>
    /// <returns>Health status information</returns>
    [HttpGet("health")]
    [AllowAnonymous] // Allow anonymous access for health checks
    public async Task<IActionResult> GetHealthAsync()
    {
        try
        {
            var performance = await _serilogAnalyticsAppService.GetSystemPerformanceAsync();
            
            return Ok(new
            {
                Status = "Healthy",
                SystemHealth = performance.HealthStatus,
                Timestamp = DateTime.UtcNow,
                Version = "1.0.0"
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new
            {
                Status = "Unhealthy",
                Error = "Failed to retrieve system health",
                Timestamp = DateTime.UtcNow,
                Details = ex.Message
            });
        }
    }

    #endregion

    #region Quick Access Endpoints

    /// <summary>
    /// Gets a quick summary of current system status
    /// </summary>
    /// <returns>Quick summary data for dashboards</returns>
    [HttpGet("summary")]
    public async Task<IActionResult> GetQuickSummaryAsync()
    {
        try
        {
            var dashboard = await _serilogAnalyticsAppService.GetSerilogDashboardAsync();
            
            return Ok(new
            {
                TotalLogs = dashboard.Statistics.TotalLogs,
                ErrorRate = dashboard.Statistics.ErrorRate,
                AvgResponseTime = dashboard.Statistics.AvgResponseTime,
                SystemHealth = dashboard.Performance.HealthStatus,
                TopError = dashboard.TopErrors.FirstOrDefault()?.ErrorMessage ?? "None",
                SlowestEndpoint = dashboard.SlowRequests.FirstOrDefault()?.RequestPath ?? "None",
                LastUpdated = dashboard.GeneratedAt
            });
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to generate quick summary");
            return StatusCode(500, new { Error = "Failed to generate summary" });
        }
    }

    /// <summary>
    /// Gets system metrics in a format suitable for monitoring tools
    /// </summary>
    /// <returns>Metrics data for monitoring systems</returns>
    [HttpGet("metrics")]
    [AllowAnonymous] // Allow anonymous access for monitoring tools
    public async Task<IActionResult> GetMetricsAsync()
    {
        try
        {
            var performance = await _serilogAnalyticsAppService.GetSystemPerformanceAsync();
            
            // Return metrics in a simple key-value format
            var metrics = new Dictionary<string, object>
            {
                ["serilog_requests_per_minute"] = performance.RequestsPerMinute,
                ["serilog_errors_per_minute"] = performance.ErrorsPerMinute,
                ["serilog_avg_response_time_ms"] = performance.AvgResponseTime,
                ["serilog_p95_response_time_ms"] = performance.P95ResponseTime,
                ["serilog_p99_response_time_ms"] = performance.P99ResponseTime,
                ["serilog_health_status"] = performance.HealthStatus == "Healthy" ? 1 : 0,
                ["serilog_last_check"] = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
            };

            return Ok(metrics);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to generate metrics");
            return StatusCode(500, new { Error = "Failed to generate metrics" });
        }
    }

    #endregion
}