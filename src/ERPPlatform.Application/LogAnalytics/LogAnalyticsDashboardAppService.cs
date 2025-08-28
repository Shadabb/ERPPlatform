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
using Volo.Abp.AuditLogging;
using Volo.Abp.Caching;
using Volo.Abp.Domain.Repositories;
using ERPPlatform.LogAnalytics.Helpers;
using ERPPlatform.Permissions;

namespace ERPPlatform.LogAnalytics;

/// <summary>
/// ABP-compliant log analytics dashboard service with proper authorization, caching, and AutoMapper integration
/// Follows ABP framework best practices for application services
/// </summary>
[Authorize(ERPPlatformPermissions.LogAnalytics.Default)]
public class LogAnalyticsDashboardAppService : ApplicationService, ILogAnalyticsDashboardAppService
{
    private readonly IRepository<AuditLog, Guid> _auditLogRepository;
    private readonly LogAnalyticsDashboardHelper _dashboardHelper;
    private readonly IDistributedCache<LogAnalyticsDashboardDto> _dashboardCache;
    private readonly IDistributedCache<SystemHealthDto> _healthCache;

    public LogAnalyticsDashboardAppService(
        IRepository<AuditLog, Guid> auditLogRepository,
        LogAnalyticsDashboardHelper dashboardHelper,
        IDistributedCache<LogAnalyticsDashboardDto> dashboardCache,
        IDistributedCache<SystemHealthDto> healthCache)
    {
        _auditLogRepository = auditLogRepository;
        _dashboardHelper = dashboardHelper;
        _dashboardCache = dashboardCache;
        _healthCache = healthCache;
    }

    #region Dashboard Operations

    [Authorize(ERPPlatformPermissions.LogAnalytics.Dashboard)]
    public virtual async Task<LogAnalyticsDashboardDto> GetDashboardDataAsync()
    {
        var request = new DashboardRangeRequestDto
        {
            FromDate = DateTime.UtcNow.AddDays(-LogAnalyticsDashboardConstants.DefaultValues.DefaultDashboardDays),
            ToDate = DateTime.UtcNow,
            TopCount = LogAnalyticsDashboardConstants.DefaultValues.DefaultTopCount
        };
        
        return await GetDashboardDataByRangeAsync(request);
    }

