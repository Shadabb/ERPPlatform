using Microsoft.Extensions.Logging;
using System;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Application.Services;
using ERPPlatform.Logging.Helpers;

namespace ERPPlatform.Logging;

/// <summary>
/// Service for logging business operations following ABP standards
/// </summary>
public class BusinessOperationLogAppService : ApplicationService, IBusinessOperationLogAppService
{
    private readonly LoggingContextProvider _contextProvider;
    private readonly StructuredLoggerHelper _loggerHelper;

    public BusinessOperationLogAppService(
        LoggingContextProvider contextProvider,
        StructuredLoggerHelper loggerHelper)
    {
        _contextProvider = contextProvider;
        _loggerHelper = loggerHelper;
    }

    public async Task LogOperationAsync(BusinessOperationLogDto logData)
    {
        Check.NotNull(logData, nameof(logData));
        
        // Validate required fields
        if (!_loggerHelper.ValidateLogEntry(logData, "Operation", "EntityType", "EntityId"))
        {
            Logger.LogWarning("Invalid business operation log data provided");
            return;
        }

        try
        {
            // Enrich with context
            _contextProvider.EnrichWithContext(logData);

            // Create structured log properties
            var properties = _loggerHelper.CreateLogProperties(logData);
            
            // Add business-specific properties
            properties[LoggingConstants.PropertyNames.Operation] = logData.Operation;
            properties[LoggingConstants.PropertyNames.EntityType] = logData.EntityType;
            properties[LoggingConstants.PropertyNames.EntityId] = logData.EntityId;
            properties[LoggingConstants.PropertyNames.Category] = LoggingConstants.Categories.BusinessOperation;
            properties["IsSuccessful"] = logData.IsSuccessful;
            
            if (!string.IsNullOrEmpty(logData.Description))
            {
                properties["Description"] = logData.Description;
            }

            if (!string.IsNullOrEmpty(logData.ErrorMessage))
            {
                properties["ErrorMessage"] = logData.ErrorMessage;
            }

            // Log based on success/failure
            using var scope = _loggerHelper.CreateLogScope(Logger, properties);
            
            var message = logData.IsSuccessful
                ? "Business operation {Operation} completed successfully for {EntityType} {EntityId} by user {UserId}"
                : "Business operation {Operation} failed for {EntityType} {EntityId} by user {UserId}: {ErrorMessage}";

            var level = logData.IsSuccessful ? LogLevel.Information : LogLevel.Warning;
            
            Logger.Log(level, message, 
                logData.Operation, 
                logData.EntityType, 
                logData.EntityId, 
                logData.UserId,
                logData.ErrorMessage);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error occurred while logging business operation");
        }

        await Task.CompletedTask;
    }

    public async Task LogSuccessAsync(string operation, string entityType, string entityId, string? description = null)
    {
        var logData = new BusinessOperationLogDto
        {
            Operation = Check.NotNullOrWhiteSpace(operation, nameof(operation)),
            EntityType = Check.NotNullOrWhiteSpace(entityType, nameof(entityType)),
            EntityId = Check.NotNullOrWhiteSpace(entityId, nameof(entityId)),
            Description = description,
            IsSuccessful = true
        };

        await LogOperationAsync(logData);
    }

    public async Task LogFailureAsync(string operation, string entityType, string entityId, string errorMessage, string? description = null)
    {
        var logData = new BusinessOperationLogDto
        {
            Operation = Check.NotNullOrWhiteSpace(operation, nameof(operation)),
            EntityType = Check.NotNullOrWhiteSpace(entityType, nameof(entityType)),
            EntityId = Check.NotNullOrWhiteSpace(entityId, nameof(entityId)),
            ErrorMessage = Check.NotNullOrWhiteSpace(errorMessage, nameof(errorMessage)),
            Description = description,
            IsSuccessful = false
        };

        await LogOperationAsync(logData);
    }
}