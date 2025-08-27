using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace ERPPlatform.LogAnalytics;

/// <summary>
/// Application service for Serilog-based analytics dashboard
/// Provides technical insights into application performance, errors, and system health
/// </summary>
public interface ISerilogAnalyticsAppService : IApplicationService
{
    #region Dashboard Operations
    
    /// <summary>
    /// Gets Serilog analytics dashboard data for the default time range (last 24 hours)
    /// </summary>
    /// <returns>Complete Serilog analytics dashboard data</returns>
    Task<SerilogDashboardDto> GetSerilogDashboardAsync();
    
    /// <summary>
    /// Gets Serilog analytics dashboard data for a specific date range
    /// </summary>
    /// <param name="request">Dashboard request with date filters and options</param>
    /// <returns>Complete Serilog analytics dashboard data for the specified range</returns>
    Task<SerilogDashboardDto> GetSerilogDashboardByRangeAsync(SerilogDashboardRequestDto request);
    
    /// <summary>
    /// Gets system performance metrics from Serilog data
    /// </summary>
    /// <returns>Current system performance information</returns>
    Task<SystemPerformanceDto> GetSystemPerformanceAsync();
    
    #endregion
    
    #region Log Search and Analysis
    
    /// <summary>
    /// Searches Serilog entries with advanced filtering
    /// </summary>
    /// <param name="request">Search request with filters and pagination</param>
    /// <returns>Paginated Serilog search results</returns>
    Task<SerilogSearchResponseDto> SearchSerilogEntriesAsync(SerilogSearchRequestDto request);
    
    /// <summary>
    /// Gets recent error logs for troubleshooting
    /// </summary>
    /// <param name="count">Number of recent errors to retrieve</param>
    /// <returns>List of recent error entries</returns>
    Task<List<SerilogTopErrorDto>> GetRecentErrorsAsync(int count = 50);
    
    /// <summary>
    /// Gets slow requests for performance analysis
    /// </summary>
    /// <param name="count">Number of slow requests to retrieve</param>
    /// <param name="minDuration">Minimum duration in milliseconds to consider slow</param>
    /// <returns>List of slow requests</returns>
    Task<List<SerilogSlowRequestDto>> GetSlowRequestsAsync(int count = 50, int minDuration = 5000);
    
    #endregion
    
    #region Analytics and Insights
    
    /// <summary>
    /// Gets endpoint performance statistics
    /// </summary>
    /// <param name="request">Dashboard request with filters</param>
    /// <returns>List of endpoint performance metrics</returns>
    Task<List<SerilogEndpointStatsDto>> GetEndpointStatisticsAsync(SerilogDashboardRequestDto request);
    
    /// <summary>
    /// Gets hourly trends for performance monitoring
    /// </summary>
    /// <param name="request">Dashboard request with date range</param>
    /// <returns>Hourly trend data</returns>
    Task<List<SerilogHourlyTrendDto>> GetHourlyTrendsAsync(SerilogDashboardRequestDto request);
    
    /// <summary>
    /// Gets log level distribution for the specified period
    /// </summary>
    /// <param name="request">Dashboard request with date range</param>
    /// <returns>Log level distribution data</returns>
    Task<List<SerilogLevelCountDto>> GetLogLevelDistributionAsync(SerilogDashboardRequestDto request);
    
    #endregion
    
    #region Export and Reporting
    
    /// <summary>
    /// Exports Serilog analytics data to specified format
    /// </summary>
    /// <param name="request">Search request with filters</param>
    /// <param name="format">Export format (csv, json, xlsx)</param>
    /// <returns>Exported data as byte array</returns>
    Task<byte[]> ExportSerilogDataAsync(SerilogSearchRequestDto request, string format = "csv");
    
    /// <summary>
    /// Generates performance report for the specified period
    /// </summary>
    /// <param name="request">Dashboard request with date range</param>
    /// <returns>Performance report as byte array (PDF)</returns>
    Task<byte[]> GeneratePerformanceReportAsync(SerilogDashboardRequestDto request);
    
    #endregion
}