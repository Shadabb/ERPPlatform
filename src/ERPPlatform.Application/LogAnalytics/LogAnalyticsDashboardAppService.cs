using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;
using System.Text.Json;
using Volo.Abp.AuditLogging;
using Volo.Abp.Domain.Repositories;
using Microsoft.Extensions.Logging;

namespace ERPPlatform.LogAnalytics;

public class LogAnalyticsDashboardAppService : ApplicationService, ILogAnalyticsDashboardAppService
{
    private readonly IRepository<AuditLog, Guid> _auditLogRepository;

    public LogAnalyticsDashboardAppService(IRepository<AuditLog, Guid> auditLogRepository)
    {
        _auditLogRepository = auditLogRepository;
    }

    public async Task<LogAnalyticsDashboardDto> GetDashboardDataAsync()
    {
        var endDate = DateTime.UtcNow;
        var startDate = endDate.AddDays(-7); // Last 7 days
        return await GetDashboardDataByRangeAsync(startDate, endDate);
    }

    public async Task<LogAnalyticsDashboardDto> GetDashboardDataByRangeAsync(DateTime fromDate, DateTime toDate)
    {
        var dashboard = new LogAnalyticsDashboardDto();

        // Execute queries sequentially to avoid DbContext concurrency issues
        dashboard.AuditStatistics = await GetAuditLogStatisticsAsync(fromDate, toDate);
        dashboard.Statistics = await GetStatisticsAsync(fromDate, toDate);
        dashboard.LogLevelCounts = await GetLogLevelCountsAsync(fromDate, toDate);
        dashboard.ApplicationCounts = await GetApplicationCountsAsync(fromDate, toDate);
        dashboard.HourlyCounts = await GetHourlyTrendsAsync(fromDate, toDate);
        dashboard.RecentLogs = await GetRecentLogsAsync(20);
        dashboard.TopErrors = await GetTopErrorsAsync(10);
        dashboard.PerformanceMetrics = await GetPerformanceMetricsAsync(10);
        dashboard.RecentAuditLogs = await GetRecentAuditLogsAsync(20);
        dashboard.TopUserActivities = await GetTopUserActivitiesAsync(10);
        dashboard.TopAuditMethods = await GetTopAuditMethodsAsync(10);

        return dashboard;
    }

    public async Task<LogSearchResponseDto> SearchLogsAsync(LogSearchRequestDto request)
    {
        // Debug logging
        Logger.LogInformation("SearchLogsAsync called with FromDate: {FromDate}, ToDate: {ToDate}", 
            request.FromDate, request.ToDate);

        // Get real audit log data instead of mock data
        var auditLogsWithActions = await _auditLogRepository.GetListAsync(includeDetails: true);
        Logger.LogInformation("Retrieved {Count} audit logs from database", auditLogsWithActions.Count);
        
        // Convert audit logs to recent log format for consistency
        var allLogs = auditLogsWithActions
            .SelectMany(auditLog => auditLog.Actions?.Select(action => new RecentLogEntryDto
            {
                Timestamp = auditLog.ExecutionTime,
                Level = !string.IsNullOrEmpty(auditLog.Exceptions) ? "Error" : "Information",
                Application = GetApplicationNameFromService(action.ServiceName),
                Message = $"{action.ServiceName}.{action.MethodName}",
                UserId = auditLog.UserId?.ToString(),
                Exception = !string.IsNullOrEmpty(auditLog.Exceptions) ? auditLog.Exceptions : null,
                HasException = !string.IsNullOrEmpty(auditLog.Exceptions),
                HttpStatusCode = auditLog.HttpStatusCode,
                ExecutionDuration = auditLog.ExecutionDuration,
                ServiceName = action.ServiceName,
                MethodName = action.MethodName
            }) ?? new List<RecentLogEntryDto>())
            .AsQueryable();

        Logger.LogInformation("Converted to {Count} log entries after mapping", allLogs.Count());

        // Log sample timestamps before filtering
        var sampleLogs = allLogs.Take(3).ToList();
        foreach (var log in sampleLogs)
        {
            Logger.LogInformation("Sample log timestamp: {Timestamp}", log.Timestamp);
        }

        // Apply filters
        if (request.FromDate.HasValue)
        {
            Logger.LogInformation("Filtering by FromDate: {FromDate}", request.FromDate.Value);
            allLogs = allLogs.Where(l => l.Timestamp >= request.FromDate.Value);
        }
        
        if (request.ToDate.HasValue)
        {
            Logger.LogInformation("Filtering by ToDate: {ToDate}", request.ToDate.Value);
            allLogs = allLogs.Where(l => l.Timestamp <= request.ToDate.Value);
        }

        if (request.LogLevels.Any())
            allLogs = allLogs.Where(l => request.LogLevels.Contains(l.Level));

        if (request.Applications.Any())
            allLogs = allLogs.Where(l => request.Applications.Contains(l.Application));

        if (!string.IsNullOrWhiteSpace(request.SearchText))
        {
            var searchText = request.SearchText.ToLower();
            allLogs = allLogs.Where(l => l.Message.ToLower().Contains(searchText));
        }

        if (!string.IsNullOrWhiteSpace(request.UserId))
            allLogs = allLogs.Where(l => l.UserId == request.UserId);

        var totalCount = allLogs.Count();
        Logger.LogInformation("After filters applied: {Count} logs remain", totalCount);
        
        var logs = allLogs
            .OrderByDescending(l => l.Timestamp)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToList();

        Logger.LogInformation("Final result: {Count} logs after pagination", logs.Count);

        return new LogSearchResponseDto
        {
            Logs = logs,
            TotalCount = totalCount,
            Page = request.Page,
            PageSize = request.PageSize,
            TotalPages = (int)Math.Ceiling((double)totalCount / request.PageSize)
        };
    }

