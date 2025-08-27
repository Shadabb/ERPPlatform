using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;
using ERPPlatform.LogAnalytics.Helpers;

namespace ERPPlatform.LogAnalytics;

/// <summary>
/// Application service for Serilog-based analytics following ABP standards
/// Provides comprehensive application performance and error analytics
/// </summary>
public class SerilogAnalyticsAppService : ApplicationService, ISerilogAnalyticsAppService
{
    private readonly IRepository<ApplicationLog, int> _applicationLogRepository;
    private readonly SerilogAnalyticsHelper _analyticsHelper;

    public SerilogAnalyticsAppService(
        IRepository<ApplicationLog, int> applicationLogRepository,
        SerilogAnalyticsHelper analyticsHelper)
    {
        _applicationLogRepository = applicationLogRepository;
        _analyticsHelper = analyticsHelper;
    }

    #region Dashboard Operations

    public async Task<SerilogDashboardDto> GetSerilogDashboardAsync()
    {
        // Use LOCAL time to match our ApplicationLog storage strategy
        var request = new SerilogDashboardRequestDto
        {
            FromDate = DateTime.Now.AddHours(-24), // Local time instead of UTC
            ToDate = DateTime.Now,                 // Local time instead of UTC
            TopErrorsCount = 10,
            TopEndpointsCount = 10,
            SlowRequestsCount = 20
        };

        return await GetSerilogDashboardByRangeAsync(request);
    }

    public async Task<SerilogDashboardDto> GetSerilogDashboardByRangeAsync(SerilogDashboardRequestDto request)
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
            var last24Hours = DateTime.SpecifyKind(DateTime.Now.AddHours(-24), DateTimeKind.Unspecified);
            var logs = await _applicationLogRepository.GetListAsync(
                x => x.TimeStamp >= last24Hours && !string.IsNullOrEmpty(x.HttpMethod) && !string.IsNullOrEmpty(x.RequestPath));

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

            var durations = logs.Where(x => x.Duration.HasValue).Select(x => x.Duration!.Value).ToList();
            var errorCount = logs.Count(x => x.Level == "Error" || x.Level == "Fatal");
            var totalRequests = logs.Count;

            var avgResponseTime = durations.Any() ? durations.Average() : 0;
            var (p95, p99) = _analyticsHelper.CalculatePercentiles(durations);
            var errorRate = _analyticsHelper.CalculatePercentage(errorCount, totalRequests);
            var requestsPerMinute = _analyticsHelper.CalculateRatePerMinute(totalRequests, last24Hours, DateTime.UtcNow);
            var errorsPerMinute = _analyticsHelper.CalculateRatePerMinute(errorCount, last24Hours, DateTime.UtcNow);

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
                    ["MedianResponseTime"] = durations.Any() ? durations.OrderBy(x => x).Skip(durations.Count / 2).First() : 0
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

        Logger.LogInformation("Searching Serilog entries with filters: FromDate={FromDate}, ToDate={ToDate}, Page={Page}",
            request.FromDate, request.ToDate, request.Page);

