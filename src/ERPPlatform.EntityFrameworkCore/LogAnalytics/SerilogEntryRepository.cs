using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Volo.Abp.DependencyInjection;
using Volo.Abp.EntityFrameworkCore;
using ERPPlatform.EntityFrameworkCore;

namespace ERPPlatform.LogAnalytics;

/// <summary>
/// Repository for accessing Serilog entries from seriloglogs table
/// Since SerilogEntry is a keyless entity, we use IQueryable-based access
/// </summary>
public class SerilogEntryRepository : ITransientDependency
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
    /// Gets all Serilog entries with optional filtering
    /// </summary>
    public async Task<List<SerilogEntry>> GetListAsync(
        DateTime? fromDate = null,
        DateTime? toDate = null,
        int? minLevel = null,
        int? maxLevel = null,
        bool hasExceptionOnly = false,
        int skip = 0,
        int take = 100)
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
            .ToListAsync();
    }

    /// <summary>
    /// Gets recent Serilog entries
    /// </summary>
    public async Task<List<SerilogEntry>> GetRecentAsync(int count = 50)
    {
        var queryable = await GetQueryableAsync();
        return await queryable
            .OrderByDescending(x => x.Timestamp)
            .Take(count)
            .ToListAsync();
    }

    /// <summary>
    /// Gets error entries from the last specified period
    /// </summary>
    public async Task<List<SerilogEntry>> GetRecentErrorsAsync(int count = 50, int hoursBack = 24)
    {
        var fromDate = DateTime.SpecifyKind(DateTime.Now.AddHours(-hoursBack), DateTimeKind.Unspecified);
        var queryable = await GetQueryableAsync();

        return await queryable
            .Where(x => x.Timestamp >= fromDate && x.Level >= 4) // Error or Fatal
            .OrderByDescending(x => x.Timestamp)
            .Take(count)
            .ToListAsync();
    }

    /// <summary>
    /// Gets count of entries by log level
    /// </summary>
    public async Task<Dictionary<string, int>> GetLogLevelCountsAsync(DateTime? fromDate = null, DateTime? toDate = null)
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
            .ToListAsync();

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
    /// Gets total count of entries
    /// </summary>
    public async Task<int> GetTotalCountAsync(DateTime? fromDate = null, DateTime? toDate = null)
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

        return await queryable.CountAsync();
    }

    /// <summary>
    /// Gets entries grouped by hour for trend analysis
    /// </summary>
    public async Task<List<HourlyLogCount>> GetHourlyTrendsAsync(DateTime fromDate, DateTime toDate)
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
            .ToListAsync();

        return hourlyData;
    }
}

/// <summary>
/// Data transfer object for hourly log counts
/// </summary>
public class HourlyLogCount
{
    public DateTime Hour { get; set; }
    public int TotalCount { get; set; }
    public int ErrorCount { get; set; }
    public int WarningCount { get; set; }
    public int InfoCount { get; set; }
}