using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Volo.Abp.DependencyInjection;
using Volo.Abp.EntityFrameworkCore;
using ERPPlatform.EntityFrameworkCore;

namespace ERPPlatform.LogAnalytics;

/// <summary>
/// Repository for accessing Serilog entries from seriloglogs table
/// Follows ABP repository pattern for keyless entities
/// </summary>
public class SerilogEntryRepository : ISerilogEntryRepository, ITransientDependency
{
    private readonly IDbContextProvider<ERPPlatformDbContext> _dbContextProvider;

    public SerilogEntryRepository(IDbContextProvider<ERPPlatformDbContext> dbContextProvider)
    {
        _dbContextProvider = dbContextProvider;
    }

    /// <summary>
    /// Gets queryable access to Serilog entries
    /// </summary>
    public async Task<IQueryable<SerilogEntry>> GetQueryableAsync()
    {
        var dbContext = await _dbContextProvider.GetDbContextAsync();
        return dbContext.SerilogEntries.AsQueryable();
    }

    /// <summary>
    /// Gets recent Serilog entries
    /// </summary>
    public async Task<List<SerilogEntry>> GetRecentAsync(int count = 50, CancellationToken cancellationToken = default)
    {
        var queryable = await GetQueryableAsync();
        return await queryable
            .OrderByDescending(x => x.Timestamp)
            .Take(count)
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Gets error entries from the last specified period
    /// </summary>
    public async Task<List<SerilogEntry>> GetRecentErrorsAsync(int count = 50, int withinHours = 24, CancellationToken cancellationToken = default)
    {
        var cutoffTime = DateTime.SpecifyKind(DateTime.Now.AddHours(-withinHours), DateTimeKind.Unspecified);
        var queryable = await GetQueryableAsync();

        return await queryable
            .Where(x => x.Timestamp >= cutoffTime && x.Level >= 4) // Error or Fatal
            .OrderByDescending(x => x.Timestamp)
            .Take(count)
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Gets total count of all log entries
    /// </summary>
    public async Task<long> GetTotalCountAsync(CancellationToken cancellationToken = default)
    {
        var queryable = await GetQueryableAsync();
        return await queryable.LongCountAsync(cancellationToken);
    }

    /// <summary>
    /// Gets total count of log entries within date range (overload for backward compatibility)
    /// </summary>
    public async Task<int> GetTotalCountAsync(DateTime? fromDate, DateTime? toDate, CancellationToken cancellationToken = default)
    {
        var result = await GetCountByDateRangeAsync(fromDate ?? DateTime.MinValue, toDate ?? DateTime.MaxValue, cancellationToken);
        return (int)result;
    }

    /// <summary>
    /// Gets count of log entries by level within date range
    /// </summary>
    public async Task<Dictionary<string, int>> GetLogLevelCountsAsync(DateTime? fromDate = null, DateTime? toDate = null, CancellationToken cancellationToken = default)
    {
        var queryable = await GetQueryableAsync();

        if (fromDate.HasValue)
        {
            var fromDateUnspecified = DateTime.SpecifyKind(fromDate.Value, DateTimeKind.Unspecified);
            queryable = queryable.Where(x => x.Timestamp >= fromDateUnspecified);
        }

        if (toDate.HasValue)
        {
            var toDateUnspecified = DateTime.SpecifyKind(toDate.Value, DateTimeKind.Unspecified);
            queryable = queryable.Where(x => x.Timestamp <= toDateUnspecified);
        }

        var levelCounts = await queryable
            .Where(x => x.Level.HasValue)
            .GroupBy(x => x.Level!.Value)
            .Select(g => new { Level = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);

        return levelCounts.ToDictionary(
            x => x.Level switch
            {
                0 => "Verbose",
                1 => "Debug",
                2 => "Information", 
                3 => "Warning",
                4 => "Error",
                5 => "Fatal",
                _ => "Unknown"
            },
            x => x.Count);
    }

    /// <summary>
    /// Gets count of log entries within date range
    /// </summary>
    public async Task<long> GetCountByDateRangeAsync(DateTime fromDate, DateTime toDate, CancellationToken cancellationToken = default)
    {
        var fromDateUnspecified = DateTime.SpecifyKind(fromDate, DateTimeKind.Unspecified);
        var toDateUnspecified = DateTime.SpecifyKind(toDate, DateTimeKind.Unspecified);
        
        var queryable = await GetQueryableAsync();
        return await queryable
            .Where(x => x.Timestamp >= fromDateUnspecified && x.Timestamp <= toDateUnspecified)
            .LongCountAsync(cancellationToken);
    }

    #region Legacy Methods (marked as obsolete for gradual migration)

    /// <summary>
    /// Gets all Serilog entries with optional filtering
    /// </summary>
    [Obsolete("Use GetQueryableAsync() with LINQ for better performance")]
    public async Task<List<SerilogEntry>> GetListAsync(
        DateTime? fromDate = null,
        DateTime? toDate = null,
        int? minLevel = null,
        int? maxLevel = null,
        bool hasExceptionOnly = false,
        int skip = 0,
        int take = 100,
        CancellationToken cancellationToken = default)
    {
        var queryable = await GetQueryableAsync();

        if (fromDate.HasValue)
        {
            var fromDateUnspecified = DateTime.SpecifyKind(fromDate.Value, DateTimeKind.Unspecified);
            queryable = queryable.Where(x => x.Timestamp >= fromDateUnspecified);
        }

        if (toDate.HasValue)
        {
            var toDateUnspecified = DateTime.SpecifyKind(toDate.Value, DateTimeKind.Unspecified);
            queryable = queryable.Where(x => x.Timestamp <= toDateUnspecified);
        }

        if (minLevel.HasValue)
            queryable = queryable.Where(x => x.Level >= minLevel.Value);

        if (maxLevel.HasValue)
            queryable = queryable.Where(x => x.Level <= maxLevel.Value);

        if (hasExceptionOnly)
            queryable = queryable.Where(x => !string.IsNullOrEmpty(x.Exception));

        return await queryable
            .OrderByDescending(x => x.Timestamp)
            .Skip(skip)
            .Take(take)
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Gets entries grouped by hour for trend analysis
    /// </summary>
    [Obsolete("Move to application layer for better separation of concerns")]
    public async Task<List<HourlyLogCount>> GetHourlyTrendsAsync(DateTime fromDate, DateTime toDate, CancellationToken cancellationToken = default)
    {
        var fromDateUnspecified = DateTime.SpecifyKind(fromDate, DateTimeKind.Unspecified);
        var toDateUnspecified = DateTime.SpecifyKind(toDate, DateTimeKind.Unspecified);
        
        var queryable = await GetQueryableAsync();

        var hourlyData = await queryable
            .Where(x => x.Timestamp >= fromDateUnspecified && x.Timestamp <= toDateUnspecified)
            .GroupBy(x => new 
            { 
                Year = x.Timestamp!.Value.Year,
                Month = x.Timestamp!.Value.Month,
                Day = x.Timestamp!.Value.Day,
                Hour = x.Timestamp!.Value.Hour
            })
            .Select(g => new HourlyLogCount
            {
                Hour = new DateTime(g.Key.Year, g.Key.Month, g.Key.Day, g.Key.Hour, 0, 0),
                TotalCount = g.Count(),
                ErrorCount = g.Count(x => x.Level >= 4),
                WarningCount = g.Count(x => x.Level == 3),
                InfoCount = g.Count(x => x.Level == 2)
            })
            .OrderBy(x => x.Hour)
            .ToListAsync(cancellationToken);

        return hourlyData;
    }

    #endregion
}

/// <summary>
/// Data transfer object for hourly log counts
/// TODO: Move to Domain.Shared layer as proper DTO
/// </summary>
[Obsolete("Move to Domain.Shared layer")]
public class HourlyLogCount
{
    public DateTime Hour { get; set; }
    public int TotalCount { get; set; }
    public int ErrorCount { get; set; }
    public int WarningCount { get; set; }
    public int InfoCount { get; set; }
}