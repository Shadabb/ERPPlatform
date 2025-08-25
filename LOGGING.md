# Serilog Implementation Guide for ERPPlatform

## Overview

This document describes the comprehensive Serilog implementation in the ERPPlatform application, including structured logging, performance monitoring, and business operation tracking.

## Architecture

The logging implementation consists of several components:

1. **Core Serilog Integration**: Configuration-based setup in all applications
2. **Structured Logger Service**: High-level service for structured logging
3. **Performance Logging**: Automatic performance monitoring with interceptors
4. **Business Operation Logging**: Domain-specific logging for business activities
5. **Multiple Sinks**: File, Console, and PostgreSQL database logging

## Configuration

### Serilog Packages Added

All applications now include these Serilog packages:

```xml
<PackageReference Include="Serilog.AspNetCore" Version="9.0.0" />
<PackageReference Include="Serilog.Sinks.Async" Version="2.1.0" />
<PackageReference Include="Serilog.Sinks.File" Version="6.0.0" />
<PackageReference Include="Serilog.Sinks.PostgreSQL" Version="3.5.0" />
<PackageReference Include="Serilog.Enrichers.Environment" Version="3.0.1" />
<PackageReference Include="Serilog.Enrichers.Process" Version="3.0.0" />
<PackageReference Include="Serilog.Enrichers.Thread" Version="4.0.0" />
<PackageReference Include="Serilog.Settings.Configuration" Version="8.0.4" />
```

### Application Configuration

Each application has its own logging configuration in `appsettings.json`:

#### Development Configuration Features:
- **File Logging**: Daily rolling files with size limits
- **Console Logging**: Formatted output for development
- **Database Logging**: Warning+ level events stored in PostgreSQL
- **Enrichment**: Machine name, environment, process info, thread ID

#### Production Configuration Features:
- **Centralized Logging**: Files stored in `/var/logs/erpplatform/`
- **Extended Retention**: 90-day log retention
- **Error-Only Database**: Only errors and critical events in database
- **Performance Optimized**: Async sinks for high-throughput scenarios

## Logging Sinks

### 1. Console Sink
- **Purpose**: Development debugging and container logs
- **Format**: `[{Timestamp:HH:mm:ss} {Level:u3}] {SourceContext}: {Message:lj}{NewLine}{Exception}`
- **Level**: All levels in development

### 2. File Sink
- **Purpose**: Persistent log storage with rotation
- **Location**: 
  - Development: `Logs/{application-name}-.txt`
  - Production: `/var/logs/erpplatform/{application-name}-.txt`
- **Rotation**: Daily with 10MB/50MB size limits
- **Retention**: 30 days (dev) / 90 days (prod)

### 3. PostgreSQL Sink
- **Purpose**: Queryable log storage for analysis
- **Tables**: 
  - `Logs` (API Host)
  - `AuthServerLogs` (Auth Server)
  - `WebAppLogs` (Web App)
- **Level**: Warning+ (configurable)
- **Auto-creation**: Tables created automatically

## Structured Logging Service

### IStructuredLoggerService

The service provides methods for different logging scenarios:

```csharp
// Basic structured logging
_structuredLogger.LogInformation("User {UserId} performed action {Action}", userId, action);

// Logging with properties
_structuredLogger.LogInformationWithProperties(
    "Complex operation completed", 
    new Dictionary<string, object>
    {
        ["Operation"] = "DataSync",
        ["RecordsProcessed"] = 1500,
        ["Duration"] = TimeSpan.FromMinutes(2)
    }
);

// Business operation logging
_structuredLogger.LogBusinessOperation("Create", "Product", productId, userId);

// Performance logging
_structuredLogger.LogPerformance("DatabaseQuery", queryDuration);

// Security event logging
_structuredLogger.LogSecurityEvent("LoginAttempt", userId, "Failed login attempt");
```

### Key Features

1. **Automatic Context**: Current user, tenant, timestamp automatically included
2. **Sensitive Data Protection**: Passwords and secrets are automatically redacted
3. **Performance Monitoring**: Slow operations automatically flagged
4. **Security Tracking**: Security events with IP address and user agent
5. **Business Auditing**: Complete audit trail for business operations

## Performance Logging

### PerformanceLoggingInterceptor

Automatic performance monitoring using Castle DynamicProxy:

```csharp
public class SampleService : ITransientDependency
{
    // This method will be automatically monitored
    public async Task<string> ProcessDataAsync(string data)
    {
        // Method implementation
        await Task.Delay(1000);
        return "Processed: " + data;
    }
}
```

**Features:**
- **Automatic Timing**: All public methods timed automatically
- **Slow Query Detection**: Operations > 5 seconds logged as warnings
- **Parameter Logging**: Method parameters logged (sensitive data redacted)
- **Exception Correlation**: Performance data included with exceptions
- **Async Support**: Full support for async/await patterns

## Demo API Endpoints

The application includes demo endpoints to showcase logging capabilities:

### Available Endpoints:

1. **GET /api/logging-demo/structured/{inputData}**
   - Demonstrates structured logging with properties
   - Shows user activity logging

