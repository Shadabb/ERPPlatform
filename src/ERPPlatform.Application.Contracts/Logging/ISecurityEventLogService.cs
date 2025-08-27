using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace ERPPlatform.Logging;

/// <summary>
/// Service interface for logging security events following ABP standards
/// </summary>
public interface ISecurityEventLogService : IApplicationService
{
    /// <summary>
    /// Logs a security event with structured data
    /// </summary>
    /// <param name="logData">Security event log data</param>
    Task LogSecurityEventAsync(SecurityEventLogDto logData);

    /// <summary>
    /// Logs a successful authentication event
    /// </summary>
    /// <param name="eventType">Type of authentication event</param>
    /// <param name="description">Description of the event</param>
    /// <param name="resource">Optional resource being accessed</param>
    Task LogAuthenticationSuccessAsync(string eventType, string description, string? resource = null);

    /// <summary>
    /// Logs a failed authentication event
    /// </summary>
    /// <param name="eventType">Type of authentication event</param>
    /// <param name="description">Description of the event</param>
    /// <param name="failureReason">Reason for authentication failure</param>
    /// <param name="resource">Optional resource being accessed</param>
    Task LogAuthenticationFailureAsync(string eventType, string description, string failureReason, string? resource = null);

    /// <summary>
    /// Logs unauthorized access attempts
    /// </summary>
    /// <param name="resource">Resource that was accessed</param>
    /// <param name="action">Action that was attempted</param>
    /// <param name="reason">Reason access was denied</param>
    Task LogUnauthorizedAccessAsync(string resource, string action, string reason);

    /// <summary>
    /// Logs permission denied events
    /// </summary>
    /// <param name="permission">Permission that was required</param>
    /// <param name="resource">Resource being accessed</param>
    /// <param name="action">Action being attempted</param>
    Task LogPermissionDeniedAsync(string permission, string resource, string action);

    /// <summary>
    /// Logs sensitive data access events
    /// </summary>
    /// <param name="dataType">Type of sensitive data accessed</param>
    /// <param name="recordCount">Number of records accessed</param>
    /// <param name="purpose">Purpose of data access</param>
    Task LogSensitiveDataAccessAsync(string dataType, int recordCount, string purpose);
}