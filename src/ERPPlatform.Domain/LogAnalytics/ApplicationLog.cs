using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Volo.Abp;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Entities.Auditing;

namespace ERPPlatform.LogAnalytics;

/// <summary>
/// Domain entity representing the ApplicationLogs table created by Serilog
/// This entity maps to the Serilog PostgreSQL sink table structure
/// Inherits from CreationAuditedAggregateRoot to get ABP's creation auditing features
/// Uses CreationAudited instead of FullAudited since logs are immutable and should never be modified/deleted
/// </summary>
[Table("ApplicationLogs")]
public class ApplicationLog : CreationAuditedAggregateRoot<int>
{
    // Id is inherited from CreationAuditedAggregateRoot<int>
    // CreationTime and CreatorId are also inherited for audit tracking
    // No modification or deletion auditing since logs are immutable

    /// <summary>
    /// Log message content
    /// </summary>
    [Column("Message")]
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// Log level (Information, Warning, Error, etc.)
    /// </summary>
    [Column("Level")]
    public string Level { get; set; } = string.Empty;

    /// <summary>
    /// Timestamp when the log was created
    /// </summary>
    [Column("TimeStamp")]
    public DateTime TimeStamp { get; set; }

    /// <summary>
    /// Exception details if present
    /// </summary>
    [Column("Exception")]
    public string? Exception { get; set; }

    /// <summary>
    /// Additional log properties as JSON
    /// </summary>
    [Column("Properties")]
    public string Properties { get; set; } = "{}";

    /// <summary>
    /// Full log event data
    /// </summary>
    [Column("LogEvent")]
    public string? LogEvent { get; set; }

    #region Custom Columns (Added by our configuration)

    /// <summary>
    /// User ID associated with the log entry
    /// </summary>
    [Column("UserId")]
    public string? UserId { get; set; }

    /// <summary>
    /// Request ID for correlation
    /// </summary>
    [Column("RequestId")]
    public string? RequestId { get; set; }

    /// <summary>
    /// Correlation ID for distributed tracing
    /// </summary>
    [Column("CorrelationId")]
    public string? CorrelationId { get; set; }

    /// <summary>
    /// HTTP method (GET, POST, PUT, etc.)
    /// </summary>
    [Column("HttpMethod")]
    public string? HttpMethod { get; set; }

    /// <summary>
    /// Request path/endpoint
    /// </summary>
    [Column("RequestPath")]
    public string? RequestPath { get; set; }

    /// <summary>
    /// HTTP response status code
    /// </summary>
    [Column("ResponseStatusCode")]
    public int? ResponseStatusCode { get; set; }

    /// <summary>
    /// Request duration in milliseconds
    /// </summary>
    [Column("Duration")]
    public long? Duration { get; set; }

    #endregion

    #region Computed Properties

    /// <summary>
    /// Indicates if this is an error log entry
    /// </summary>
    [NotMapped]
    public bool IsError => Level == "Error" || Level == "Fatal";

    /// <summary>
    /// Indicates if this is a warning log entry
    /// </summary>
    [NotMapped]
    public bool IsWarning => Level == "Warning";

    /// <summary>
    /// Indicates if this request was slow
    /// </summary>
    [NotMapped]
    public bool IsSlowRequest => Duration.HasValue && Duration > 5000;

    /// <summary>
    /// Indicates if this is an HTTP request log
    /// </summary>
    [NotMapped]
    public bool IsHttpRequest => !string.IsNullOrEmpty(HttpMethod) && !string.IsNullOrEmpty(RequestPath);

    /// <summary>
    /// Gets the performance level based on duration
    /// </summary>
    [NotMapped]
    public string PerformanceLevel
    {
        get
        {
            if (!Duration.HasValue) return "Unknown";
            return Duration.Value switch
            {
                <= 100 => "Excellent",
                <= 500 => "Good",
                <= 1000 => "Fair", 
                <= 5000 => "Slow",
                _ => "Critical"
            };
        }
    }

    /// <summary>
    /// Gets the HTTP status category
    /// </summary>
    [NotMapped]
    public string StatusCategory
    {
        get
        {
            if (!ResponseStatusCode.HasValue) return "Unknown";
            return ResponseStatusCode.Value switch
            {
                >= 200 and < 300 => "Success",
                >= 300 and < 400 => "Redirect",
                >= 400 and < 500 => "ClientError",
                >= 500 => "ServerError",
                _ => "Unknown"
            };
        }
    }

    /// <summary>
    /// Gets TimeStamp as Local time (since we store local times as Unspecified)
    /// </summary>
    [NotMapped]
    public DateTime TimeStampLocal => DateTime.SpecifyKind(TimeStamp, DateTimeKind.Local);

    /// <summary>
    /// Gets CreationTime as Local time (since we store local times as Unspecified)  
    /// </summary>
    [NotMapped]
    public DateTime CreationTimeLocal => DateTime.SpecifyKind(CreationTime, DateTimeKind.Local);

    /// <summary>
    /// Gets display-friendly formatted timestamp
    /// </summary>
    [NotMapped]
    public string FormattedTimeStamp => TimeStampLocal.ToString("yyyy-MM-dd HH:mm:ss");

    /// <summary>
    /// Gets display-friendly formatted creation time
    /// </summary>
    [NotMapped]
    public string FormattedCreationTime => CreationTimeLocal.ToString("yyyy-MM-dd HH:mm:ss");

    #endregion

    /// <summary>
    /// Protected constructor for Entity Framework
    /// </summary>
    protected ApplicationLog()
    {
    }

    /// <summary>
    /// Constructor for creating new ApplicationLog instances
    /// </summary>
    public ApplicationLog(
        string message, 
        string level, 
        DateTime timeStamp,
        string? exception = null,
        string properties = "{}")
    {
        Message = Check.NotNullOrWhiteSpace(message, nameof(message));
        Level = Check.NotNullOrWhiteSpace(level, nameof(level));
        TimeStamp = timeStamp;
        Exception = exception;
        Properties = properties ?? "{}";
    }
}