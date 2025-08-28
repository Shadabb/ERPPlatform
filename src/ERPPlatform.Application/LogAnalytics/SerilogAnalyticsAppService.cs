using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Logging;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Caching;
using ERPPlatform.LogAnalytics.Helpers;
using ERPPlatform.Permissions;

namespace ERPPlatform.LogAnalytics;

/// <summary>
/// ABP-compliant Serilog analytics application service
/// Uses the actual seriloglogs table created by Serilog.Sinks.PostgreSQL
/// Provides comprehensive application performance and error analytics with proper authorization and caching
/// </summary>
[Authorize(ERPPlatformPermissions.LogAnalytics.SerilogDashboard)]
public class SerilogAnalyticsAppService : ApplicationService, ISerilogAnalyticsAppService
{
    private readonly SerilogEntryRepository _serilogRepository;
    private readonly SerilogAnalyticsHelper _analyticsHelper;
    private readonly IDistributedCache<SerilogDashboardDto> _dashboardCache;

    public SerilogAnalyticsAppService(
        SerilogEntryRepository serilogRepository,
        SerilogAnalyticsHelper analyticsHelper,
        IDistributedCache<SerilogDashboardDto> dashboardCache)
    {
        _serilogRepository = serilogRepository;
        _analyticsHelper = analyticsHelper;
        _dashboardCache = dashboardCache;
    }

    #region Dashboard Operations

    public virtual async Task<SerilogDashboardDto> GetSerilogDashboardAsync()
    {
        var request = new SerilogDashboardRequestDto
        {
            FromDate = DateTime.Now.AddDays(-7),
            ToDate = DateTime.Now,
            TopErrorsCount = 10,
            TopEndpointsCount = 10,
            SlowRequestsCount = 20
        };

        return await GetSerilogDashboardByRangeAsync(request);
    }