2. **GET /api/logging-demo/performance/{delayMs}**
   - Demonstrates performance logging
   - Shows slow operation detection

3. **GET /api/logging-demo/error/{shouldThrow}**
   - Demonstrates error logging
   - Shows exception handling with context

4. **GET /api/logging-demo/business-operation/{entityType}/{entityId}**
   - Demonstrates business operation logging
   - Shows audit trail creation

5. **POST /api/logging-demo/security-event**
   - Demonstrates security event logging
   - Shows structured security audit

## Log Analysis Queries

### PostgreSQL Queries for Log Analysis

```sql
-- Find slow operations (> 5 seconds)
SELECT 
    "Timestamp",
    "Properties"->>'Operation' as Operation,
    ("Properties"->>'DurationMs')::numeric as DurationMs,
    "Properties"->>'UserId' as UserId
FROM "Logs" 
WHERE "Properties"->>'Category' = 'Performance' 
    AND ("Properties"->>'DurationMs')::numeric > 5000
ORDER BY "Timestamp" DESC;

-- Security events by user
SELECT 
    "Timestamp",
    "Properties"->>'EventType' as EventType,
    "Properties"->>'UserId' as UserId,
    "Properties"->>'IPAddress' as IPAddress,
    "Message"
FROM "AuthServerLogs" 
WHERE "Properties"->>'Category' = 'Security'
ORDER BY "Timestamp" DESC;

-- Business operations audit trail
SELECT 
    "Timestamp",
    "Properties"->>'Operation' as Operation,
    "Properties"->>'EntityType' as EntityType,
    "Properties"->>'EntityId' as EntityId,
    "Properties"->>'UserId' as UserId,
    "Properties"->>'UserName' as UserName
FROM "Logs" 
WHERE "Properties"->>'Category' = 'BusinessOperation'
ORDER BY "Timestamp" DESC;

-- Error summary by application
SELECT 
    "Properties"->>'Application' as Application,
    COUNT(*) as ErrorCount,
    MIN("Timestamp") as FirstError,
    MAX("Timestamp") as LastError
FROM "Logs" 
WHERE "Level" = 'Error' 
    AND "Timestamp" >= NOW() - INTERVAL '24 hours'
GROUP BY "Properties"->>'Application';
```

## Best Practices

### 1. Log Levels
- **Debug**: Detailed diagnostic information
- **Information**: General application flow
- **Warning**: Potentially harmful situations
- **Error**: Error events that allow application to continue
- **Critical**: Very serious error events

### 2. Message Templates
```csharp
// Good: Structured with parameters
_logger.LogInformation("User {UserId} created order {OrderId} with {ItemCount} items", 
    userId, orderId, itemCount);

// Bad: String concatenation
_logger.LogInformation($"User {userId} created order {orderId} with {itemCount} items");
```

### 3. Performance Considerations
- Use async sinks for high-volume scenarios
- Set appropriate minimum log levels for production
- Consider log retention policies for disk space management
- Use database logging sparingly (errors/warnings only)

### 4. Security
- Never log sensitive data (passwords, tokens, personal information)
- Use the built-in sensitive data detection
- Consider data privacy regulations (GDPR, etc.)
- Regularly review and purge old logs

## Monitoring and Alerting

### Recommended Monitoring
1. **Log Volume**: Monitor for unusual spikes in log volume
2. **Error Rate**: Track error percentages and trends
3. **Performance**: Monitor for increasing response times
4. **Security**: Alert on suspicious patterns or events
5. **Disk Space**: Monitor log storage usage

### Integration Points
- **Application Performance Monitoring (APM)**: Integrate with tools like Application Insights
- **Log Aggregation**: Consider ELK Stack or similar for log analysis
- **Alerting**: Set up alerts for critical errors and security events
- **Dashboards**: Create monitoring dashboards for operations teams

## Troubleshooting

### Common Issues

1. **PostgreSQL Connection Issues**
   - Verify connection string in configuration
   - Ensure PostgreSQL user has table creation permissions
   - Check network connectivity

2. **File Permission Issues**
   - Ensure application has write permissions to log directory
   - Check log directory exists and is accessible
   - Verify disk space availability

3. **Performance Impact**
   - Reduce log level in production if needed
   - Enable async sinks for high-volume scenarios
   - Consider log sampling for very high-traffic applications

### Configuration Validation

Use this minimal configuration to test basic functionality:

```json
{
  "Serilog": {
    "MinimumLevel": "Information",
    "WriteTo": [
      {
        "Name": "Console"
      }
    ]
  }
}
```

## Future Enhancements

### Planned Features
1. **Log Analytics Dashboard**: Web-based log analysis interface
2. **Real-time Monitoring**: SignalR-based real-time log streaming
3. **Advanced Correlation**: Request correlation across microservices
4. **Machine Learning**: Anomaly detection in log patterns
5. **Export Features**: Log export to various formats (CSV, JSON, etc.)

This comprehensive logging implementation provides a solid foundation for monitoring, debugging, and auditing your ERPPlatform application.