    public async Task<List<string>> GetApplicationsAsync()
    {
        // Get real applications from AuditLog data
        var auditLogs = await _auditLogRepository.GetListAsync(includeDetails: true);
        
        // Extract unique application names from audit log service names
        var applications = auditLogs
            .Where(auditLog => auditLog.Actions != null && auditLog.Actions.Any())
            .SelectMany(auditLog => auditLog.Actions!)
            .Select(action => GetApplicationNameFromService(action.ServiceName))
            .Distinct()
            .Where(app => !string.IsNullOrWhiteSpace(app))
            .OrderBy(app => app)
            .ToList();
            
        // If no applications found, return default
        if (!applications.Any())
        {
            applications.Add("ERPPlatform.Application");
        }
        
        return applications;
    }

    public async Task<List<string>> GetLogLevelsAsync()
    {
        // Get unique log levels from AuditLog data
        var auditLogs = await _auditLogRepository.GetListAsync(includeDetails: true);
        
        // Extract unique log levels - derive from exception presence and other criteria
        var logLevels = auditLogs
            .Where(auditLog => auditLog.Actions != null && auditLog.Actions.Any())
            .SelectMany(auditLog => auditLog.Actions!.Select(action => new
            {
                HasException = !string.IsNullOrEmpty(auditLog.Exceptions),
                StatusCode = auditLog.HttpStatusCode
            }))
            .Select(log => log.HasException ? "Error" : "Information")
            .Distinct()
            .Where(level => !string.IsNullOrWhiteSpace(level))
            .OrderBy(level => level)
            .ToList();
            
        // Ensure we have standard log levels
        var standardLevels = new[] { "Information", "Warning", "Error" };
        foreach (var level in standardLevels)
        {
            if (!logLevels.Contains(level))
            {
                logLevels.Add(level);
            }
        }
        
        return logLevels.OrderBy(x => x).ToList();
    }

    public async Task<List<TopErrorDto>> GetTopErrorsAsync(int count = 10)
    {
        // Get audit logs with exceptions and group by error type
        var auditLogs = await _auditLogRepository.GetListAsync(
            x => !string.IsNullOrEmpty(x.Exceptions),
            includeDetails: true);

        if (!auditLogs.Any())
        {
            return new List<TopErrorDto>();
        }

        var errorGroups = auditLogs
            .GroupBy(x => ExtractFirstLineOfException(x.Exceptions))
            .Select(g => new TopErrorDto
            {
                ErrorMessage = g.Key ?? "Unknown Error",
                ExceptionType = ExtractExceptionType(g.First().Exceptions),
                Count = g.Count(),
                LastOccurrence = g.Max(x => x.ExecutionTime),
                AffectedApplications = new List<string> { "ERPPlatform" }
            })
            .OrderByDescending(x => x.Count)
            .Take(count)
            .ToList();

        return errorGroups;
    }

