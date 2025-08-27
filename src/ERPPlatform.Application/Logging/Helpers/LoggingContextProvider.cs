using System;
using System.Diagnostics;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Users;

namespace ERPPlatform.Logging.Helpers;

/// <summary>
/// Provides contextual information for logging operations
/// </summary>
public class LoggingContextProvider : ITransientDependency
{
    private readonly ICurrentUser _currentUser;
    public LoggingContextProvider(ICurrentUser currentUser)
    {
        _currentUser = currentUser;
    }

    /// <summary>
    /// Enriches log entry with current user context
    /// </summary>
    /// <param name="logEntry">Log entry to enrich</param>
    public void EnrichWithUserContext(LogEntryDto logEntry)
    {
        logEntry.UserId = _currentUser.Id?.ToString();
        logEntry.UserName = _currentUser.UserName;
        logEntry.TenantId = _currentUser.TenantId?.ToString();
    }

    /// <summary>
    /// Enriches log entry with HTTP context information (placeholder - will be handled by Web layer)
    /// </summary>
    /// <param name="logEntry">Log entry to enrich</param>
    public void EnrichWithHttpContext(LogEntryDto logEntry)
    {
        // HTTP context enrichment will be handled by Web layer or middleware
        // This keeps the Application layer decoupled from ASP.NET Core
    }

    /// <summary>
    /// Enriches log entry with tracing information
    /// </summary>
    /// <param name="logEntry">Log entry to enrich</param>
    public void EnrichWithTracing(LogEntryDto logEntry)
    {
        var activity = Activity.Current;
        if (activity != null)
        {
            logEntry.TraceId = activity.TraceId.ToString();
            
            if (string.IsNullOrEmpty(logEntry.RequestId))
            {
                logEntry.RequestId = activity.SpanId.ToString();
            }
        }
    }

    /// <summary>
    /// Enriches log entry with all available context information
    /// </summary>
    /// <param name="logEntry">Log entry to enrich</param>
    public void EnrichWithContext(LogEntryDto logEntry)
    {
        EnrichWithUserContext(logEntry);
        EnrichWithHttpContext(logEntry);
        EnrichWithTracing(logEntry);
        
        // Ensure timestamp is set
        if (logEntry.Timestamp == default)
        {
            logEntry.Timestamp = DateTimeOffset.UtcNow;
        }
    }

}