using Microsoft.AspNetCore.Mvc;
using System;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp.AspNetCore.Mvc;
using ERPPlatform.Logging;

namespace ERPPlatform.Controllers;

/// <summary>
/// Modern logging demonstration controller showcasing the refactored logging implementation
/// following ABP standards and best practices
/// </summary>
[ApiController]
[Route("api/logging-demo")]
public class ModernLoggingDemoController : AbpControllerBase
{
    private readonly LoggingExamplesAppService _loggingExamplesService;
    private readonly IBusinessOperationLogAppService _businessLogService;
    private readonly IUserActivityLogAppService _userActivityLogService;
    private readonly IPerformanceLogAppService _performanceLogService;
    private readonly ISecurityEventLogAppService _securityLogService;

    public ModernLoggingDemoController(
        LoggingExamplesAppService loggingExamplesService,
        IBusinessOperationLogAppService businessLogService,
        IUserActivityLogAppService userActivityLogService,
        IPerformanceLogAppService performanceLogService,
        ISecurityEventLogAppService securityLogService)
    {
        _loggingExamplesService = loggingExamplesService;
        _businessLogService = businessLogService;
        _userActivityLogService = userActivityLogService;
        _performanceLogService = performanceLogService;
        _securityLogService = securityLogService;
    }

    /// <summary>
    /// Demonstrates business operation logging
    /// </summary>
    /// <param name="entityType">Type of entity (e.g., "Customer", "Order")</param>
    /// <param name="entityId">ID of the entity</param>
    /// <param name="shouldSucceed">Whether operation should succeed</param>
    [HttpPost("business-operation")]
    public async Task<ActionResult<string>> DemoBusinessOperationAsync(
        [FromQuery] string entityType = "Customer", 
        [FromQuery] string entityId = "123", 
        [FromQuery] bool shouldSucceed = true)
    {
        var result = await _loggingExamplesService.DemoBusinessOperationAsync(entityType, entityId, shouldSucceed);
        return Ok(result);
    }

    /// <summary>
    /// Demonstrates user activity logging
    /// </summary>
    /// <param name="action">Action performed by user</param>
    /// <param name="details">Details of the action</param>
    [HttpPost("user-activity")]
    public async Task<ActionResult<string>> DemoUserActivityAsync(
        [FromQuery] string action = "ViewReport", 
        [FromQuery] string details = "User viewed sales report for Q1 2025")
    {
        var result = await _loggingExamplesService.DemoUserActivityAsync(action, details);
        return Ok(result);
    }

    /// <summary>
    /// Demonstrates performance logging with configurable delay
    /// </summary>
    /// <param name="delayMs">Delay in milliseconds to simulate work</param>
    [HttpPost("performance")]
    public async Task<ActionResult<string>> DemoPerformanceAsync([FromQuery] int delayMs = 500)
    {
        var result = await _loggingExamplesService.DemoPerformanceLoggingAsync(delayMs);
        return Ok(result);
    }

    /// <summary>
    /// Demonstrates database query performance logging
    /// </summary>
    /// <param name="entityType">Type of entity being queried</param>
    /// <param name="recordCount">Number of records in result</param>
    [HttpPost("query-performance")]
    public async Task<ActionResult<string>> DemoQueryPerformanceAsync(
        [FromQuery] string entityType = "Customer", 
        [FromQuery] int recordCount = 1000)
    {
        var result = await _loggingExamplesService.DemoQueryPerformanceAsync(entityType, recordCount);
        return Ok(result);
    }

    /// <summary>
    /// Demonstrates security event logging
    /// </summary>
    /// <param name="eventType">Type of security event</param>
    /// <param name="isSuccessful">Whether event was successful</param>
    [HttpPost("security-event")]
    public async Task<ActionResult<string>> DemoSecurityEventAsync(
        [FromQuery] string eventType = "Login", 
        [FromQuery] bool isSuccessful = true)
    {
        var result = await _loggingExamplesService.DemoSecurityEventAsync(eventType, isSuccessful);
        return Ok(result);
    }

