using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;
using Volo.Abp.Users;

namespace ERPPlatform.Logging;

public interface ILoggingExampleAppService
{
    Task<string> DemoStructuredLoggingAsync(string inputData);
    Task<string> DemoPerformanceLoggingAsync(int delayMs);
    Task<string> DemoErrorLoggingAsync(bool shouldThrow);
    Task<string> DemoBusinessOperationLoggingAsync(string entityType, int entityId);
}

public class LoggingExampleAppService : ApplicationService, ILoggingExampleAppService
{
    private readonly IStructuredLoggerService _structuredLogger;

    public LoggingExampleAppService(IStructuredLoggerService structuredLogger)
    {
        _structuredLogger = structuredLogger;
    }

    public async Task<string> DemoStructuredLoggingAsync(string inputData)
    {
        // Example 1: Simple structured logging
        _structuredLogger.LogInformation("Processing demo request with input: {InputData}", inputData);

        // Example 2: Structured logging with additional properties
        var properties = new Dictionary<string, object>
        {
            ["RequestId"] = Guid.NewGuid().ToString(),
            ["InputLength"] = inputData?.Length ?? 0,
            ["ProcessingStep"] = "Validation"
        };

        _structuredLogger.LogInformationWithProperties(
            "Validating input data for demo request", 
            properties
        );

        // Example 3: Log user activity
        _structuredLogger.LogUserActivity(
            CurrentUser.Id?.ToString(),
            "DemoRequest",
            $"User requested demo with data: {inputData}",
            new Dictionary<string, object>
            {
                ["InputData"] = inputData,
                ["Service"] = nameof(LoggingExampleAppService)
            }
        );

        await Task.Delay(100); // Simulate some work

        _structuredLogger.LogInformation("Demo request completed successfully");
        
        return $"Processed: {inputData} at {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC";
    }

    public async Task<string> DemoPerformanceLoggingAsync(int delayMs)
    {
        var stopwatch = Stopwatch.StartNew();
        
        _structuredLogger.LogInformation("Starting performance demo with {DelayMs}ms delay", delayMs);

        try
        {
            // Simulate work
            await Task.Delay(delayMs);
            
            stopwatch.Stop();
            
            // Log performance metrics
            _structuredLogger.LogPerformance(
                "DemoPerformanceLogging", 
                stopwatch.Elapsed,
                new Dictionary<string, object>
                {
                    ["RequestedDelayMs"] = delayMs,
                    ["ActualDelayMs"] = stopwatch.ElapsedMilliseconds
                }
            );

            return $"Performance demo completed in {stopwatch.ElapsedMilliseconds}ms";
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            
            _structuredLogger.LogError(
                ex, 
                "Performance demo failed after {ElapsedMs}ms with delay {DelayMs}ms", 
                stopwatch.ElapsedMilliseconds, 
                delayMs
            );
            
            throw;
        }
    }

    public async Task<string> DemoErrorLoggingAsync(bool shouldThrow)
    {
        _structuredLogger.LogInformation("Starting error demo with shouldThrow: {ShouldThrow}", shouldThrow);

        if (shouldThrow)
        {
            var context = new Dictionary<string, object>
            {
                ["ShouldThrow"] = shouldThrow,
                ["DemoType"] = "ErrorHandling",
                ["RequestTime"] = DateTime.UtcNow
            };

            var exception = new InvalidOperationException("This is a demo exception for logging purposes");
            
            _structuredLogger.LogErrorWithProperties(
                exception,
                "Demo exception was intentionally thrown",
                context
            );

            throw exception;
        }

        await Task.Delay(50);
        
        _structuredLogger.LogInformation("Error demo completed without throwing exception");
        return "No error thrown";
    }

    public async Task<string> DemoBusinessOperationLoggingAsync(string entityType, int entityId)
    {
        // Log business operation
        _structuredLogger.LogBusinessOperation(
            "View",
            entityType,
            entityId,
            CurrentUser.Id?.ToString(),
            new Dictionary<string, object>
            {
                ["Source"] = "Demo",
                ["RequestTime"] = DateTime.UtcNow,
                ["UserAgent"] = "DemoClient"
            }
        );

        await Task.Delay(25);

        // Log another business operation
        _structuredLogger.LogBusinessOperation(
            "Update",
            entityType,
            entityId,
            CurrentUser.Id?.ToString(),
            new Dictionary<string, object>
            {
                ["Changes"] = "Demo update operation",
                ["PreviousValue"] = "OldValue",
                ["NewValue"] = "NewValue"
            }
        );

        return $"Business operations logged for {entityType} {entityId}";
    }
}