        try
        {
            var query = await _applicationLogRepository.GetQueryableAsync();
            query = _analyticsHelper.ApplySearchFilters(query, request);

            var totalCount = query.Count();
            var logs = query
                .OrderByDescending(x => x.TimeStamp)
                .Skip((request.Page - 1) * request.PageSize)
                .Take(request.PageSize)
                .Select(x => _analyticsHelper.MapToSerilogEntryDto(x))
                .ToList();

            Logger.LogInformation("Serilog search completed: {TotalCount} total, {PageCount} in current page",
                totalCount, logs.Count);

            return new SerilogSearchResponseDto(totalCount, logs, request.Page, request.PageSize);
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
            var last24Hours = DateTime.SpecifyKind(DateTime.Now.AddHours(-24), DateTimeKind.Unspecified);
            var errorLogs = await _applicationLogRepository.GetListAsync(
                x => x.TimeStamp >= last24Hours && (x.Level == "Error" || x.Level == "Fatal"),
                includeDetails: false);

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
                    FirstOccurrence = g.Min(x => x.TimeStamp),
                    LastOccurrence = g.Max(x => x.TimeStamp),
                    AffectedEndpoints = g.Where(x => !string.IsNullOrEmpty(x.RequestPath))
                                        .Select(x => x.RequestPath!)
                                        .Distinct()
                                        .Take(5)
                                        .ToList(),
                    Level = g.First().Level
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
            var last24Hours = DateTime.SpecifyKind(DateTime.Now.AddHours(-24), DateTimeKind.Unspecified);
            var slowRequests = await _applicationLogRepository.GetListAsync(
                x => x.TimeStamp >= last24Hours && 
                     x.Duration.HasValue && 
                     x.Duration.Value >= minDuration &&
                     !string.IsNullOrEmpty(x.HttpMethod) && !string.IsNullOrEmpty(x.RequestPath));

            var result = slowRequests
                .OrderByDescending(x => x.Duration)
                .Take(count)
                .Select(x => new SerilogSlowRequestDto
                {
                    RequestPath = x.RequestPath ?? "Unknown",
                    HttpMethod = x.HttpMethod ?? "Unknown",
                    Duration = x.Duration!.Value,
                    TimeStamp = x.TimeStamp,
                    UserId = x.UserId,
                    ResponseStatusCode = x.ResponseStatusCode,
                    PerformanceLevel = _analyticsHelper.GetPerformanceLevel(x.Duration)
                })
                .ToList();

            return result;
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
            // Convert DateTime to unspecified kind to avoid PostgreSQL issues
            var fromDateUnspecified = request.FromDate.HasValue 
                ? DateTime.SpecifyKind(request.FromDate.Value, DateTimeKind.Unspecified)
                : DateTime.SpecifyKind(DateTime.Today.AddDays(-7), DateTimeKind.Unspecified);
            var toDateUnspecified = request.ToDate.HasValue 
                ? DateTime.SpecifyKind(request.ToDate.Value, DateTimeKind.Unspecified)
                : DateTime.SpecifyKind(DateTime.Now, DateTimeKind.Unspecified);
                
            var logs = await _applicationLogRepository.GetListAsync(
                x => x.TimeStamp >= fromDateUnspecified && 
                     x.TimeStamp <= toDateUnspecified &&
                     !string.IsNullOrEmpty(x.HttpMethod) && !string.IsNullOrEmpty(x.RequestPath));

            if (!logs.Any())
            {
                return new List<SerilogEndpointStatsDto>();
            }

            var endpointStats = logs
                .GroupBy(x => new { x.RequestPath, x.HttpMethod })
                .Select(g => new SerilogEndpointStatsDto
                {
                    Endpoint = g.Key.RequestPath ?? "Unknown",
                    HttpMethod = g.Key.HttpMethod ?? "Unknown",
                    RequestCount = g.Count(),
                    AvgDuration = g.Where(x => x.Duration.HasValue)
                                   .Select(x => x.Duration!.Value)
                                   .DefaultIfEmpty(0)
                                   .Average(),
                    MaxDuration = g.Where(x => x.Duration.HasValue)
                                   .Select(x => x.Duration!.Value)
                                   .DefaultIfEmpty(0)
                                   .Max(),
                    MinDuration = g.Where(x => x.Duration.HasValue)
                                   .Select(x => x.Duration!.Value)
                                   .DefaultIfEmpty(0)
                                   .Min(),
                    ErrorCount = g.Count(x => (x.Level == "Error" || x.Level == "Fatal") || (x.ResponseStatusCode.HasValue && x.ResponseStatusCode >= 400)),
                    SuccessCount = g.Count(x => !(x.Level == "Error" || x.Level == "Fatal") && (!x.ResponseStatusCode.HasValue || x.ResponseStatusCode < 400))
                })
                .OrderByDescending(x => x.RequestCount)
                .Take(request.TopEndpointsCount)
                .ToList();

            return endpointStats;
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
            // Convert DateTime to unspecified kind to avoid PostgreSQL issues
            var fromDateUnspecified = request.FromDate.HasValue 
                ? DateTime.SpecifyKind(request.FromDate.Value, DateTimeKind.Unspecified)
                : DateTime.SpecifyKind(DateTime.Today.AddDays(-7), DateTimeKind.Unspecified);
            var toDateUnspecified = request.ToDate.HasValue 
                ? DateTime.SpecifyKind(request.ToDate.Value, DateTimeKind.Unspecified)
                : DateTime.SpecifyKind(DateTime.Now, DateTimeKind.Unspecified);
                
            var logs = await _applicationLogRepository.GetListAsync(
                x => x.TimeStamp >= fromDateUnspecified && x.TimeStamp <= toDateUnspecified);

            if (!logs.Any())
            {
                return new List<SerilogHourlyTrendDto>();
            }

            var maxHours = Math.Min(24, (int)(toDateUnspecified - fromDateUnspecified).TotalHours + 1);
            return _analyticsHelper.GroupByHour(logs, maxHours);
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
            // Convert DateTime to unspecified kind to avoid PostgreSQL issues
            var fromDateUnspecified = request.FromDate.HasValue 
                ? DateTime.SpecifyKind(request.FromDate.Value, DateTimeKind.Unspecified)
                : DateTime.SpecifyKind(DateTime.Today.AddDays(-7), DateTimeKind.Unspecified);
            var toDateUnspecified = request.ToDate.HasValue 
                ? DateTime.SpecifyKind(request.ToDate.Value, DateTimeKind.Unspecified)
                : DateTime.SpecifyKind(DateTime.Now, DateTimeKind.Unspecified);
            
            var logs = await _applicationLogRepository.GetListAsync(
                x => x.TimeStamp >= fromDateUnspecified && x.TimeStamp <= toDateUnspecified);

            if (!logs.Any())
            {
                return new List<SerilogLevelCountDto>();
            }

            var totalLogs = logs.Count;
            var levelCounts = logs
                .GroupBy(x => x.Level)
                .Select(g => new SerilogLevelCountDto
                {
                    Level = g.Key,
                    Count = g.Count(),
                    Percentage = _analyticsHelper.CalculatePercentage(g.Count(), totalLogs)
                })
                .OrderByDescending(x => _analyticsHelper.GetLogLevelPriority(x.Level))
                .ToList();

            return levelCounts;
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
        // Convert DateTime to unspecified kind to avoid PostgreSQL issues
        var fromDateUnspecified = request.FromDate.HasValue 
            ? DateTime.SpecifyKind(request.FromDate.Value, DateTimeKind.Unspecified)
            : DateTime.SpecifyKind(DateTime.Today.AddDays(-7), DateTimeKind.Unspecified);
        var toDateUnspecified = request.ToDate.HasValue 
            ? DateTime.SpecifyKind(request.ToDate.Value, DateTimeKind.Unspecified)
            : DateTime.SpecifyKind(DateTime.Now, DateTimeKind.Unspecified);
            
        var allLogs = await _applicationLogRepository.GetListAsync(
            x => x.TimeStamp >= fromDateUnspecified && x.TimeStamp <= toDateUnspecified);

        var todayLogs = await _applicationLogRepository.CountAsync(x => x.TimeStamp.Date == DateTime.Today);

        var totalLogs = allLogs.Count;
        var errorCount = allLogs.Count(x => x.Level == "Error" || x.Level == "Fatal");
        var warningCount = allLogs.Count(x => x.Level == "Warning");
        var infoCount = totalLogs - errorCount - warningCount;
        var httpRequests = allLogs.Where(x => !string.IsNullOrEmpty(x.HttpMethod) && !string.IsNullOrEmpty(x.RequestPath)).ToList();
        var totalRequests = httpRequests.Count;
        var avgResponseTime = httpRequests.Where(x => x.Duration.HasValue)
                                         .Select(x => x.Duration!.Value)
                                         .DefaultIfEmpty(0)
                                         .Average();
        var slowRequestCount = httpRequests.Count(x => x.IsSlowRequest);
        var http4xxCount = httpRequests.Count(x => x.ResponseStatusCode.HasValue && 
                                                   x.ResponseStatusCode >= 400 && 
                                                   x.ResponseStatusCode < 500);
        var http5xxCount = httpRequests.Count(x => x.ResponseStatusCode.HasValue && 
                                                   x.ResponseStatusCode >= 500);

        return new SerilogStatisticsDto
        {
            TotalLogs = totalLogs,
            TodayLogs = todayLogs,
            ErrorCount = errorCount,
            WarningCount = warningCount,
            InfoCount = infoCount,
            TotalRequests = totalRequests,
            AvgResponseTime = Math.Round(avgResponseTime, 2),
            SlowRequestCount = slowRequestCount,
            Http4xxCount = http4xxCount,
            Http5xxCount = http5xxCount
        };
    }

    private async Task<List<SerilogRecentEntryDto>> GetRecentSerilogEntriesAsync(int count)
    {
        var recentLogs = await _applicationLogRepository.GetListAsync(
            x => true, // Get all recent logs
            includeDetails: false);

        return recentLogs
            .OrderByDescending(x => x.TimeStamp)
            .Take(count)
            .Select(x => _analyticsHelper.MapToRecentEntryDto(x))
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