    /// <summary>
    /// Demonstrates unauthorized access logging
    /// </summary>
    /// <param name="resource">Resource being accessed</param>
    /// <param name="action">Action being attempted</param>
    [HttpPost("unauthorized-access")]
    public async Task<ActionResult<string>> DemoUnauthorizedAccessAsync(
        [FromQuery] string resource = "AdminPanel", 
        [FromQuery] string action = "Delete")
    {
        var result = await _loggingExamplesService.DemoUnauthorizedAccessAsync(resource, action);
        return Ok(result);
    }

    /// <summary>
    /// Demonstrates data export logging
    /// </summary>
    /// <param name="exportType">Type of data being exported</param>
    /// <param name="recordCount">Number of records exported</param>
    /// <param name="format">Export format</param>
    [HttpPost("data-export")]
    public async Task<ActionResult<string>> DemoDataExportAsync(
        [FromQuery] string exportType = "Customers", 
        [FromQuery] int recordCount = 500, 
        [FromQuery] string format = "CSV")
    {
        var result = await _loggingExamplesService.DemoDataExportAsync(exportType, recordCount, format);
        return Ok(result);
    }

    /// <summary>
    /// Demonstrates comprehensive logging for complex business processes
    /// </summary>
    /// <param name="processName">Name of the business process</param>
    /// <param name="entityType">Type of entity being processed</param>
    /// <param name="entityId">ID of the entity</param>
    [HttpPost("complex-process")]
    public async Task<ActionResult<string>> DemoComplexProcessAsync(
        [FromQuery] string processName = "OrderFulfillment", 
        [FromQuery] string entityType = "Order", 
        [FromQuery] string entityId = "ORD-2025-001")
    {
        var result = await _loggingExamplesService.DemoComplexBusinessProcessAsync(processName, entityType, entityId);
        return Ok(result);
    }

    /// <summary>
    /// Demonstrates direct service usage for custom logging scenarios
    /// </summary>
    [HttpPost("direct-service-usage")]
    public async Task<ActionResult<string>> DemoDirectServiceUsageAsync()
    {
        // Example: Custom business operation with specific properties
        var customLogData = new BusinessOperationLogDto
        {
            Operation = "CustomOperation",
            EntityType = "CustomEntity",
            EntityId = Guid.NewGuid().ToString(),
            Description = "Demonstrating direct service usage with custom properties",
            IsSuccessful = true
        };

        // Add custom properties
        customLogData.AdditionalProperties["CustomProperty1"] = "Custom Value 1";
        customLogData.AdditionalProperties["CustomProperty2"] = 42;
        customLogData.AdditionalProperties["CustomProperty3"] = DateTime.UtcNow;

        await _businessLogService.LogOperationAsync(customLogData);

        return Ok("Successfully demonstrated direct service usage with custom properties");
    }

    /// <summary>
    /// Gets logging service information
    /// </summary>
    [HttpGet("info")]
    public ActionResult<object> GetLoggingInfo()
    {
        return Ok(new
        {
            Title = "Modern Logging System - ABP Standards Implementation",
            Description = "Refactored logging implementation following ABP best practices",
            Features = new[]
            {
                "Structured logging with typed DTOs",
                "Proper separation of concerns",
                "Context enrichment (user, HTTP, tracing)",
                "Validation and error handling",
                "Performance thresholds and monitoring",
                "Security event tracking",
                "Business operation auditing",
                "User activity logging"
            },
            Services = new[]
            {
                "IBusinessOperationLogAppService - Business operation logging",
                "IUserActivityLogAppService - User activity tracking", 
                "IPerformanceLogAppService - Performance monitoring",
                "ISecurityEventLogAppService - Security event logging"
            },
            Constants = new
            {
                Categories = typeof(LoggingConstants.Categories).GetFields().Select(f => f.GetValue(null)).ToArray(),
                BusinessOperations = typeof(LoggingConstants.BusinessOperations).GetFields().Select(f => f.GetValue(null)).ToArray(),
                SecurityEvents = typeof(LoggingConstants.SecurityEvents).GetFields().Select(f => f.GetValue(null)).ToArray()
            }
        });
    }
}