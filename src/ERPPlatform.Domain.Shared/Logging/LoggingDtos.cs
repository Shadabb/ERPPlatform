using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace ERPPlatform.Logging;

/// <summary>
/// Base class for all logging DTOs with common properties
/// </summary>
public abstract class LogEntryDto
{
    public string? UserId { get; set; }
    public string? UserName { get; set; }
    public string? TenantId { get; set; }
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
    public string? RequestId { get; set; }
    public string? TraceId { get; set; }
    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;
    public Dictionary<string, object> AdditionalProperties { get; set; } = new();
}

/// <summary>
/// DTO for business operation logging
/// </summary>
public class BusinessOperationLogDto : LogEntryDto
{
    [Required]
    [StringLength(100)]
    public string Operation { get; set; } = string.Empty;

    [Required]
    [StringLength(100)]
    public string EntityType { get; set; } = string.Empty;

    [Required]
    public string EntityId { get; set; } = string.Empty;

    [StringLength(500)]
    public string? Description { get; set; }

    public bool IsSuccessful { get; set; } = true;
    public string? ErrorMessage { get; set; }
}

/// <summary>
/// DTO for user activity logging
/// </summary>
public class UserActivityLogDto : LogEntryDto
{
    [Required]
    [StringLength(100)]
    public string Action { get; set; } = string.Empty;

    [Required]
    [StringLength(500)]
    public string Details { get; set; } = string.Empty;

    [StringLength(100)]
    public string? Module { get; set; }

    [StringLength(200)]
    public string? Resource { get; set; }
}

/// <summary>
/// DTO for performance logging
/// </summary>
public class PerformanceLogDto : LogEntryDto
{
    [Required]
    [StringLength(200)]
    public string Operation { get; set; } = string.Empty;

    [Required]
    public TimeSpan Duration { get; set; }

    public long DurationMilliseconds => (long)Duration.TotalMilliseconds;

    [StringLength(100)]
    public string? Component { get; set; }

    [StringLength(100)]
    public string? Method { get; set; }

    public bool IsSlowOperation => DurationMilliseconds > LoggingConstants.PerformanceThresholds.SlowOperation;
    public string? QueryDetails { get; set; }
}

/// <summary>
/// DTO for security event logging
/// </summary>
public class SecurityEventLogDto : LogEntryDto
{
    [Required]
    [StringLength(50)]
    public string EventType { get; set; } = string.Empty;

    [Required]
    [StringLength(500)]
    public string Description { get; set; } = string.Empty;

    [StringLength(50)]
    public string Severity { get; set; } = "Medium";

    [StringLength(100)]
    public string? Resource { get; set; }

    public bool IsSuccessful { get; set; } = true;
    public string? FailureReason { get; set; }
}

/// <summary>
/// DTO for system event logging
/// </summary>
public class SystemEventLogDto : LogEntryDto
{
    [Required]
    [StringLength(100)]
    public string EventType { get; set; } = string.Empty;

    [Required]
    [StringLength(500)]
    public string Message { get; set; } = string.Empty;

    [StringLength(50)]
    public string Severity { get; set; } = "Information";

    [StringLength(100)]
    public string? Component { get; set; }

    public string? StackTrace { get; set; }
}