    public async Task<List<PerformanceMetricDto>> GetPerformanceMetricsAsync(int count = 10)
    {
        // Use audit log data to show performance metrics
        var auditMethodCounts = await GetTopAuditMethodsAsync(count);
        
        var performanceMetrics = auditMethodCounts.Select(method => new PerformanceMetricDto
        {
            Operation = $"{method.ServiceName}.{method.MethodName}",
            AvgDuration = method.AvgDuration,
            MaxDuration = method.MaxDuration,
            ExecutionCount = method.CallCount,
            SlowExecutions = method.FailureCount, // Using failure count as slow executions for now
            LastExecution = method.LastCalled
        }).ToList();

        return performanceMetrics;
    }

    public async Task<List<HourlyLogCountDto>> GetHourlyTrendsAsync(DateTime fromDate, DateTime toDate)
    {
        // For dashboard, show trends from all audit logs (last 24 hours of activity)
        var last24Hours = DateTime.Now.AddHours(-24);
        var auditLogs = await _auditLogRepository.GetListAsync(
            x => x.ExecutionTime >= last24Hours);

        Logger.LogInformation("Hourly trends: Using last 24 hours from {StartTime}, found {Count} audit logs", 
            last24Hours, auditLogs.Count);

        if (!auditLogs.Any())
        {
            return new List<HourlyLogCountDto>();
        }

        // Group by hour and count
        var hourlyData = auditLogs
            .GroupBy(x => new DateTime(x.ExecutionTime.Year, x.ExecutionTime.Month, x.ExecutionTime.Day, x.ExecutionTime.Hour, 0, 0))
            .Select(g => new HourlyLogCountDto
            {
                Hour = g.Key,
                TotalCount = g.Count(),
                ErrorCount = g.Count(x => !string.IsNullOrEmpty(x.Exceptions)),
                WarningCount = 0, // No warning concept in audit logs
                InfoCount = g.Count(x => string.IsNullOrEmpty(x.Exceptions))
            })
            .OrderBy(x => x.Hour)
            .Take(24)
            .ToList();

        return hourlyData;
    }

    public async Task<Dictionary<string, object>> GetSystemHealthAsync()
    {
        // Get system health from real AuditLog data
        var auditLogs = await _auditLogRepository.GetListAsync(includeDetails: true);
        var recentLogs = auditLogs.Where(x => x.ExecutionTime >= DateTime.UtcNow.AddHours(-1)).ToList();
        
        var recentErrors = recentLogs.Count(x => !string.IsNullOrEmpty(x.Exceptions));
        var recentCritical = recentLogs.Count(x => !string.IsNullOrEmpty(x.Exceptions) && x.HttpStatusCode >= 500);
        var avgResponseTime = recentLogs.Any() ? recentLogs.Average(x => (double)x.ExecutionDuration) : 0;
        
        return new Dictionary<string, object>
        {
            ["Status"] = recentCritical > 0 ? "Critical" : recentErrors > 10 ? "Warning" : "Healthy",
            ["RecentErrors"] = recentErrors,
            ["RecentCritical"] = recentCritical,
            ["AvgResponseTime"] = Math.Round(avgResponseTime, 2),
            ["LastCheck"] = DateTime.UtcNow
        };
    }

    public async Task<byte[]> ExportLogsAsync(LogSearchRequestDto request, string format = "csv")
    {
        // Debug logging
        Logger.LogInformation("ExportLogsAsync called with FromDate: {FromDate}, ToDate: {ToDate}, Format: {Format}", 
            request.FromDate, request.ToDate, format);

        var logs = await SearchLogsAsync(new LogSearchRequestDto
        {
            FromDate = request.FromDate,
            ToDate = request.ToDate,
            LogLevels = request.LogLevels,
            Applications = request.Applications,
            SearchText = request.SearchText,
            UserId = request.UserId,
            Category = request.Category,
            Page = 1,
            PageSize = 10000 // Export up to 10k records
        });

        Logger.LogInformation("SearchLogsAsync returned {Count} logs", logs.Logs.Count);

        if (format.ToLower() == "json")
        {
            return Encoding.UTF8.GetBytes(JsonSerializer.Serialize(logs.Logs, new JsonSerializerOptions 
            { 
                WriteIndented = true 
            }));
        }

        // Default to CSV
        var csv = new StringBuilder();
        csv.AppendLine("Timestamp,Level,Application,Message,UserId,Exception");
        
        foreach (var log in logs.Logs)
        {
            csv.AppendLine($"\"{log.Timestamp:yyyy-MM-dd HH:mm:ss}\",\"{log.Level}\",\"{log.Application}\",\"{EscapeCsv(log.Message)}\",\"{log.UserId}\",\"{EscapeCsv(log.Exception)}\"");
        }

        Logger.LogInformation("Generated CSV with {Length} characters", csv.ToString().Length);
        return Encoding.UTF8.GetBytes(csv.ToString());
    }