    public virtual async Task<SerilogDashboardDto> GetSerilogDashboardByRangeAsync(SerilogDashboardRequestDto request)
    {
        Check.NotNull(request, nameof(request));
        
        request = _analyticsHelper.ValidateDashboardRequest(request);

        Logger.LogInformation("Generating Serilog dashboard for range {FromDate} to {ToDate}",
            request.FromDate, request.ToDate);

        try
        {
            var dashboard = new SerilogDashboardDto();

            // Execute dashboard queries sequentially to avoid DbContext concurrency issues
            dashboard.Statistics = await GetSerilogStatisticsAsync(request);
            dashboard.LogLevelDistribution = await GetLogLevelDistributionAsync(request);
            dashboard.RecentLogs = await GetRecentSerilogEntriesAsync(50);
            dashboard.TopErrors = await GetTopSerilogErrorsAsync(request.TopErrorsCount);

            if (request.IncludeHourlyTrends)
            {
                dashboard.HourlyTrends = await GetHourlyTrendsAsync(request);
            }

            if (request.IncludePerformanceMetrics)
            {
                dashboard.Performance = await GetSystemPerformanceAsync();
                dashboard.SlowRequests = await GetSlowRequestsAsync(request.SlowRequestsCount);
                dashboard.TopEndpoints = await GetEndpointStatisticsAsync(request);
            }

            Logger.LogInformation("Serilog dashboard generated successfully with {TotalLogs} total logs",
                dashboard.Statistics.TotalLogs);

            return dashboard;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error generating Serilog dashboard for range {FromDate} to {ToDate}",
                request.FromDate, request.ToDate);
            throw new UserFriendlyException("Failed to generate Serilog analytics dashboard. Please try again.");
        }
    }

    public async Task<SystemPerformanceDto> GetSystemPerformanceAsync()
    {
        try
        {
            var logs = await _serilogRepository.GetListAsync(DateTime.Now.AddHours(-24), DateTime.Now, null, null, false, 0, 10000);

            if (!logs.Any())
            {
                return new SystemPerformanceDto
                {
                    HealthStatus = "Unknown",
                    AdditionalMetrics = new Dictionary<string, object>
                    {
                        ["Message"] = "No HTTP request data available"
                    }
                };
            }

            // For now, return basic metrics. TODO: Extract performance data from LogEvent JSON
            var errorCount = logs.Count(x => x.IsError);
            var totalRequests = logs.Count;

            var avgResponseTime = 0.0; // TODO: Extract from LogEvent
            var (p95, p99) = (0.0, 0.0); // TODO: Extract from LogEvent
            var errorRate = _analyticsHelper.CalculatePercentage(errorCount, totalRequests);
            var requestsPerMinute = totalRequests / (24.0 * 60.0); // Rough calculation
            var errorsPerMinute = errorCount / (24.0 * 60.0); // Rough calculation

            return new SystemPerformanceDto
            {
                AvgResponseTime = Math.Round(avgResponseTime, 2),
                P95ResponseTime = p95,
                P99ResponseTime = p99,
                RequestsPerMinute = (int)Math.Round(requestsPerMinute),
                ErrorsPerMinute = (int)Math.Round(errorsPerMinute),
                Throughput = requestsPerMinute,
                HealthStatus = _analyticsHelper.GetSystemHealthStatus(errorRate, avgResponseTime, totalRequests),
                AdditionalMetrics = new Dictionary<string, object>
                {
                    ["ErrorRate"] = errorRate,
                    ["SuccessRate"] = 100 - errorRate,
                    ["TotalRequests"] = totalRequests,
                    ["TotalErrors"] = errorCount,
                    ["MedianResponseTime"] = 0 // TODO: Extract from LogEvent
                }
            };
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error calculating system performance metrics");
            return new SystemPerformanceDto
            {
                HealthStatus = "Unknown",
                AdditionalMetrics = new Dictionary<string, object>
                {
                    ["Error"] = "Unable to calculate performance metrics"
                }
            };
        }
    }

    #endregion

    #region Log Search and Analysis

    public async Task<SerilogSearchResponseDto> SearchSerilogEntriesAsync(SerilogSearchRequestDto request)
    {
        Check.NotNull(request, nameof(request));
        
        request = _analyticsHelper.ValidateSearchRequest(request);

        try
        {
            // Get recent logs with proper pagination - respect request parameters
            var totalLogsToGet = request.Page * request.PageSize; // Get enough logs for pagination
            var allLogs = await _serilogRepository.GetRecentAsync(totalLogsToGet);
            
            // Apply pagination
            var skip = (request.Page - 1) * request.PageSize;
            var pagedLogs = allLogs.Skip(skip).Take(request.PageSize).ToList();
            
            var mappedLogs = pagedLogs.Select(x => new SerilogEntryDto
            {
                TimeStamp = x.Timestamp ?? DateTime.MinValue,
                Level = x.LevelName,
                Message = x.Message ?? "No message",
                Exception = x.Exception,
                RequestPath = x.RequestPath,
                HttpMethod = x.HttpMethod,
                UserId = x.UserId,
                Properties = x.LogEvent ?? "{}"
            }).ToList();

            // Get the actual total count from database (not just the fetched logs)
            var totalCount = await _serilogRepository.GetTotalCountAsync();

            return new SerilogSearchResponseDto((int)totalCount, mappedLogs, request.Page, request.PageSize);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error searching Serilog entries");
            throw new UserFriendlyException("Failed to search log entries. Please try again.");
        }
    }

    public async Task<List<SerilogTopErrorDto>> GetRecentErrorsAsync(int count = 50)
    {
        try
        {
            var errorLogs = await _serilogRepository.GetRecentErrorsAsync(count, 24);

            if (!errorLogs.Any())
            {
                return new List<SerilogTopErrorDto>();
            }

            var errorGroups = errorLogs
                .GroupBy(x => _analyticsHelper.ExtractErrorMessage(x.Exception))
                .Select(g => new SerilogTopErrorDto
                {
                    ErrorMessage = g.Key ?? "Unknown Error",
                    ExceptionType = _analyticsHelper.ExtractExceptionType(g.First().Exception),
                    Count = g.Count(),
                    FirstOccurrence = g.Min(x => x.Timestamp) ?? DateTime.MinValue,
                    LastOccurrence = g.Max(x => x.Timestamp) ?? DateTime.MinValue,
                    AffectedEndpoints = g.Where(x => !string.IsNullOrEmpty(x.RequestPath))
                                        .Select(x => x.RequestPath!)
                                        .Distinct()
                                        .Take(5)
                                        .ToList(),
                    Level = g.First().LevelName
                })
                .OrderByDescending(x => x.Count)
                .ThenByDescending(x => x.LastOccurrence)
                .Take(count)
                .ToList();

            return errorGroups;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error retrieving recent errors");
            throw new UserFriendlyException("Failed to retrieve recent errors. Please try again.");
        }
    }

    public async Task<List<SerilogSlowRequestDto>> GetSlowRequestsAsync(int count = 50, int minDuration = 5000)
    {
        try
        {
            // For now, return empty list. TODO: Extract request duration from LogEvent JSON
            return new List<SerilogSlowRequestDto>();
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error retrieving slow requests");
            throw new UserFriendlyException("Failed to retrieve slow requests. Please try again.");
        }
    }

    #endregion

    #region Analytics and Insights

    public async Task<List<SerilogEndpointStatsDto>> GetEndpointStatisticsAsync(SerilogDashboardRequestDto request)
    {
        try
        {
            // For now, return empty list. TODO: Extract endpoint data from LogEvent JSON
            return new List<SerilogEndpointStatsDto>();
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error calculating endpoint statistics");
            throw new UserFriendlyException("Failed to calculate endpoint statistics. Please try again.");
        }
    }

    public async Task<List<SerilogHourlyTrendDto>> GetHourlyTrendsAsync(SerilogDashboardRequestDto request)
    {
        try
        {
            var fromDate = request.FromDate ?? DateTime.Today.AddDays(-1);
            var toDate = request.ToDate ?? DateTime.Now;
                
            var hourlyData = await _serilogRepository.GetHourlyTrendsAsync(fromDate, toDate);

            return hourlyData.Select(x => new SerilogHourlyTrendDto
            {
                Hour = x.Hour,
                TotalCount = x.TotalCount,
                TotalRequests = x.TotalCount, // Same as total count for now
                ErrorCount = x.ErrorCount,
                WarningCount = x.WarningCount,
                InfoCount = x.InfoCount,
                AvgResponseTime = 0, // TODO: Extract from LogEvent
                SlowRequestCount = 0 // TODO: Extract from LogEvent
            }).ToList();
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error calculating hourly trends");
            throw new UserFriendlyException("Failed to calculate hourly trends. Please try again.");
        }
    }

    public async Task<List<SerilogLevelCountDto>> GetLogLevelDistributionAsync(SerilogDashboardRequestDto request)
    {
        try
        {
            var fromDate = request.FromDate ?? DateTime.Today.AddDays(-7);
            var toDate = request.ToDate ?? DateTime.Now;

            // Get log level counts from the repository
            var levelCounts = await _serilogRepository.GetLogLevelCountsAsync(fromDate, toDate);

            if (!levelCounts.Any())
            {
                return new List<SerilogLevelCountDto>();
            }

            var totalLogs = levelCounts.Values.Sum();
            
            return levelCounts
                .Select(kv => new SerilogLevelCountDto
                {
                    Level = kv.Key,
                    Count = kv.Value,
                    Percentage = _analyticsHelper.CalculatePercentage(kv.Value, totalLogs)
                })
                .OrderByDescending(x => _analyticsHelper.GetLogLevelPriority(x.Level))
                .ToList();
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error calculating log level distribution");
            throw new UserFriendlyException("Failed to calculate log level distribution. Please try again.");
        }
    }

    #endregion

    #region Export and Reporting

    public async Task<byte[]> ExportSerilogDataAsync(SerilogSearchRequestDto request, string format = "csv")
    {
        Check.NotNull(request, nameof(request));
        
        format = format.ToLowerInvariant();
        request = _analyticsHelper.ValidateSearchRequest(request);
        request.PageSize = 10000; // Export more records

        Logger.LogInformation("Exporting Serilog data in {Format} format", format);

        try
        {
            var searchResult = await SearchSerilogEntriesAsync(request);

            return format switch
            {
                "json" => await ExportAsJsonAsync(searchResult.Items),
                "csv" => await ExportAsCsvAsync(searchResult.Items),
                _ => await ExportAsCsvAsync(searchResult.Items)
            };
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error exporting Serilog data in {Format} format", format);
            throw new UserFriendlyException($"Failed to export data in {format} format. Please try again.");
        }
    }

    public async Task<byte[]> GeneratePerformanceReportAsync(SerilogDashboardRequestDto request)
    {
        // For now, return CSV report. In future, could generate PDF
        var dashboard = await GetSerilogDashboardByRangeAsync(request);
        
        var report = new StringBuilder();
        report.AppendLine("Serilog Analytics Performance Report");
        report.AppendLine($"Generated: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");
        report.AppendLine($"Period: {(request.FromDate?.ToString("yyyy-MM-dd") ?? "7 days ago")} to {(request.ToDate?.ToString("yyyy-MM-dd") ?? "now")}");
        report.AppendLine();
        
        report.AppendLine("SUMMARY STATISTICS");
        report.AppendLine($"Total Logs: {dashboard.Statistics.TotalLogs:N0}");
        report.AppendLine($"Total Requests: {dashboard.Statistics.TotalRequests:N0}");
        report.AppendLine($"Error Rate: {dashboard.Statistics.ErrorRate:F2}%");
        report.AppendLine($"Average Response Time: {dashboard.Statistics.AvgResponseTime:F2}ms");
        report.AppendLine($"Slow Requests: {dashboard.Statistics.SlowRequestCount:N0}");
        report.AppendLine();
        
        report.AppendLine("SYSTEM HEALTH");
        report.AppendLine($"Status: {dashboard.Performance.HealthStatus}");
        report.AppendLine($"Requests per Minute: {dashboard.Performance.RequestsPerMinute:N0}");
        report.AppendLine($"P95 Response Time: {dashboard.Performance.P95ResponseTime:F2}ms");
        report.AppendLine($"P99 Response Time: {dashboard.Performance.P99ResponseTime:F2}ms");
        
        return Encoding.UTF8.GetBytes(report.ToString());
    }

    #endregion

    #region Private Helper Methods

    private async Task<SerilogStatisticsDto> GetSerilogStatisticsAsync(SerilogDashboardRequestDto request)
    {
        var fromDate = request.FromDate ?? DateTime.Today.AddDays(-7);
        var toDate = request.ToDate ?? DateTime.Now;

        // Get the actual total count from database without date filtering to match Recent Logs behavior
        var totalLogs = await _serilogRepository.GetTotalCountAsync();
        
        // Get today's logs count
        var todayLogs = await _serilogRepository.GetTotalCountAsync(DateTime.Today, DateTime.Today.AddDays(1));

        // Get log level counts without date filtering to match working behavior
        var levelCounts = await _serilogRepository.GetLogLevelCountsAsync();

        var errorCount = levelCounts.GetValueOrDefault("Error", 0) + levelCounts.GetValueOrDefault("Fatal", 0);
        var warningCount = levelCounts.GetValueOrDefault("Warning", 0);
        var infoCount = levelCounts.GetValueOrDefault("Information", 0);

        // For now, return basic statistics
        // TODO: Extract HTTP-specific metrics from LogEvent JSON when available
        return new SerilogStatisticsDto
        {
            TotalLogs = (int)totalLogs,
            TodayLogs = todayLogs,
            ErrorCount = errorCount,
            WarningCount = warningCount,
            InfoCount = infoCount,
            TotalRequests = 0, // Will be calculated from LogEvent when available
            AvgResponseTime = 0, // Will be calculated from LogEvent when available
            SlowRequestCount = 0, // Will be calculated from LogEvent when available
            Http4xxCount = 0, // Will be calculated from LogEvent when available
            Http5xxCount = 0 // Will be calculated from LogEvent when available
        };
    }

    private async Task<List<SerilogRecentEntryDto>> GetRecentSerilogEntriesAsync(int count)
    {
        var recentLogs = await _serilogRepository.GetRecentAsync(count);

        return recentLogs
            .Select(x => new SerilogRecentEntryDto
            {
                TimeStamp = x.Timestamp ?? DateTime.MinValue,
                Level = x.LevelName,
                Message = x.Message ?? "No message",
                Exception = x.Exception,
                Application = x.Application,
                RequestPath = x.RequestPath,
                HttpMethod = x.HttpMethod,
                UserId = x.UserId
            })
            .ToList();
    }

    private async Task<List<SerilogTopErrorDto>> GetTopSerilogErrorsAsync(int count)
    {
        return await GetRecentErrorsAsync(count);
    }

    private async Task<byte[]> ExportAsJsonAsync(IReadOnlyList<SerilogEntryDto> entries)
    {
        var json = JsonSerializer.Serialize(entries, new JsonSerializerOptions
        {
            WriteIndented = true
        });
        return Encoding.UTF8.GetBytes(json);
    }

    private async Task<byte[]> ExportAsCsvAsync(IReadOnlyList<SerilogEntryDto> entries)
    {
        var csv = new StringBuilder();
        csv.AppendLine("TimeStamp,Level,Message,RequestPath,HttpMethod,Duration,ResponseStatusCode,UserId,HasException");

        foreach (var entry in entries)
        {
            csv.AppendLine($"\"{_analyticsHelper.FormatTimestampForExport(entry.TimeStamp)}\"," +
                          $"\"{entry.Level}\"," +
                          $"\"{_analyticsHelper.EscapeCsvValue(entry.Message)}\"," +
                          $"\"{entry.RequestPath}\"," +
                          $"\"{entry.HttpMethod}\"," +
                          $"\"{entry.Duration}\"," +
                          $"\"{entry.ResponseStatusCode}\"," +
                          $"\"{entry.UserId}\"," +
                          $"\"{!string.IsNullOrEmpty(entry.Exception)}\"");
        }

        return await Task.FromResult(Encoding.UTF8.GetBytes(csv.ToString()));
    }

    #endregion
}