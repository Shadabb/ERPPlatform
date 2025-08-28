using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ERPPlatform.LogAnalytics;

/// <summary>
/// Repository interface for SerilogEntry following ABP repository pattern
/// Since SerilogEntry is a keyless entity from external table, we use a custom interface
/// </summary>
public interface ISerilogEntryRepository
{
    /// <summary>
    /// Gets the most recent log entries
    /// </summary>
    /// <param name="count">Maximum number of entries to return</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of recent log entries ordered by timestamp descending</returns>
    Task<List<SerilogEntry>> GetRecentAsync(int count = 50, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets recent error log entries within specified hours
    /// </summary>
    /// <param name="count">Maximum number of entries to return</param>
    /// <param name="withinHours">Time range in hours to look back</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of error log entries</returns>
    Task<List<SerilogEntry>> GetRecentErrorsAsync(int count = 50, int withinHours = 24, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets total count of all log entries
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Total number of log entries</returns>
    Task<long> GetTotalCountAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets count of log entries by level within date range
    /// </summary>
    /// <param name="fromDate">Start date (optional)</param>
    /// <param name="toDate">End date (optional)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Dictionary of log level counts</returns>
    Task<Dictionary<string, int>> GetLogLevelCountsAsync(DateTime? fromDate = null, DateTime? toDate = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets count of log entries within date range
    /// </summary>
    /// <param name="fromDate">Start date</param>
    /// <param name="toDate">End date</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Count of log entries in date range</returns>
    Task<long> GetCountByDateRangeAsync(DateTime fromDate, DateTime toDate, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets queryable access to log entries for complex queries
    /// </summary>
    /// <returns>IQueryable for SerilogEntry</returns>
    Task<IQueryable<SerilogEntry>> GetQueryableAsync();
}