    #region ABP Audit Log Methods

    public async Task<AuditLogStatisticsDto> GetAuditLogStatisticsAsync(DateTime fromDate, DateTime toDate)
    {
        // Get ALL audit logs for total count (dashboard should show overall statistics)
        var allAuditLogs = await _auditLogRepository.GetListAsync(includeDetails: true);
        var totalAuditLogs = allAuditLogs.Count;
        
        Logger.LogInformation("Total audit logs in database: {TotalCount}", totalAuditLogs);
        
        // Get audit logs within date range for other calculations  
        var auditLogsWithActions = await _auditLogRepository.GetListAsync(
            x => x.ExecutionTime >= fromDate && x.ExecutionTime <= toDate,
            includeDetails: true);

        Logger.LogInformation("Audit logs in date range {FromDate} to {ToDate}: {Count}", 
            fromDate, toDate, auditLogsWithActions.Count);
        
        // Get today's audit logs count (separate query as it's a different date range)
        var todayAuditLogs = await _auditLogRepository.CountAsync(x => 
            x.ExecutionTime.Date == DateTime.Today);

        var failedOperations = auditLogsWithActions.Count(x => !string.IsNullOrEmpty(x.Exceptions));
        var successfulOperations = totalAuditLogs - failedOperations;

        var avgExecutionDuration = auditLogsWithActions.Any() 
            ? auditLogsWithActions.Average(x => (double)x.ExecutionDuration) 
            : 0;

        var uniqueUsers = auditLogsWithActions
            .Where(x => x.UserId != null)
            .Select(x => x.UserId)
            .Distinct()
            .Count();

        var uniqueServices = auditLogsWithActions
            .SelectMany(x => x.Actions ?? new List<AuditLogAction>())
            .Select(x => x.ServiceName)
            .Where(x => !string.IsNullOrEmpty(x))
            .Distinct()
            .Count();

        return new AuditLogStatisticsDto
        {
            TotalAuditLogs = totalAuditLogs,
            TodayAuditLogs = todayAuditLogs,
            SuccessfulOperations = successfulOperations,
            FailedOperations = failedOperations,
            AvgExecutionDuration = avgExecutionDuration,
            UniqueUsers = uniqueUsers,
            UniqueServices = uniqueServices
        };
    }

    public async Task<AuditLogSearchResponseDto> SearchAuditLogsAsync(AuditLogSearchRequestDto request)
    {
        // Build predicate for filtering
        var auditLogs = await _auditLogRepository.GetListAsync(includeDetails: true);

        // Apply filters in memory (for simplicity, in production consider server-side filtering)
        var filteredLogs = auditLogs.AsQueryable();

        if (request.FromDate.HasValue)
            filteredLogs = filteredLogs.Where(x => x.ExecutionTime >= request.FromDate.Value);

        if (request.ToDate.HasValue)
            filteredLogs = filteredLogs.Where(x => x.ExecutionTime <= request.ToDate.Value);

        if (!string.IsNullOrWhiteSpace(request.UserId))
            filteredLogs = filteredLogs.Where(x => x.UserId != null && x.UserId.ToString() == request.UserId);

        if (!string.IsNullOrWhiteSpace(request.ServiceName))
            filteredLogs = filteredLogs.Where(x => x.Actions != null && x.Actions.Any(a => a.ServiceName != null && a.ServiceName.Contains(request.ServiceName)));

        if (!string.IsNullOrWhiteSpace(request.MethodName))
            filteredLogs = filteredLogs.Where(x => x.Actions != null && x.Actions.Any(a => a.MethodName != null && a.MethodName.Contains(request.MethodName)));

        if (!string.IsNullOrWhiteSpace(request.HttpMethod))
            filteredLogs = filteredLogs.Where(x => x.HttpMethod == request.HttpMethod);

        if (request.MinDuration.HasValue)
            filteredLogs = filteredLogs.Where(x => x.ExecutionDuration >= request.MinDuration.Value);

        if (request.MaxDuration.HasValue)
            filteredLogs = filteredLogs.Where(x => x.ExecutionDuration <= request.MaxDuration.Value);

        if (request.HasException.HasValue)
            filteredLogs = filteredLogs.Where(x => request.HasException.Value ? !string.IsNullOrEmpty(x.Exceptions) : string.IsNullOrEmpty(x.Exceptions));

        if (!string.IsNullOrWhiteSpace(request.ClientIp))
            filteredLogs = filteredLogs.Where(x => x.ClientIpAddress != null && x.ClientIpAddress.Contains(request.ClientIp));

        var totalCount = filteredLogs.Count();
        
        var result = filteredLogs
            .OrderByDescending(x => x.ExecutionTime)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(x => new RecentAuditLogDto
            {
                ExecutionTime = x.ExecutionTime,
                UserId = x.UserId != null ? x.UserId.ToString() : null,
                UserName = x.UserName,
                ServiceName = x.Actions != null && x.Actions.Any() ? x.Actions.First().ServiceName ?? "Unknown" : "Unknown",
                MethodName = x.Actions != null && x.Actions.Any() ? x.Actions.First().MethodName ?? "Unknown" : "Unknown",
                ExecutionDuration = x.ExecutionDuration,
                ClientIpAddress = x.ClientIpAddress,
                BrowserInfo = x.BrowserInfo,
                HttpMethod = x.HttpMethod,
                Url = x.Url,
                HttpStatusCode = x.HttpStatusCode,
                HasException = !string.IsNullOrEmpty(x.Exceptions),
                Exception = x.Exceptions
            })
            .ToList();

        return new AuditLogSearchResponseDto
        {
            AuditLogs = result,
            TotalCount = totalCount,
            Page = request.Page,
            PageSize = request.PageSize,
            TotalPages = (int)Math.Ceiling((double)totalCount / request.PageSize)
        };
    }

