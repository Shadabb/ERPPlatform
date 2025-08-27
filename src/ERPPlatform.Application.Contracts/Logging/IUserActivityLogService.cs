using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace ERPPlatform.Logging;

/// <summary>
/// Service interface for logging user activities following ABP standards
/// </summary>
public interface IUserActivityLogService : IApplicationService
{
    /// <summary>
    /// Logs a user activity with structured data
    /// </summary>
    /// <param name="logData">User activity log data</param>
    Task LogActivityAsync(UserActivityLogDto logData);

    /// <summary>
    /// Logs a user action with minimal required information
    /// </summary>
    /// <param name="action">The action performed by the user</param>
    /// <param name="details">Details about the action</param>
    /// <param name="module">Optional module/area where action occurred</param>
    /// <param name="resource">Optional specific resource affected</param>
    Task LogActionAsync(string action, string details, string? module = null, string? resource = null);

    /// <summary>
    /// Logs user navigation/page access
    /// </summary>
    /// <param name="pageName">Name of the page accessed</param>
    /// <param name="module">Module/area of the page</param>
    Task LogPageAccessAsync(string pageName, string? module = null);

    /// <summary>
    /// Logs user data export activities
    /// </summary>
    /// <param name="exportType">Type of data exported</param>
    /// <param name="recordCount">Number of records exported</param>
    /// <param name="format">Export format (CSV, JSON, etc.)</param>
    Task LogDataExportAsync(string exportType, int recordCount, string format);
}