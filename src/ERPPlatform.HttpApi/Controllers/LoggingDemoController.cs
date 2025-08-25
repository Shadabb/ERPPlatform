using ERPPlatform.Logging;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;
using Volo.Abp.AspNetCore.Mvc;

namespace ERPPlatform.Controllers;

[ApiController]
[Route("api/logging-demo")]
public class LoggingDemoController : AbpControllerBase
{
    private readonly ILoggingExampleAppService _loggingExampleAppService;
    private readonly IStructuredLoggerService _structuredLogger;

    public LoggingDemoController(
        ILoggingExampleAppService loggingExampleAppService,
        IStructuredLoggerService structuredLogger)
    {
        _loggingExampleAppService = loggingExampleAppService;
        _structuredLogger = structuredLogger;
    }

    /// <summary>
    /// Demo structured logging with various log levels and properties
    /// </summary>
    [HttpGet("structured/{inputData}")]
    public async Task<ActionResult<string>> DemoStructuredLogging(string inputData)
    {
        _structuredLogger.LogInformation("API endpoint called: {Endpoint} with parameter: {InputData}", 
            nameof(DemoStructuredLogging), inputData);

        var result = await _loggingExampleAppService.DemoStructuredLoggingAsync(inputData);
        
        _structuredLogger.LogInformation("API endpoint {Endpoint} completed successfully", 
            nameof(DemoStructuredLogging));

        return Ok(result);
    }

    /// <summary>
    /// Demo performance logging with configurable delay
    /// </summary>
    [HttpGet("performance/{delayMs:int}")]
    public async Task<ActionResult<string>> DemoPerformanceLogging(int delayMs)
    {
        _structuredLogger.LogInformation("Performance demo API called with delay: {DelayMs}ms", delayMs);

        var result = await _loggingExampleAppService.DemoPerformanceLoggingAsync(delayMs);
        
        return Ok(result);
    }

    /// <summary>
    /// Demo error logging and exception handling
    /// </summary>
    [HttpGet("error/{shouldThrow:bool}")]
    public async Task<ActionResult<string>> DemoErrorLogging(bool shouldThrow)
    {
        _structuredLogger.LogInformation("Error demo API called with shouldThrow: {ShouldThrow}", shouldThrow);

        try
        {
            var result = await _loggingExampleAppService.DemoErrorLoggingAsync(shouldThrow);
            return Ok(result);
        }
        catch (System.Exception ex)
        {
            _structuredLogger.LogError(ex, "Error demo API failed as expected");
            return BadRequest($"Demo exception occurred: {ex.Message}");
        }
    }

    /// <summary>
    /// Demo business operation logging
    /// </summary>
    [HttpGet("business-operation/{entityType}/{entityId:int}")]
    public async Task<ActionResult<string>> DemoBusinessOperationLogging(string entityType, int entityId)
    {
        _structuredLogger.LogUserActivity(
            CurrentUser.Id?.ToString(),
            "AccessBusinessOperationDemo",
            $"Accessed business operation demo for {entityType} {entityId}"
        );

        var result = await _loggingExampleAppService.DemoBusinessOperationLoggingAsync(entityType, entityId);
        
        return Ok(result);
    }

    /// <summary>
    /// Demo security event logging
    /// </summary>
    [HttpPost("security-event")]
    public async Task<ActionResult<string>> DemoSecurityEventLogging([FromBody] SecurityEventRequest request)
    {
        _structuredLogger.LogSecurityEvent(
            request.EventType,
            CurrentUser.Id?.ToString(),
            request.Details,
            new System.Collections.Generic.Dictionary<string, object>
            {
                ["ApiEndpoint"] = nameof(DemoSecurityEventLogging),
                ["RequestTime"] = System.DateTime.UtcNow,
                ["Severity"] = request.Severity
            }
        );

        await Task.Delay(100); // Simulate processing

        return Ok($"Security event '{request.EventType}' logged successfully");
    }
}

public class SecurityEventRequest
{
    public string EventType { get; set; }
    public string Details { get; set; }
    public string Severity { get; set; } = "Medium";
}