    public async Task<List<RecentAuditLogDto>> GetRecentAuditLogsAsync(int count = 20)
    {
        var auditLogs = await _auditLogRepository.GetListAsync(includeDetails: true);
        
        var result = auditLogs
            .OrderByDescending(x => x.ExecutionTime)
            .Take(count)
            .Select(x => new RecentAuditLogDto
            {
                ExecutionTime = x.ExecutionTime,
                UserId = x.UserId != null ? x.UserId.ToString() : null,
                UserName = x.UserName,
                ServiceName = x.Actions != null && x.Actions.Any() ? x.Actions.First().ServiceName ?? "Unknown" : "Unknown",
                MethodName = x.Actions != null && x.Actions.Any() ? x.Actions.First().MethodName ?? "Unknown" : "Unknown",
                ExecutionDuration = x.ExecutionDuration,
                ClientIpAddress = x.ClientIpAddress,
                BrowserInfo = x.BrowserInfo,
                HttpMethod = x.HttpMethod,
                Url = x.Url,
                HttpStatusCode = x.HttpStatusCode,
                HasException = !string.IsNullOrEmpty(x.Exceptions),
                Exception = x.Exceptions
            })
            .ToList();

        return result;
    }

    public async Task<List<TopUserActivityDto>> GetTopUserActivitiesAsync(int count = 10)
    {
        var auditLogs = await _auditLogRepository.GetListAsync(x => x.UserId != null);
        
        var userActivities = auditLogs
            .GroupBy(x => new { x.UserId, x.UserName })
            .Select(g => new TopUserActivityDto
            {
                UserId = g.Key.UserId.ToString(),
                UserName = g.Key.UserName,
                ActivityCount = g.Count(),
                SuccessfulOperations = g.Count(x => string.IsNullOrEmpty(x.Exceptions)),
                FailedOperations = g.Count(x => !string.IsNullOrEmpty(x.Exceptions)),
                LastActivity = g.Max(x => x.ExecutionTime),
                AvgExecutionTime = g.Any() ? g.Average(x => (double)x.ExecutionDuration) : 0
            })
            .OrderByDescending(x => x.ActivityCount)
            .Take(count)
            .ToList();

        return userActivities;
    }

    public async Task<List<AuditLogMethodCountDto>> GetTopAuditMethodsAsync(int count = 10)
    {
        var auditLogs = await _auditLogRepository.GetListAsync(includeDetails: true);
        
        var methodCounts = auditLogs
            .Where(x => x.Actions != null && x.Actions.Any())
            .SelectMany(x => x.Actions.Select(a => new { AuditLog = x, Action = a }))
            .GroupBy(x => new { x.Action.ServiceName, x.Action.MethodName })
            .Select(g => new AuditLogMethodCountDto
            {
                ServiceName = g.Key.ServiceName ?? "Unknown",
                MethodName = g.Key.MethodName ?? "Unknown", 
                CallCount = g.Count(),
                FailureCount = g.Count(x => !string.IsNullOrEmpty(x.AuditLog.Exceptions)),
                AvgDuration = g.Any() ? g.Average(x => (double)x.AuditLog.ExecutionDuration) : 0,
                MaxDuration = g.Any() ? g.Max(x => (double)x.AuditLog.ExecutionDuration) : 0,
                LastCalled = g.Max(x => x.AuditLog.ExecutionTime)
            })
            .OrderByDescending(x => x.CallCount)
            .Take(count)
            .ToList();

        return methodCounts;
    }

