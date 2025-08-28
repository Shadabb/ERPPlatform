using Microsoft.Extensions.Logging;
using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Application.Services;

namespace ERPPlatform.Logging;

/// <summary>
/// Modern application service demonstrating the refactored logging implementation
/// following ABP standards and best practices
/// </summary>
public class LoggingExamplesAppService : ApplicationService
{
    private readonly IBusinessOperationLogAppService _businessLogService;
    private readonly IUserActivityLogAppService _userActivityLogService;
    private readonly IPerformanceLogAppService _performanceLogService;
    private readonly ISecurityEventLogAppService _securityLogService;

    public LoggingExamplesAppService(
        IBusinessOperationLogAppService businessLogService,
        IUserActivityLogAppService userActivityLogService,
        IPerformanceLogAppService performanceLogService,
        ISecurityEventLogAppService securityLogService)
    {
        _businessLogService = businessLogService;
        _userActivityLogService = userActivityLogService;
        _performanceLogService = performanceLogService;
        _securityLogService = securityLogService;
    }

    /// <summary>
    /// Demonstrates business operation logging with proper validation
    /// </summary>
    public async Task<string> DemoBusinessOperationAsync(string entityType, string entityId, bool shouldSucceed = true)
    {
        Check.NotNullOrWhiteSpace(entityType, nameof(entityType));
        Check.NotNullOrWhiteSpace(entityId, nameof(entityId));

        var operation = LoggingConstants.BusinessOperations.Update;
        var description = $"Demonstrating {operation} operation on {entityType}";

        try
        {
            // Simulate business logic
            await Task.Delay(100);

            if (shouldSucceed)
            {
                await _businessLogService.LogSuccessAsync(operation, entityType, entityId, description);
                return $"Successfully logged {operation} operation for {entityType} {entityId}";
            }
            else
            {
                await _businessLogService.LogFailureAsync(
                    operation, 
                    entityType, 
                    entityId, 
                    "Simulated business logic failure", 
                    description);
                return $"Logged failed {operation} operation for {entityType} {entityId}";
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error in business operation demo");
            await _businessLogService.LogFailureAsync(operation, entityType, entityId, ex.Message, description);
            throw;
        }
    }

    /// <summary>
    /// Demonstrates user activity logging with context enrichment
    /// </summary>
    public async Task<string> DemoUserActivityAsync(string action, string details)
    {
        Check.NotNullOrWhiteSpace(action, nameof(action));
        Check.NotNullOrWhiteSpace(details, nameof(details));

        await _userActivityLogService.LogActionAsync(
            action, 
            details, 
            module: "LoggingDemo", 
            resource: "ExampleResource");

        return $"Successfully logged user activity: {action}";
    }

    /// <summary>
    /// Demonstrates performance logging with automatic duration tracking
    /// </summary>
    public async Task<string> DemoPerformanceLoggingAsync(int delayMs = 500)
    {
        var stopwatch = Stopwatch.StartNew();
        var operation = "DemoPerformanceOperation";

        try
        {
            // Simulate work
            await Task.Delay(delayMs);
            
            // Some business logic
            var result = $"Processed operation with {delayMs}ms delay";
            
            stopwatch.Stop();
            
            await _performanceLogService.LogOperationPerformanceAsync(
                operation, 
                stopwatch.Elapsed, 
                component: "LoggingDemo", 
                method: nameof(DemoPerformanceLoggingAsync));

            return result;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            Logger.LogError(ex, "Performance demo operation failed after {Duration}ms", stopwatch.ElapsedMilliseconds);
            throw;
        }
    }

    /// <summary>
    /// Demonstrates database query performance logging
    /// </summary>
    public async Task<string> DemoQueryPerformanceAsync(string entityType, int recordCount)
    {
        Check.NotNullOrWhiteSpace(entityType, nameof(entityType));

        var stopwatch = Stopwatch.StartNew();
        
        try
        {
            // Simulate database query
            await Task.Delay(50 + (recordCount / 100)); // Simulate query time based on record count
            
            stopwatch.Stop();
            
            await _performanceLogService.LogQueryPerformanceAsync(
                "SELECT", 
                stopwatch.Elapsed, 
                entityType, 
                recordCount);

            return $"Query completed for {recordCount} {entityType} records in {stopwatch.ElapsedMilliseconds}ms";
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            Logger.LogError(ex, "Query performance demo failed");
            throw;
        }
    }

    /// <summary>
    /// Demonstrates security event logging
    /// </summary>
    public async Task<string> DemoSecurityEventAsync(string eventType, bool isSuccessful = true)
    {
        Check.NotNullOrWhiteSpace(eventType, nameof(eventType));

        var description = $"Demonstrating {eventType} security event";
        var resource = "DemoResource";

        if (isSuccessful)
        {
            await _securityLogService.LogAuthenticationSuccessAsync(eventType, description, resource);
            return $"Successfully logged security event: {eventType}";
        }
        else
        {
            await _securityLogService.LogAuthenticationFailureAsync(
                eventType, 
                description, 
                "Demo failure reason", 
                resource);
            return $"Logged failed security event: {eventType}";
        }
    }

    /// <summary>
    /// Demonstrates unauthorized access logging
    /// </summary>
    public async Task<string> DemoUnauthorizedAccessAsync(string resource, string action)
    {
        Check.NotNullOrWhiteSpace(resource, nameof(resource));
        Check.NotNullOrWhiteSpace(action, nameof(action));

        await _securityLogService.LogUnauthorizedAccessAsync(
            resource, 
            action, 
            "User lacks required permissions");

        return $"Logged unauthorized access attempt to {resource} for action {action}";
    }

    /// <summary>
    /// Demonstrates data export activity logging
    /// </summary>
    public async Task<string> DemoDataExportAsync(string exportType, int recordCount, string format)
    {
        Check.NotNullOrWhiteSpace(exportType, nameof(exportType));
        Check.NotNullOrWhiteSpace(format, nameof(format));

        await _userActivityLogService.LogDataExportAsync(exportType, recordCount, format);

        return $"Successfully logged export of {recordCount} {exportType} records in {format} format";
    }

    /// <summary>
    /// Demonstrates comprehensive logging for a complex business process
    /// </summary>
    public async Task<string> DemoComplexBusinessProcessAsync(string processName, string entityType, string entityId)
    {
        Check.NotNullOrWhiteSpace(processName, nameof(processName));
        Check.NotNullOrWhiteSpace(entityType, nameof(entityType));
        Check.NotNullOrWhiteSpace(entityId, nameof(entityId));

        var stopwatch = Stopwatch.StartNew();
        
        try
        {
            // Log process start
            await _userActivityLogService.LogActionAsync(
                "ProcessStarted", 
                $"Started {processName} for {entityType} {entityId}",
                "BusinessProcess");

            // Simulate process steps
            await Task.Delay(200);
            
            // Log business operation
            await _businessLogService.LogSuccessAsync(
                LoggingConstants.BusinessOperations.Process, 
                entityType, 
                entityId, 
                $"Completed {processName}");

            stopwatch.Stop();
            
            // Log performance
            await _performanceLogService.LogOperationPerformanceAsync(
                processName, 
                stopwatch.Elapsed, 
                "BusinessProcess");

            // Log completion
            await _userActivityLogService.LogActionAsync(
                "ProcessCompleted", 
                $"Completed {processName} for {entityType} {entityId} in {stopwatch.ElapsedMilliseconds}ms",
                "BusinessProcess");

            return $"Complex business process '{processName}' completed successfully with comprehensive logging";
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            
            // Log failure
            await _businessLogService.LogFailureAsync(
                LoggingConstants.BusinessOperations.Process, 
                entityType, 
                entityId, 
                ex.Message, 
                $"Failed {processName}");

            Logger.LogError(ex, "Complex business process failed");
            throw;
        }
    }
}