    [Authorize(ERPPlatformPermissions.LogAnalytics.Dashboard)]
    public virtual async Task<LogAnalyticsDashboardDto> GetDashboardDataByRangeAsync(DashboardRangeRequestDto request)
    {
        Check.NotNull(request, nameof(request));
        
        var (fromDate, toDate) = _dashboardHelper.ValidateDateRange(
            request.FromDate, 
            request.ToDate, 
            LogAnalyticsDashboardConstants.DefaultValues.DefaultDashboardDays);

        Logger.LogInformation("Getting dashboard data for range {FromDate} to {ToDate}", fromDate, toDate);

        var cacheKey = $"dashboard_{fromDate:yyyyMMdd}_{toDate:yyyyMMdd}_{request.TopCount}_{request.IncludeHourlyTrends}_{request.IncludePerformanceMetrics}";
        
        var cachedDashboard = await _dashboardCache.GetAsync(cacheKey);
        if (cachedDashboard != null)
        {
            Logger.LogDebug("Returning cached dashboard data for key {CacheKey}", cacheKey);
            return cachedDashboard;
        }

        try
        {
            var dashboard = new LogAnalyticsDashboardDto();

            // Get base audit statistics first
            var auditStatsRequest = new AuditLogSearchRequestDto { FromDate = fromDate, ToDate = toDate };
            dashboard.AuditStatistics = await GetAuditLogStatisticsAsync(auditStatsRequest);
            dashboard.Statistics = await GetLogStatisticsAsync(fromDate, toDate);

            // Get dashboard components sequentially to avoid DbContext concurrency issues
            dashboard.LogLevelCounts = await GetLogLevelCountsAsync(fromDate, toDate);
            dashboard.ApplicationCounts = await GetApplicationCountsAsync(fromDate, toDate);
            dashboard.RecentLogs = await GetRecentLogsForDashboardAsync(20);
            dashboard.TopErrors = await GetTopErrorsAsync(request.TopCount);
            dashboard.RecentAuditLogs = await GetRecentAuditLogsForDashboardAsync(20);
            dashboard.TopUserActivities = await GetTopUserActivitiesForDashboardAsync(request.TopCount);
            dashboard.TopAuditMethods = await GetTopAuditMethodsForDashboardAsync(request.TopCount);

            if (request.IncludeHourlyTrends)
            {
                dashboard.HourlyCounts = await GetHourlyTrendsAsync(fromDate, toDate);
            }

            if (request.IncludePerformanceMetrics)
            {
                dashboard.PerformanceMetrics = await GetPerformanceMetricsAsync(request.TopCount);
            }

            Logger.LogInformation("Dashboard data generated successfully with {TotalLogs} total audit logs", 
                dashboard.AuditStatistics.TotalAuditLogs);

            await _dashboardCache.SetAsync(cacheKey, dashboard, new Microsoft.Extensions.Caching.Distributed.DistributedCacheEntryOptions 
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5)
            });
            return dashboard;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error generating dashboard data for range {FromDate} to {ToDate}", fromDate, toDate);
            throw new UserFriendlyException("Failed to generate dashboard data. Please try again.");
        }
    }

    [Authorize(ERPPlatformPermissions.LogAnalytics.ViewLogs)]
    public virtual async Task<SystemHealthDto> GetSystemHealthAsync()
    {
        const string healthCacheKey = "system_health";
        var cachedHealth = await _healthCache.GetAsync(healthCacheKey);
        if (cachedHealth != null)
        {
            Logger.LogDebug("Returning cached system health data");
            return cachedHealth;
        }

        try
        {
            var oneHourAgo = DateTime.UtcNow.AddHours(-1);
            var oneHourAgoUnspecified = DateTime.SpecifyKind(oneHourAgo, DateTimeKind.Unspecified);
            
            var recentLogs = await _auditLogRepository.GetListAsync(
                x => x.ExecutionTime >= oneHourAgoUnspecified,
                includeDetails: true);

            var recentErrors = recentLogs.Count(x => !string.IsNullOrEmpty(x.Exceptions));
            var recentCritical = recentLogs.Count(x => !string.IsNullOrEmpty(x.Exceptions) && x.HttpStatusCode >= 500);
            var avgResponseTime = recentLogs.Any() ? recentLogs.Average(x => (double)x.ExecutionDuration) : 0;

            var status = _dashboardHelper.GetHealthStatus(recentErrors, recentCritical, avgResponseTime);

            var healthDto = new SystemHealthDto
            {
                Status = status,
                RecentErrors = recentErrors,
                RecentCritical = recentCritical,
                AvgResponseTime = Math.Round(avgResponseTime, 2),
                AdditionalInfo = new Dictionary<string, object>
                {
                    ["TotalRecentLogs"] = recentLogs.Count,
                    ["SuccessfulOperations"] = recentLogs.Count - recentErrors,
                    ["CheckPeriod"] = "Last 1 hour"
                }
            };

            await _healthCache.SetAsync(healthCacheKey, healthDto, new Microsoft.Extensions.Caching.Distributed.DistributedCacheEntryOptions 
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(2)
            });
            return healthDto;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error getting system health");
            return new SystemHealthDto
            {
                Status = LogAnalyticsDashboardConstants.HealthStatus.Unknown,
                AdditionalInfo = new Dictionary<string, object>
                {
                    ["Error"] = "Unable to determine system health"
                }
            };
        }
    }

    [Authorize(ERPPlatformPermissions.LogAnalytics.ViewLogs)]
    public virtual async Task<ApplicationsListDto> GetApplicationsAsync()
    {
        try
        {
            var auditLogs = await _auditLogRepository.GetListAsync(includeDetails: true);

            var applications = auditLogs
                .Where(x => x.Actions != null && x.Actions.Any())
                .SelectMany(x => x.Actions!)
                .Select(a => _dashboardHelper.GetApplicationNameFromService(a.ServiceName))
                .Distinct()
                .Where(app => !string.IsNullOrWhiteSpace(app))
                .OrderBy(app => app)
                .ToList();

            if (!applications.Any())
            {
                applications.Add(LogAnalyticsDashboardConstants.Applications.Application);
            }

            return new ApplicationsListDto
            {
                Applications = applications
            };
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error getting applications list");
            return new ApplicationsListDto
            {
                Applications = new List<string> { LogAnalyticsDashboardConstants.Applications.Application }
            };
        }
    }

    #endregion

    #region Log Search and Export

    [Authorize(ERPPlatformPermissions.LogAnalytics.SearchLogs)]
    public virtual async Task<LogSearchResponseDto> SearchLogsAsync(LogSearchRequestDto request)
    {
        Check.NotNull(request, nameof(request));
        
        request.ValidateAndSetDefaults();

        Logger.LogInformation("Searching logs with filters: FromDate={FromDate}, ToDate={ToDate}, Page={Page}, PageSize={PageSize}",
            request.FromDate, request.ToDate, request.Page, request.PageSize);

        try
        {
            var auditLogs = await _auditLogRepository.GetListAsync(includeDetails: true);

            // Convert audit logs to log entries
            var allLogs = auditLogs
                .SelectMany(auditLog => auditLog.Actions?.Select(action => new RecentLogEntryDto
                {
                    Timestamp = auditLog.ExecutionTime,
                    Level = _dashboardHelper.GetLogLevelFromAuditLog(!string.IsNullOrEmpty(auditLog.Exceptions), auditLog.HttpStatusCode),
                    Application = _dashboardHelper.GetApplicationNameFromService(action.ServiceName),
                    Message = $"{action.ServiceName}.{action.MethodName}",
                    UserId = auditLog.UserId?.ToString(),
                    Exception = auditLog.Exceptions,
                    HasException = !string.IsNullOrEmpty(auditLog.Exceptions),
                    HttpStatusCode = auditLog.HttpStatusCode,
                    ExecutionDuration = auditLog.ExecutionDuration,
                    ServiceName = action.ServiceName,
                    MethodName = action.MethodName,
                    Properties = new Dictionary<string, object>
                    {
                        ["Duration"] = auditLog.ExecutionDuration,
                        ["HttpStatusCode"] = auditLog.HttpStatusCode ?? 0,
                        ["ClientIp"] = auditLog.ClientIpAddress ?? "Unknown"
                    }
                }) ?? new List<RecentLogEntryDto>())
                .AsQueryable();

            // Apply filters
            allLogs = ApplyLogSearchFilters(allLogs, request);

            var totalCount = allLogs.Count();
            var pagedLogs = allLogs
                .OrderByDescending(l => l.Timestamp)
                .Skip((request.Page - 1) * request.PageSize)
                .Take(request.PageSize)
                .ToList();

            Logger.LogInformation("Log search completed: {TotalCount} total, {PageCount} in current page", 
                totalCount, pagedLogs.Count);

            return new LogSearchResponseDto(totalCount, pagedLogs, request.Page, request.PageSize);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error searching logs");
            throw new UserFriendlyException("Failed to search logs. Please try again.");
        }
    }

    [Authorize(ERPPlatformPermissions.LogAnalytics.ExportLogs)]
    public virtual async Task<byte[]> ExportLogsAsync(ExportLogsRequestDto request)
    {
        Check.NotNull(request, nameof(request));
        
        request = _dashboardHelper.ValidateExportRequest(request);

        Logger.LogInformation("Exporting logs in {Format} format with {MaxRecords} max records", 
            request.Format, request.MaxRecords);

        try
        {
            var searchRequest = new LogSearchRequestDto
            {
                FromDate = request.FromDate,
                ToDate = request.ToDate,
                LogLevels = request.LogLevels,
                Applications = request.Applications,
                SearchText = request.SearchText,
                UserId = request.UserId,
                Category = request.Category,
                Page = 1,
                PageSize = request.MaxRecords
            };

            var searchResult = await SearchLogsAsync(searchRequest);

            return request.Format.ToLower() switch
            {
                LogAnalyticsDashboardConstants.ExportFormats.Json => await ExportLogsAsJsonAsync(searchResult.Items),
                LogAnalyticsDashboardConstants.ExportFormats.Csv => await ExportLogsAsCsvAsync(searchResult.Items),
                _ => await ExportLogsAsCsvAsync(searchResult.Items)
            };
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error exporting logs in {Format} format", request.Format);
            throw new UserFriendlyException($"Failed to export logs in {request.Format} format. Please try again.");
        }
    }

    [Authorize(ERPPlatformPermissions.LogAnalytics.ViewLogs)]
    public virtual async Task<PaginatedResponse<RecentLogEntryDto>> GetRecentLogsAsync(RecentLogsRequestDto request)
    {
        Check.NotNull(request, nameof(request));
        
        request.ValidateAndSetDefaults();

        try
        {
            var (skip, take) = _dashboardHelper.ValidatePagination(request.Skip, request.Take);

            var auditLogs = await _auditLogRepository.GetListAsync(includeDetails: true);

            var allLogEntries = auditLogs
                .SelectMany(auditLog => auditLog.Actions?.Select(action => new RecentLogEntryDto
                {
                    Timestamp = auditLog.ExecutionTime,
                    Level = _dashboardHelper.GetLogLevelFromAuditLog(!string.IsNullOrEmpty(auditLog.Exceptions), auditLog.HttpStatusCode),
                    Application = _dashboardHelper.GetApplicationNameFromService(action.ServiceName),
                    Message = $"{action.ServiceName}.{action.MethodName}",
                    UserId = auditLog.UserId?.ToString(),
                    Exception = auditLog.Exceptions,
                    HasException = !string.IsNullOrEmpty(auditLog.Exceptions),
                    HttpStatusCode = auditLog.HttpStatusCode,
                    ExecutionDuration = auditLog.ExecutionDuration,
                    ServiceName = action.ServiceName,
                    MethodName = action.MethodName,
                    Properties = new Dictionary<string, object>
                    {
                        ["Duration"] = auditLog.ExecutionDuration,
                        ["HttpStatusCode"] = auditLog.HttpStatusCode ?? 0
                    }
                }) ?? new List<RecentLogEntryDto>())
                .OrderByDescending(x => x.Timestamp);

            // Apply filters
            var filteredLogs = ApplyRecentLogsFilters(allLogEntries.AsQueryable(), request);

            var totalCount = filteredLogs.Count();
            var paginatedItems = filteredLogs
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
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error getting recent logs");
            throw new UserFriendlyException("Failed to get recent logs. Please try again.");
        }
    }

    #endregion

    #region Audit Log Operations

    [Authorize(ERPPlatformPermissions.AuditLogs.View)]
    public virtual async Task<AuditLogStatisticsDto> GetAuditLogStatisticsAsync(AuditLogSearchRequestDto request)
    {
        Check.NotNull(request, nameof(request));

        var (fromDate, toDate) = _dashboardHelper.ValidateDateRange(request.FromDate, request.ToDate);

        try
        {
            var allAuditLogs = await _auditLogRepository.GetListAsync(includeDetails: true);
            var filteredLogs = allAuditLogs.Where(x => x.ExecutionTime >= fromDate && x.ExecutionTime <= toDate).ToList();
            var todayLogs = await _auditLogRepository.CountAsync(x => x.ExecutionTime.Date == DateTime.Today);

            var failedOperations = filteredLogs.Count(x => !string.IsNullOrEmpty(x.Exceptions));
            var successfulOperations = filteredLogs.Count - failedOperations;

            var avgExecutionDuration = filteredLogs.Any() 
                ? filteredLogs.Average(x => (double)x.ExecutionDuration) 
                : 0;

            var uniqueUsers = filteredLogs
                .Where(x => x.UserId != null)
                .Select(x => x.UserId)
                .Distinct()
                .Count();

            var uniqueServices = filteredLogs
                .SelectMany(x => x.Actions ?? new List<AuditLogAction>())
                .Select(x => x.ServiceName)
                .Where(x => !string.IsNullOrEmpty(x))
                .Distinct()
                .Count();

            return new AuditLogStatisticsDto
            {
                TotalAuditLogs = allAuditLogs.Count,
                TodayAuditLogs = todayLogs,
                SuccessfulOperations = successfulOperations,
                FailedOperations = failedOperations,
                AvgExecutionDuration = Math.Round(avgExecutionDuration, 2),
                UniqueUsers = uniqueUsers,
                UniqueServices = uniqueServices
            };
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error getting audit log statistics");
            throw new UserFriendlyException("Failed to get audit log statistics. Please try again.");
        }
    }

    [Authorize(ERPPlatformPermissions.AuditLogs.View)]
    public virtual async Task<AuditLogSearchResponseDto> SearchAuditLogsAsync(AuditLogSearchRequestDto request)
    {
        Check.NotNull(request, nameof(request));
        
        request.ValidateAndSetDefaults();

        try
        {
            var auditLogs = await _auditLogRepository.GetListAsync(includeDetails: true);

            var filteredLogs = ApplyAuditLogSearchFilters(auditLogs.AsQueryable(), request);

            var totalCount = filteredLogs.Count();
            var pagedLogs = filteredLogs
                .OrderByDescending(x => x.ExecutionTime)
                .Skip((request.Page - 1) * request.PageSize)
                .Take(request.PageSize)
                .Select(x => MapToRecentAuditLogDto(x))
                .ToList();

            return new AuditLogSearchResponseDto(totalCount, pagedLogs, request.Page, request.PageSize);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error searching audit logs");
            throw new UserFriendlyException("Failed to search audit logs. Please try again.");
        }
    }

    [Authorize(ERPPlatformPermissions.AuditLogs.View)]
    public virtual async Task<PaginatedResponse<RecentAuditLogDto>> GetRecentAuditLogsAsync(RecentAuditLogsRequestDto request)
    {
        Check.NotNull(request, nameof(request));
        
        request.ValidateAndSetDefaults();

        try
        {
            var (skip, take) = _dashboardHelper.ValidatePagination(request.Skip, request.Take);

            var auditLogs = await _auditLogRepository.GetListAsync(includeDetails: true);

            var filteredLogs = auditLogs.AsQueryable();

            if (request.FromDate.HasValue)
                filteredLogs = filteredLogs.Where(x => x.ExecutionTime >= request.FromDate.Value);

            if (request.ToDate.HasValue)
                filteredLogs = filteredLogs.Where(x => x.ExecutionTime <= request.ToDate.Value);

            if (!string.IsNullOrWhiteSpace(request.UserId))
                filteredLogs = filteredLogs.Where(x => x.UserId != null && x.UserId.ToString() == request.UserId);

            if (request.HasException.HasValue)
                filteredLogs = filteredLogs.Where(x => request.HasException.Value ? !string.IsNullOrEmpty(x.Exceptions) : string.IsNullOrEmpty(x.Exceptions));

            var totalCount = filteredLogs.Count();
            var result = filteredLogs
                .OrderByDescending(x => x.ExecutionTime)
                .Skip(skip)
                .Take(take)
                .Select(x => MapToRecentAuditLogDto(x))
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
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error getting recent audit logs");
            throw new UserFriendlyException("Failed to get recent audit logs. Please try again.");
        }
    }

    [Authorize(ERPPlatformPermissions.AuditLogs.Export)]
    public virtual async Task<byte[]> ExportAuditLogsAsync(ExportAuditLogsRequestDto request)
    {
        Check.NotNull(request, nameof(request));
        
        request.ValidateAndSetDefaults();

        try
        {
            var searchRequest = new AuditLogSearchRequestDto
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
                PageSize = request.MaxRecords
            };

            var searchResult = await SearchAuditLogsAsync(searchRequest);

            return request.Format.ToLower() switch
            {
                LogAnalyticsDashboardConstants.ExportFormats.Json => await ExportAuditLogsAsJsonAsync(searchResult.Items),
                LogAnalyticsDashboardConstants.ExportFormats.Csv => await ExportAuditLogsAsCsvAsync(searchResult.Items),
                _ => await ExportAuditLogsAsCsvAsync(searchResult.Items)
            };
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error exporting audit logs");
            throw new UserFriendlyException("Failed to export audit logs. Please try again.");
        }
    }

    #endregion

    #region Analytics and Insights

    [Authorize(ERPPlatformPermissions.LogAnalytics.ViewLogs)]
    public virtual async Task<PaginatedResponse<TopUserActivityDto>> GetTopUserActivitiesAsync(TopUserActivitiesRequestDto request)
    {
        Check.NotNull(request, nameof(request));
        
        request.ValidateAndSetDefaults();

        try
        {
            var (fromDate, toDate) = _dashboardHelper.ValidateDateRange(request.FromDate, request.ToDate);
            
            // Convert DateTime to unspecified kind to avoid PostgreSQL issues
            var fromDateUnspecified = DateTime.SpecifyKind(fromDate, DateTimeKind.Unspecified);
            var toDateUnspecified = DateTime.SpecifyKind(toDate, DateTimeKind.Unspecified);

            var auditLogs = await _auditLogRepository.GetListAsync(
                x => x.UserId != null && x.ExecutionTime >= fromDateUnspecified && x.ExecutionTime <= toDateUnspecified);

            var userActivities = auditLogs
                .GroupBy(x => new { x.UserId, x.UserName })
                .Select(g => new TopUserActivityDto
                {
                    UserId = g.Key.UserId!.ToString()!,
                    UserName = g.Key.UserName,
                    ActivityCount = g.Count(),
                    SuccessfulOperations = g.Count(x => string.IsNullOrEmpty(x.Exceptions)),
                    FailedOperations = g.Count(x => !string.IsNullOrEmpty(x.Exceptions)),
                    LastActivity = g.Max(x => x.ExecutionTime),
                    AvgExecutionTime = g.Any() ? Math.Round(g.Average(x => (double)x.ExecutionDuration), 2) : 0
                })
                .OrderByDescending(x => x.ActivityCount)
                .Take(request.Count)
                .ToList();

            return new PaginatedResponse<TopUserActivityDto>
            {
                Items = userActivities,
                TotalCount = userActivities.Count,
                Skip = 0,
                Take = request.Count,
                HasMore = false
            };
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error getting top user activities");
            throw new UserFriendlyException("Failed to get top user activities. Please try again.");
        }
    }

    [Authorize(ERPPlatformPermissions.LogAnalytics.ViewLogs)]
    public virtual async Task<PaginatedResponse<AuditLogMethodCountDto>> GetTopAuditMethodsAsync(TopAuditMethodsRequestDto request)
    {
        Check.NotNull(request, nameof(request));
        
        request.ValidateAndSetDefaults();

        try
        {
            var (fromDate, toDate) = _dashboardHelper.ValidateDateRange(request.FromDate, request.ToDate);
            
            // Convert DateTime to unspecified kind to avoid PostgreSQL issues
            var fromDateUnspecified = DateTime.SpecifyKind(fromDate, DateTimeKind.Unspecified);
            var toDateUnspecified = DateTime.SpecifyKind(toDate, DateTimeKind.Unspecified);

            var auditLogs = await _auditLogRepository.GetListAsync(
                x => x.ExecutionTime >= fromDateUnspecified && x.ExecutionTime <= toDateUnspecified,
                includeDetails: true);

            var methodCounts = auditLogs
                .Where(x => x.Actions != null && x.Actions.Any())
                .SelectMany(x => x.Actions!.Select(a => new { AuditLog = x, Action = a }))
                .GroupBy(x => new { x.Action.ServiceName, x.Action.MethodName })
                .Select(g => new AuditLogMethodCountDto
                {
                    ServiceName = g.Key.ServiceName ?? "Unknown",
                    MethodName = g.Key.MethodName ?? "Unknown",
                    CallCount = g.Count(),
                    FailureCount = g.Count(x => !string.IsNullOrEmpty(x.AuditLog.Exceptions)),
                    AvgDuration = g.Any() ? Math.Round(g.Average(x => (double)x.AuditLog.ExecutionDuration), 2) : 0,
                    MaxDuration = g.Any() ? g.Max(x => (double)x.AuditLog.ExecutionDuration) : 0,
                    LastCalled = g.Max(x => x.AuditLog.ExecutionTime)
                })
                .OrderByDescending(x => x.CallCount)
                .Take(request.Count)
                .ToList();

            return new PaginatedResponse<AuditLogMethodCountDto>
            {
                Items = methodCounts,
                TotalCount = methodCounts.Count,
                Skip = 0,
                Take = request.Count,
                HasMore = false
            };
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error getting top audit methods");
            throw new UserFriendlyException("Failed to get top audit methods. Please try again.");
        }
    }

    #endregion

    #region Private Helper Methods

    protected virtual IQueryable<RecentLogEntryDto> ApplyLogSearchFilters(IQueryable<RecentLogEntryDto> logs, LogSearchRequestDto request)
    {
        if (request.FromDate.HasValue)
            logs = logs.Where(l => l.Timestamp >= request.FromDate.Value);

        if (request.ToDate.HasValue)
            logs = logs.Where(l => l.Timestamp <= request.ToDate.Value);

        if (request.LogLevels.Any())
            logs = logs.Where(l => request.LogLevels.Contains(l.Level));

        if (request.Applications.Any())
            logs = logs.Where(l => request.Applications.Contains(l.Application));

        if (!string.IsNullOrWhiteSpace(request.SearchText))
        {
            var searchText = request.SearchText.ToLower();
            logs = logs.Where(l => l.Message.ToLower().Contains(searchText));
        }

        if (!string.IsNullOrWhiteSpace(request.UserId))
            logs = logs.Where(l => l.UserId == request.UserId);

        return logs;
    }

    protected virtual IQueryable<RecentLogEntryDto> ApplyRecentLogsFilters(IQueryable<RecentLogEntryDto> logs, RecentLogsRequestDto request)
    {
        if (request.FromDate.HasValue)
            logs = logs.Where(l => l.Timestamp >= request.FromDate.Value);

        if (request.ToDate.HasValue)
            logs = logs.Where(l => l.Timestamp <= request.ToDate.Value);

        if (request.LogLevels.Any())
            logs = logs.Where(l => request.LogLevels.Contains(l.Level));

        if (request.Applications.Any())
            logs = logs.Where(l => request.Applications.Contains(l.Application));

        return logs;
    }

    protected virtual IQueryable<AuditLog> ApplyAuditLogSearchFilters(IQueryable<AuditLog> logs, AuditLogSearchRequestDto request)
    {
        if (request.FromDate.HasValue)
        {
            var fromDateUnspecified = DateTime.SpecifyKind(request.FromDate.Value, DateTimeKind.Unspecified);
            logs = logs.Where(x => x.ExecutionTime >= fromDateUnspecified);
        }

        if (request.ToDate.HasValue)
        {
            var toDateUnspecified = DateTime.SpecifyKind(request.ToDate.Value, DateTimeKind.Unspecified);
            logs = logs.Where(x => x.ExecutionTime <= toDateUnspecified);
        }

        if (!string.IsNullOrWhiteSpace(request.UserId))
            logs = logs.Where(x => x.UserId != null && x.UserId.ToString() == request.UserId);

        if (!string.IsNullOrWhiteSpace(request.ServiceName))
            logs = logs.Where(x => x.Actions != null && x.Actions.Any(a => a.ServiceName != null && a.ServiceName.Contains(request.ServiceName)));

        if (!string.IsNullOrWhiteSpace(request.MethodName))
            logs = logs.Where(x => x.Actions != null && x.Actions.Any(a => a.MethodName != null && a.MethodName.Contains(request.MethodName)));

        if (!string.IsNullOrWhiteSpace(request.HttpMethod))
            logs = logs.Where(x => x.HttpMethod == request.HttpMethod);

        if (request.MinDuration.HasValue)
            logs = logs.Where(x => x.ExecutionDuration >= request.MinDuration.Value);

        if (request.MaxDuration.HasValue)
            logs = logs.Where(x => x.ExecutionDuration <= request.MaxDuration.Value);

        if (request.HasException.HasValue)
            logs = logs.Where(x => request.HasException.Value ? !string.IsNullOrEmpty(x.Exceptions) : string.IsNullOrEmpty(x.Exceptions));

        if (!string.IsNullOrWhiteSpace(request.ClientIp))
            logs = logs.Where(x => x.ClientIpAddress != null && x.ClientIpAddress.Contains(request.ClientIp));

        return logs;
    }

    protected virtual RecentAuditLogDto MapToRecentAuditLogDto(AuditLog auditLog)
    {
        return new RecentAuditLogDto
        {
            ExecutionTime = auditLog.ExecutionTime,
            UserId = auditLog.UserId?.ToString(),
            UserName = auditLog.UserName,
            ServiceName = auditLog.Actions?.FirstOrDefault()?.ServiceName ?? "Unknown",
            MethodName = auditLog.Actions?.FirstOrDefault()?.MethodName ?? "Unknown",
            ExecutionDuration = auditLog.ExecutionDuration,
            ClientIpAddress = auditLog.ClientIpAddress,
            BrowserInfo = auditLog.BrowserInfo,
            HttpMethod = auditLog.HttpMethod,
            Url = auditLog.Url,
            HttpStatusCode = auditLog.HttpStatusCode,
            HasException = !string.IsNullOrEmpty(auditLog.Exceptions),
            Exception = auditLog.Exceptions
        };
    }

    protected virtual async Task<LogStatisticsDto> GetLogStatisticsAsync(DateTime fromDate, DateTime toDate)
    {
        var auditStats = await GetAuditLogStatisticsAsync(new AuditLogSearchRequestDto { FromDate = fromDate, ToDate = toDate });

        // Convert UTC DateTime to unspecified kind to avoid PostgreSQL issues
        var fromDateUnspecified = DateTime.SpecifyKind(fromDate, DateTimeKind.Unspecified);
        var toDateUnspecified = DateTime.SpecifyKind(toDate, DateTimeKind.Unspecified);
        
        var auditLogsForStats = await _auditLogRepository.GetListAsync(
            x => x.ExecutionTime >= fromDateUnspecified && x.ExecutionTime <= toDateUnspecified);

        var errorCount = auditLogsForStats.Count(x => x.HttpStatusCode.HasValue &&
            x.HttpStatusCode >= 400 && x.HttpStatusCode < 600);

        return new LogStatisticsDto
        {
            TotalLogs = auditStats.TotalAuditLogs,
            TodayLogs = auditStats.TodayAuditLogs,
            ErrorCount = errorCount,
            WarningCount = Math.Max(0, auditStats.TotalAuditLogs - auditStats.FailedOperations - auditStats.SuccessfulOperations),
            InfoCount = auditStats.SuccessfulOperations,
            AvgResponseTime = auditStats.AvgExecutionDuration,
            SlowOperations = auditLogsForStats.Count(x => _dashboardHelper.IsSlowOperation(x.ExecutionDuration)),
            SecurityEvents = auditStats.UniqueUsers,
            TotalAuditLogs = auditStats.TotalAuditLogs,
            TodayAuditLogs = auditStats.TodayAuditLogs,
            FailedOperations = auditStats.FailedOperations
        };
    }

    protected virtual async Task<List<LogLevelCountDto>> GetLogLevelCountsAsync(DateTime fromDate, DateTime toDate)
    {
        var auditStats = await GetAuditLogStatisticsAsync(new AuditLogSearchRequestDto { FromDate = fromDate, ToDate = toDate });
        var total = auditStats.TotalAuditLogs;

        if (total == 0)
        {
            return new List<LogLevelCountDto>
            {
                new() { Level = LogAnalyticsDashboardConstants.LogLevels.Information, Count = 0, Percentage = 0 },
                new() { Level = LogAnalyticsDashboardConstants.LogLevels.Warning, Count = 0, Percentage = 0 },
                new() { Level = LogAnalyticsDashboardConstants.LogLevels.Error, Count = 0, Percentage = 0 }
            };
        }

        var errorCount = auditStats.FailedOperations;
        var infoCount = auditStats.SuccessfulOperations;
        var warningCount = Math.Max(0, total - errorCount - infoCount);

        return new List<LogLevelCountDto>
        {
            new() { 
                Level = LogAnalyticsDashboardConstants.LogLevels.Information, 
                Count = infoCount, 
                Percentage = _dashboardHelper.CalculatePercentage(infoCount, total) 
            },
            new() { 
                Level = LogAnalyticsDashboardConstants.LogLevels.Warning, 
                Count = warningCount, 
                Percentage = _dashboardHelper.CalculatePercentage(warningCount, total) 
            },
            new() { 
                Level = LogAnalyticsDashboardConstants.LogLevels.Error, 
                Count = errorCount, 
                Percentage = _dashboardHelper.CalculatePercentage(errorCount, total) 
            }
        };
    }

    protected virtual async Task<List<ApplicationLogCountDto>> GetApplicationCountsAsync(DateTime fromDate, DateTime toDate)
    {
        var auditStats = await GetAuditLogStatisticsAsync(new AuditLogSearchRequestDto { FromDate = fromDate, ToDate = toDate });

        if (auditStats.TotalAuditLogs == 0)
        {
            return new List<ApplicationLogCountDto>();
        }

        return new List<ApplicationLogCountDto>
        {
            new() { 
                Application = LogAnalyticsDashboardConstants.Applications.Application, 
                Count = auditStats.TotalAuditLogs, 
                ErrorCount = auditStats.FailedOperations 
            }
        };
    }

    protected virtual async Task<List<HourlyLogCountDto>> GetHourlyTrendsAsync(DateTime fromDate, DateTime toDate)
    {
        var last24Hours = DateTime.UtcNow.AddHours(-24);
        // Convert DateTime to unspecified kind to avoid PostgreSQL issues
        var last24HoursUnspecified = DateTime.SpecifyKind(last24Hours, DateTimeKind.Unspecified);
        var auditLogs = await _auditLogRepository.GetListAsync(x => x.ExecutionTime >= last24HoursUnspecified);

        if (!auditLogs.Any())
        {
            return new List<HourlyLogCountDto>();
        }

        return _dashboardHelper.GroupByHour(
            auditLogs,
            x => x.ExecutionTime,
            x => !string.IsNullOrEmpty(x.Exceptions),
            24);
    }

    protected virtual async Task<List<RecentLogEntryDto>> GetRecentLogsForDashboardAsync(int count)
    {
        var request = new RecentLogsRequestDto { Take = count };
        var result = await GetRecentLogsAsync(request);
        return result.Items;
    }

    protected virtual async Task<List<TopErrorDto>> GetTopErrorsAsync(int count)
    {
        var auditLogs = await _auditLogRepository.GetListAsync(
            x => !string.IsNullOrEmpty(x.Exceptions),
            includeDetails: true);

        if (!auditLogs.Any())
        {
            return new List<TopErrorDto>();
        }

        var errorGroups = auditLogs
            .GroupBy(x => _dashboardHelper.ExtractExceptionMessage(x.Exceptions))
            .Select(g => new TopErrorDto
            {
                ErrorMessage = g.Key ?? "Unknown Error",
                ExceptionType = _dashboardHelper.ExtractExceptionType(g.First().Exceptions),
                Count = g.Count(),
                LastOccurrence = g.Max(x => x.ExecutionTime),
                AffectedApplications = new List<string> { LogAnalyticsDashboardConstants.Applications.Application }
            })
            .OrderByDescending(x => x.Count)
            .Take(count)
            .ToList();

        return errorGroups;
    }

    protected virtual async Task<List<PerformanceMetricDto>> GetPerformanceMetricsAsync(int count)
    {
        var methodCountsRequest = new TopAuditMethodsRequestDto { Count = count };
        var methodCounts = await GetTopAuditMethodsAsync(methodCountsRequest);

        return methodCounts.Items.Select(method => new PerformanceMetricDto
        {
            Operation = method.FullMethodName,
            AvgDuration = method.AvgDuration,
            MaxDuration = method.MaxDuration,
            ExecutionCount = method.CallCount,
            SlowExecutions = method.FailureCount,
            LastExecution = method.LastCalled
        }).ToList();
    }

    protected virtual async Task<List<RecentAuditLogDto>> GetRecentAuditLogsForDashboardAsync(int count)
    {
        var request = new RecentAuditLogsRequestDto { Take = count };
        var result = await GetRecentAuditLogsAsync(request);
        return result.Items;
    }

    protected virtual async Task<List<TopUserActivityDto>> GetTopUserActivitiesForDashboardAsync(int count)
    {
        var request = new TopUserActivitiesRequestDto { Count = count };
        var result = await GetTopUserActivitiesAsync(request);
        return result.Items;
    }

    protected virtual async Task<List<AuditLogMethodCountDto>> GetTopAuditMethodsForDashboardAsync(int count)
    {
        var request = new TopAuditMethodsRequestDto { Count = count };
        var result = await GetTopAuditMethodsAsync(request);
        return result.Items;
    }

    protected virtual async Task<byte[]> ExportLogsAsJsonAsync(IReadOnlyList<RecentLogEntryDto> logs)
    {
        return await Task.FromResult(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(logs, new JsonSerializerOptions
        {
            WriteIndented = true
        })));
    }

    protected virtual async Task<byte[]> ExportLogsAsCsvAsync(IReadOnlyList<RecentLogEntryDto> logs)
    {
        var csv = new StringBuilder();
        csv.AppendLine("Timestamp,Level,Application,Message,UserId,Exception");

        foreach (var log in logs)
        {
            csv.AppendLine($"\"{_dashboardHelper.FormatDateForExport(log.Timestamp)}\"," +
                          $"\"{log.Level}\"," +
                          $"\"{log.Application}\"," +
                          $"\"{_dashboardHelper.EscapeCsvValue(log.Message)}\"," +
                          $"\"{log.UserId}\"," +
                          $"\"{_dashboardHelper.EscapeCsvValue(log.Exception)}\"");
        }

        return await Task.FromResult(Encoding.UTF8.GetBytes(csv.ToString()));
    }

    protected virtual async Task<byte[]> ExportAuditLogsAsJsonAsync(IReadOnlyList<RecentAuditLogDto> logs)
    {
        return await Task.FromResult(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(logs, new JsonSerializerOptions
        {
            WriteIndented = true
        })));
    }

    protected virtual async Task<byte[]> ExportAuditLogsAsCsvAsync(IReadOnlyList<RecentAuditLogDto> logs)
    {
        var csv = new StringBuilder();
        csv.AppendLine("ExecutionTime,UserId,UserName,ServiceName,MethodName,ExecutionDuration,ClientIpAddress,HttpMethod,HttpStatusCode,HasException");

        foreach (var log in logs)
        {
            csv.AppendLine($"\"{_dashboardHelper.FormatDateForExport(log.ExecutionTime)}\"," +
                          $"\"{log.UserId}\"," +
                          $"\"{_dashboardHelper.EscapeCsvValue(log.UserName)}\"," +
                          $"\"{log.ServiceName}\"," +
                          $"\"{log.MethodName}\"," +
                          $"\"{log.ExecutionDuration}\"," +
                          $"\"{log.ClientIpAddress}\"," +
                          $"\"{log.HttpMethod}\"," +
                          $"\"{log.HttpStatusCode}\"," +
                          $"\"{log.HasException}\"");
        }

        return await Task.FromResult(Encoding.UTF8.GetBytes(csv.ToString()));
    }

    #endregion
}