    public async Task<byte[]> ExportAuditLogsAsync(AuditLogSearchRequestDto request, string format = "csv")
    {
        var auditLogs = await SearchAuditLogsAsync(new AuditLogSearchRequestDto
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
            Page = 1,
            PageSize = 10000 // Export up to 10k records
        });

        if (format.ToLower() == "json")
        {
            return Encoding.UTF8.GetBytes(JsonSerializer.Serialize(auditLogs.AuditLogs, new JsonSerializerOptions 
            { 
                WriteIndented = true 
            }));
        }

        // Default to CSV
        var csv = new StringBuilder();
        csv.AppendLine("ExecutionTime,UserId,UserName,ServiceName,MethodName,ExecutionDuration,ClientIpAddress,HttpMethod,HttpStatusCode,HasException");
        
        foreach (var log in auditLogs.AuditLogs)
        {
            csv.AppendLine($"\"{log.ExecutionTime:yyyy-MM-dd HH:mm:ss}\",\"{log.UserId}\",\"{EscapeCsv(log.UserName)}\",\"{log.ServiceName}\",\"{log.MethodName}\",\"{log.ExecutionDuration}\",\"{log.ClientIpAddress}\",\"{log.HttpMethod}\",\"{log.HttpStatusCode}\",\"{log.HasException}\"");
        }

