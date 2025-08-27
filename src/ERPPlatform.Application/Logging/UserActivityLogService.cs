using Microsoft.Extensions.Logging;
using System;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Application.Services;
using ERPPlatform.Logging.Helpers;

namespace ERPPlatform.Logging;

/// <summary>
/// Service for logging user activities following ABP standards
/// </summary>
public class UserActivityLogService : ApplicationService, IUserActivityLogService
{
    private readonly LoggingContextProvider _contextProvider;
    private readonly StructuredLoggerHelper _loggerHelper;

    public UserActivityLogService(
        LoggingContextProvider contextProvider,
        StructuredLoggerHelper loggerHelper)
    {
        _contextProvider = contextProvider;
        _loggerHelper = loggerHelper;
    }

    public async Task LogActivityAsync(UserActivityLogDto logData)
    {
        Check.NotNull(logData, nameof(logData));

        // Validate required fields
        if (!_loggerHelper.ValidateLogEntry(logData, "Action", "Details"))
        {
            Logger.LogWarning("Invalid user activity log data provided");
            return;
        }

        try
        {
            // Enrich with context
            _contextProvider.EnrichWithContext(logData);

            // Create structured log properties
            var properties = _loggerHelper.CreateLogProperties(logData);
            
            // Add user activity-specific properties
            properties["Action"] = logData.Action;
            properties["Details"] = logData.Details;
            properties[LoggingConstants.PropertyNames.Category] = LoggingConstants.Categories.UserActivity;
            
            if (!string.IsNullOrEmpty(logData.Module))
            {
                properties["Module"] = logData.Module;
            }

            if (!string.IsNullOrEmpty(logData.Resource))
            {
                properties["Resource"] = logData.Resource;
            }

            // Log the activity
            using var scope = _loggerHelper.CreateLogScope(Logger, properties);
            
            Logger.LogInformation(
                "User {UserId} performed action {Action} in module {Module}: {Details}",
                logData.UserId,
                logData.Action,
                logData.Module ?? "Unknown",
                logData.Details);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error occurred while logging user activity");
        }

        await Task.CompletedTask;
    }

    public async Task LogActionAsync(string action, string details, string? module = null, string? resource = null)
    {
        var logData = new UserActivityLogDto
        {
            Action = Check.NotNullOrWhiteSpace(action, nameof(action)),
            Details = Check.NotNullOrWhiteSpace(details, nameof(details)),
            Module = module,
            Resource = resource
        };

        await LogActivityAsync(logData);
    }

    public async Task LogPageAccessAsync(string pageName, string? module = null)
    {
        await LogActionAsync(
            "PageAccess",
            $"Accessed page: {pageName}",
            module,
            pageName);
    }

    public async Task LogDataExportAsync(string exportType, int recordCount, string format)
    {
        var details = $"Exported {recordCount} {exportType} records in {format} format";
        
        var logData = new UserActivityLogDto
        {
            Action = "DataExport",
            Details = details,
            Module = "DataExport",
            Resource = exportType
        };

        // Add export-specific properties
        logData.AdditionalProperties["ExportType"] = exportType;
        logData.AdditionalProperties["RecordCount"] = recordCount;
        logData.AdditionalProperties["Format"] = format;

        await LogActivityAsync(logData);
    }
}