using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using Volo.Abp.DependencyInjection;

namespace ERPPlatform.Logging.Helpers;

/// <summary>
/// Helper class for structured logging operations
/// </summary>
public class StructuredLoggerHelper : ITransientDependency
{
    /// <summary>
    /// Creates structured log properties from a LogEntryDto
    /// </summary>
    /// <param name="logEntry">Log entry containing the data</param>
    /// <returns>Dictionary of structured log properties</returns>
    public Dictionary<string, object> CreateLogProperties(LogEntryDto logEntry)
    {
        var properties = new Dictionary<string, object>();

        // Add standard properties
        AddPropertyIfNotNull(properties, LoggingConstants.PropertyNames.UserId, logEntry.UserId);
        AddPropertyIfNotNull(properties, LoggingConstants.PropertyNames.UserName, logEntry.UserName);
        AddPropertyIfNotNull(properties, LoggingConstants.PropertyNames.TenantId, logEntry.TenantId);
        AddPropertyIfNotNull(properties, LoggingConstants.PropertyNames.IpAddress, logEntry.IpAddress);
        AddPropertyIfNotNull(properties, LoggingConstants.PropertyNames.UserAgent, logEntry.UserAgent);
        AddPropertyIfNotNull(properties, LoggingConstants.PropertyNames.RequestId, logEntry.RequestId);
        AddPropertyIfNotNull(properties, LoggingConstants.PropertyNames.TraceId, logEntry.TraceId);
        
        properties[LoggingConstants.PropertyNames.Timestamp] = logEntry.Timestamp;

        // Add additional properties
        foreach (var additionalProperty in logEntry.AdditionalProperties)
        {
            properties[additionalProperty.Key] = additionalProperty.Value;
        }

        return properties;
    }

    /// <summary>
    /// Creates a logging scope with the provided properties
    /// </summary>
    /// <param name="logger">Logger instance</param>
    /// <param name="properties">Properties to include in the scope</param>
    /// <returns>Disposable logging scope</returns>
    public IDisposable CreateLogScope(ILogger logger, Dictionary<string, object> properties)
    {
        var scopes = new List<IDisposable>();

        foreach (var property in properties.Where(p => p.Value != null))
        {
            scopes.Add(logger.BeginScope(new Dictionary<string, object> { [property.Key] = property.Value }));
        }

        return new CompositeDisposable(scopes);
    }

    /// <summary>
    /// Validates that a log entry has required properties
    /// </summary>
    /// <param name="logEntry">Log entry to validate</param>
    /// <param name="requiredProperties">List of required property names</param>
    /// <returns>True if all required properties are present</returns>
    public bool ValidateLogEntry(LogEntryDto logEntry, params string[] requiredProperties)
    {
        if (logEntry == null) return false;

        foreach (var property in requiredProperties)
        {
            var value = GetLogEntryProperty(logEntry, property);
            if (string.IsNullOrWhiteSpace(value?.ToString()))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Gets the performance level based on duration
    /// </summary>
    /// <param name="duration">Operation duration</param>
    /// <returns>Performance level string</returns>
    public string GetPerformanceLevel(TimeSpan duration)
    {
        var milliseconds = (long)duration.TotalMilliseconds;

        return milliseconds switch
        {
            >= LoggingConstants.PerformanceThresholds.VerySlowOperation => "Critical",
            >= LoggingConstants.PerformanceThresholds.SlowOperation => "Slow",
            >= LoggingConstants.PerformanceThresholds.SlowQuery => "Warning",
            _ => "Normal"
        };
    }

    private void AddPropertyIfNotNull(Dictionary<string, object> properties, string key, object? value)
    {
        if (value != null && !string.IsNullOrWhiteSpace(value.ToString()))
        {
            properties[key] = value;
        }
    }

    private object? GetLogEntryProperty(LogEntryDto logEntry, string propertyName)
    {
        return propertyName switch
        {
            LoggingConstants.PropertyNames.UserId => logEntry.UserId,
            LoggingConstants.PropertyNames.UserName => logEntry.UserName,
            LoggingConstants.PropertyNames.TenantId => logEntry.TenantId,
            LoggingConstants.PropertyNames.IpAddress => logEntry.IpAddress,
            LoggingConstants.PropertyNames.RequestId => logEntry.RequestId,
            LoggingConstants.PropertyNames.TraceId => logEntry.TraceId,
            _ => logEntry.AdditionalProperties.GetValueOrDefault(propertyName)
        };
    }
}

/// <summary>
/// Disposable that manages multiple disposables
/// </summary>
internal class CompositeDisposable : IDisposable
{
    private readonly List<IDisposable> _disposables;

    public CompositeDisposable(List<IDisposable> disposables)
    {
        _disposables = disposables;
    }

    public void Dispose()
    {
        foreach (var disposable in _disposables)
        {
            try
            {
                disposable?.Dispose();
            }
            catch
            {
                // Ignore disposal exceptions to prevent cascading failures
            }
        }
    }
}