        return Encoding.UTF8.GetBytes(csv.ToString());
    }

    public async Task<PaginatedResponse<RecentAuditLogDto>> GetRecentAuditLogsPaginatedAsync(int skip = 0, int take = 20)
    {
        var auditLogs = await _auditLogRepository.GetListAsync(includeDetails: true);
        var totalCount = auditLogs.Count;
        
        var result = auditLogs
            .OrderByDescending(x => x.ExecutionTime)
            .Skip(skip)
            .Take(take)
            .Select(x => new RecentAuditLogDto
            {
                ExecutionTime = x.ExecutionTime,
                UserId = x.UserId != null ? x.UserId.ToString() : null,
                UserName = x.UserName,
                ServiceName = x.Actions != null && x.Actions.Any() ? x.Actions.First().ServiceName ?? "Unknown" : "Unknown",
                MethodName = x.Actions != null && x.Actions.Any() ? x.Actions.First().MethodName ?? "Unknown" : "Unknown",
                ExecutionDuration = x.ExecutionDuration,
                ClientIpAddress = x.ClientIpAddress,
                BrowserInfo = x.BrowserInfo,
                HttpMethod = x.HttpMethod,
                Url = x.Url,
                HttpStatusCode = x.HttpStatusCode,
                HasException = !string.IsNullOrEmpty(x.Exceptions),
                Exception = x.Exceptions
            })
            .ToList();

        return new PaginatedResponse<RecentAuditLogDto>
        {
            Items = result,
            TotalCount = totalCount,
            Skip = skip,
            Take = take,
            HasMore = skip + take < totalCount
        };
    }

    public async Task<PaginatedResponse<RecentLogEntryDto>> GetRecentLogsPaginatedAsync(int skip = 0, int take = 20)
    {
        // Get all audit logs and convert to log entries
        var auditLogs = await _auditLogRepository.GetListAsync(includeDetails: true);
        
        // Convert to log entries first, then apply pagination
        var allLogEntries = auditLogs
            .SelectMany(auditLog => auditLog.Actions?.Select(action => new RecentLogEntryDto
            {
                Timestamp = auditLog.ExecutionTime,
                Level = !string.IsNullOrEmpty(auditLog.Exceptions) ? "Error" : "Information",
                Application = "ERPPlatform",
                Message = $"{action.ServiceName}.{action.MethodName} - {auditLog.HttpMethod} {auditLog.HttpStatusCode}",
                UserId = auditLog.UserId?.ToString(),
                Exception = !string.IsNullOrEmpty(auditLog.Exceptions) ? auditLog.Exceptions : null,
                HasException = !string.IsNullOrEmpty(auditLog.Exceptions),
                HttpStatusCode = auditLog.HttpStatusCode,
                ExecutionDuration = auditLog.ExecutionDuration,
                ServiceName = action.ServiceName,
                MethodName = action.MethodName,
                Properties = new Dictionary<string, object>
                {
                    ["RequestId"] = Guid.NewGuid().ToString(),
                    ["Duration"] = auditLog.ExecutionDuration,
                    ["ServiceName"] = action.ServiceName ?? "Unknown",
                    ["MethodName"] = action.MethodName ?? "Unknown",
                    ["HttpStatusCode"] = auditLog.HttpStatusCode ?? 0
                }
            }) ?? new List<RecentLogEntryDto>())
            .OrderByDescending(x => x.Timestamp)
            .ToList();
        
        var totalCount = allLogEntries.Count;
        var paginatedItems = allLogEntries
            .Skip(skip)
            .Take(take)
            .ToList();

        return new PaginatedResponse<RecentLogEntryDto>
        {
            Items = paginatedItems,
            TotalCount = totalCount,
            Skip = skip,
            Take = take,
            HasMore = skip + take < totalCount
        };
    }

    #endregion

    #region Private Methods

    private async Task<LogStatisticsDto> GetStatisticsAsync(DateTime fromDate, DateTime toDate)
    {
        // Use audit log data for regular log statistics since no separate application log system is set up
        var auditStats = await GetAuditLogStatisticsAsync(fromDate, toDate);
        
        // Calculate error count based on HTTP status codes (4xx, 5xx)
        var auditLogsForStats = await _auditLogRepository.GetListAsync(
            x => x.ExecutionTime >= fromDate && x.ExecutionTime <= toDate,
            includeDetails: true);
            
        var errorCount = auditLogsForStats.Count(x => x.HttpStatusCode.HasValue && 
            (x.HttpStatusCode >= 400 && x.HttpStatusCode < 600));
            
        return new LogStatisticsDto
        {
            TotalLogs = auditStats.TotalAuditLogs,
            TodayLogs = auditStats.TodayAuditLogs,
            ErrorCount = errorCount, // HTTP 4xx/5xx status codes
            WarningCount = Math.Max(0, auditStats.TotalAuditLogs - auditStats.FailedOperations - auditStats.SuccessfulOperations),
            InfoCount = auditStats.SuccessfulOperations,
            AvgResponseTime = auditStats.AvgExecutionDuration,
            SlowOperations = auditLogsForStats.Count(x => x.ExecutionDuration > 5000), // Operations > 5 seconds
            SecurityEvents = auditStats.UniqueUsers,
            TotalAuditLogs = auditStats.TotalAuditLogs,
            TodayAuditLogs = auditStats.TodayAuditLogs,
            FailedOperations = auditStats.FailedOperations // Operations with exceptions
        };
    }

    private async Task<List<LogLevelCountDto>> GetLogLevelCountsAsync(DateTime fromDate, DateTime toDate)
    {
        // Use audit log data to generate log level counts
        var auditStats = await GetAuditLogStatisticsAsync(fromDate, toDate);
        var total = auditStats.TotalAuditLogs;
        
        if (total == 0)
        {
            return new List<LogLevelCountDto>
            {
                new() { Level = "Information", Count = 0, Percentage = 0 },
                new() { Level = "Warning", Count = 0, Percentage = 0 },
                new() { Level = "Error", Count = 0, Percentage = 0 }
            };
        }

        var errorCount = auditStats.FailedOperations;
        var infoCount = auditStats.SuccessfulOperations;
        var warningCount = Math.Max(0, total - errorCount - infoCount);

        return new List<LogLevelCountDto>
        {
            new() { Level = "Information", Count = infoCount, Percentage = Math.Round((double)infoCount / total * 100, 1) },
            new() { Level = "Warning", Count = warningCount, Percentage = Math.Round((double)warningCount / total * 100, 1) },
            new() { Level = "Error", Count = errorCount, Percentage = Math.Round((double)errorCount / total * 100, 1) }
        };
    }

    private async Task<List<ApplicationLogCountDto>> GetApplicationCountsAsync(DateTime fromDate, DateTime toDate)
    {
        // Use audit log data to show application counts
        var auditStats = await GetAuditLogStatisticsAsync(fromDate, toDate);
        
        if (auditStats.TotalAuditLogs == 0)
        {
            return new List<ApplicationLogCountDto>();
        }

        // For now, group all audit logs under the main application
        return new List<ApplicationLogCountDto>
        {
            new() { Application = "ERPPlatform", Count = auditStats.TotalAuditLogs, ErrorCount = auditStats.FailedOperations }
        };
    }

    private async Task<List<RecentLogEntryDto>> GetRecentLogsAsync(int count)
    {
        // Convert audit logs to regular log format for display
        var auditLogs = await GetRecentAuditLogsAsync(count);
        
        var logs = auditLogs.Select(audit => new RecentLogEntryDto
        {
            Timestamp = audit.ExecutionTime,
            Level = audit.HasException ? "Error" : "Information",
            Application = "ERPPlatform",
            Message = $"{audit.ServiceName}.{audit.MethodName} - {audit.HttpMethod} {audit.HttpStatusCode}",
            UserId = audit.UserId,
            Properties = new Dictionary<string, object>
            {
                ["RequestId"] = Guid.NewGuid().ToString(),
                ["Duration"] = audit.ExecutionDuration,
                ["ServiceName"] = audit.ServiceName ?? "Unknown",
                ["MethodName"] = audit.MethodName ?? "Unknown",
                ["HttpStatusCode"] = audit.HttpStatusCode ?? 0
            },
            Exception = audit.Exception,
            HasException = audit.HasException,
            HttpStatusCode = audit.HttpStatusCode,
            ExecutionDuration = audit.ExecutionDuration,
            ServiceName = audit.ServiceName,
            MethodName = audit.MethodName
        }).ToList();
        
        return logs;
    }

    private static string? ExtractPropertyValue(string? properties, string key)
    {
        if (string.IsNullOrWhiteSpace(properties)) return null;
        
        try
        {
            var json = JsonDocument.Parse(properties);
            if (json.RootElement.TryGetProperty(key, out var element))
            {
                return element.GetString();
            }
        }
        catch
        {
            // Fallback to simple string parsing
            var pattern = $"\"{key}\":\"";
            var startIndex = properties.IndexOf(pattern);
            if (startIndex >= 0)
            {
                startIndex += pattern.Length;
                var endIndex = properties.IndexOf("\"", startIndex);
                if (endIndex > startIndex)
                {
                    return properties.Substring(startIndex, endIndex - startIndex);
                }
            }
        }
        
        return null;
    }

    private static double ExtractNumericPropertyValue(string? properties, string key)
    {
        if (string.IsNullOrWhiteSpace(properties)) return 0;
        
        try
        {
            var json = JsonDocument.Parse(properties);
            if (json.RootElement.TryGetProperty(key, out var element))
            {
                return element.GetDouble();
            }
        }
        catch
        {
            // Fallback parsing
        }
        
        return 0;
    }

    private static Dictionary<string, object> ParseProperties(string? properties)
    {
        if (string.IsNullOrWhiteSpace(properties)) return new Dictionary<string, object>();
        
        try
        {
            return JsonSerializer.Deserialize<Dictionary<string, object>>(properties) ?? new Dictionary<string, object>();
        }
        catch
        {
            return new Dictionary<string, object>();
        }
    }

    private static string? ExtractExceptionType(string? exception)
    {
        if (string.IsNullOrWhiteSpace(exception)) return null;
        
        var lines = exception.Split('\n');
        var firstLine = lines[0].Trim();
        var colonIndex = firstLine.IndexOf(':');
        
        return colonIndex > 0 ? firstLine.Substring(0, colonIndex).Trim() : firstLine;
    }

    private static string? ExtractFirstLineOfException(string? exception)
    {
        if (string.IsNullOrWhiteSpace(exception)) return null;
        
        var lines = exception.Split('\n');
        var firstLine = lines[0].Trim();
        var colonIndex = firstLine.IndexOf(':');
        
        return colonIndex > 0 ? firstLine.Substring(colonIndex + 1).Trim() : firstLine;
    }

    private static string GetApplicationNameFromService(string serviceName)
    {
        if (string.IsNullOrWhiteSpace(serviceName))
            return "ERPPlatform.Unknown";
            
        // Map service names to application names
        if (serviceName.Contains("Controllers") || serviceName.Contains("Controller"))
            return "ERPPlatform.HttpApi.Host";
        else if (serviceName.Contains("AppService") || serviceName.Contains("Application"))
            return "ERPPlatform.Application";
        else if (serviceName.Contains("Web") || serviceName.Contains("Pages"))
            return "ERPPlatform.Web";
        else if (serviceName.Contains("AuthServer"))
            return "ERPPlatform.AuthServer";
        else
            return "ERPPlatform.Application"; // Default fallback
    }

    private static string EscapeCsv(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;
        return value.Replace("\"", "\"\"").Replace("\n", " ").Replace("\r", "");
    }

    #endregion
}