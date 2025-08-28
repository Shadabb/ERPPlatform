using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace ERPPlatform.Logging;

/// <summary>
/// Service interface for logging performance metrics following ABP standards
/// </summary>
public interface IPerformanceLogAppService : IApplicationService
{
    /// <summary>
    /// Logs performance data with structured information
    /// </summary>
    /// <param name="logData">Performance log data</param>
    Task LogPerformanceAsync(PerformanceLogDto logData);

    /// <summary>
    /// Logs operation performance with duration
    /// </summary>
    /// <param name="operation">Name of the operation</param>
    /// <param name="duration">Time taken to complete</param>
    /// <param name="component">Optional component/service name</param>
    /// <param name="method">Optional method name</param>
    Task LogOperationPerformanceAsync(string operation, TimeSpan duration, string? component = null, string? method = null);

    /// <summary>
    /// Logs database query performance
    /// </summary>
    /// <param name="queryType">Type of query (SELECT, INSERT, etc.)</param>
    /// <param name="duration">Time taken for query</param>
    /// <param name="entityType">Entity type being queried</param>
    /// <param name="recordCount">Number of records affected/returned</param>
    Task LogQueryPerformanceAsync(string queryType, TimeSpan duration, string entityType, int? recordCount = null);

    /// <summary>
    /// Logs API endpoint performance
    /// </summary>
    /// <param name="endpoint">API endpoint path</param>
    /// <param name="httpMethod">HTTP method (GET, POST, etc.)</param>
    /// <param name="duration">Request processing time</param>
    /// <param name="statusCode">HTTP status code</param>
    Task LogApiPerformanceAsync(string endpoint, string httpMethod, TimeSpan duration, int statusCode);
}