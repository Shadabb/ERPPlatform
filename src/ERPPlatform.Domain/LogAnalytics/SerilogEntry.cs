using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json;

namespace ERPPlatform.LogAnalytics;

/// <summary>
/// Domain entity representing the seriloglogs table created by Serilog PostgreSQL sink
/// Maps directly to the table structure created by Serilog.Sinks.PostgreSQL
/// Note: This is a keyless entity since the original Serilog table doesn't have a primary key
/// </summary>
[Table("seriloglogs")]
public class SerilogEntry
{
    /// <summary>
    /// The rendered log message
    /// </summary>
    [Column("message")]
    public string? Message { get; set; }

    /// <summary>
    /// The message template
    /// </summary>
    [Column("message_template")]
    public string? MessageTemplate { get; set; }

    /// <summary>
    /// Log level as integer (0=Verbose, 1=Debug, 2=Information, 3=Warning, 4=Error, 5=Fatal)
    /// </summary>
    [Column("level")]
    public int? Level { get; set; }

    /// <summary>
    /// Timestamp when the log was created
    /// </summary>
    [Column("timestamp")]
    public DateTime? Timestamp { get; set; }

    /// <summary>
    /// Exception details if present
    /// </summary>
    [Column("exception")]
    public string? Exception { get; set; }

    /// <summary>
    /// Full structured log data in JSONB format
    /// </summary>
    [Column("log_event")]
    public string? LogEvent { get; set; }

    #region Computed Properties

    /// <summary>
    /// Gets the log level as a string
    /// </summary>
    [NotMapped]
    public string LevelName
    {
        get
        {
            return Level switch
            {
                0 => "Verbose",
                1 => "Debug", 
                2 => "Information",
                3 => "Warning",
                4 => "Error",
                5 => "Fatal",
                _ => "Unknown"
            };
        }
    }

    /// <summary>
    /// Indicates if this is an error log entry
    /// </summary>
    [NotMapped]
    public bool IsError => Level.HasValue && Level >= 4; // Error or Fatal

    /// <summary>
    /// Indicates if this is a warning log entry
    /// </summary>
    [NotMapped]
    public bool IsWarning => Level == 3;

    /// <summary>
    /// Indicates if this log entry has an exception
    /// </summary>
    [NotMapped]
    public bool HasException => !string.IsNullOrEmpty(Exception);

    /// <summary>
    /// Gets display-friendly formatted timestamp
    /// </summary>
    [NotMapped]
    public string FormattedTimestamp => Timestamp?.ToString("yyyy-MM-dd HH:mm:ss.fff") ?? "Unknown";

    /// <summary>
    /// Gets parsed log event properties as dynamic object
    /// </summary>
    [NotMapped]
    public JsonDocument? LogEventProperties
    {
        get
        {
            if (string.IsNullOrEmpty(LogEvent)) return null;
            try
            {
                return JsonDocument.Parse(LogEvent);
            }
            catch
            {
                return null;
            }
        }
    }

    /// <summary>
    /// Gets the application name from log event properties
    /// </summary>
    [NotMapped]
    public string Application
    {
        get
        {
            try
            {
                var logEventDoc = LogEventProperties;
                if (logEventDoc?.RootElement.TryGetProperty("Properties", out var properties) == true &&
                    properties.TryGetProperty("Application", out var app) == true)
                {
                    return app.GetString() ?? "Unknown";
                }
            }
            catch
            {
                // Ignore parsing errors
            }
            return "Unknown";
        }
    }

    /// <summary>
    /// Gets the request path from log event properties
    /// </summary>
    [NotMapped]
    public string? RequestPath
    {
        get
        {
            try
            {
                var logEventDoc = LogEventProperties;
                if (logEventDoc?.RootElement.TryGetProperty("Properties", out var properties) == true &&
                    properties.TryGetProperty("RequestPath", out var path) == true)
                {
                    return path.GetString();
                }
            }
            catch
            {
                // Ignore parsing errors
            }
            return null;
        }
    }

    /// <summary>
    /// Gets the HTTP method from log event properties
    /// </summary>
    [NotMapped]
    public string? HttpMethod
    {
        get
        {
            try
            {
                var logEventDoc = LogEventProperties;
                if (logEventDoc?.RootElement.TryGetProperty("Properties", out var properties) == true &&
                    properties.TryGetProperty("HttpMethod", out var method) == true)
                {
                    return method.GetString();
                }
            }
            catch
            {
                // Ignore parsing errors
            }
            return null;
        }
    }

    /// <summary>
    /// Gets the user ID from log event properties
    /// </summary>
    [NotMapped]
    public string? UserId
    {
        get
        {
            try
            {
                var logEventDoc = LogEventProperties;
                if (logEventDoc?.RootElement.TryGetProperty("Properties", out var properties) == true &&
                    properties.TryGetProperty("UserId", out var userId) == true)
                {
                    return userId.GetString();
                }
            }
            catch
            {
                // Ignore parsing errors
            }
            return null;
        }
    }

    #endregion
}