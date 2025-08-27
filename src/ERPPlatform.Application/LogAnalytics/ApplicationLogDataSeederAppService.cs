using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;
using ERPPlatform.LogAnalytics;
using ERPPlatform.EntityFrameworkCore.LogAnalytics;

namespace ERPPlatform.LogAnalytics;

public class ApplicationLogDataSeederAppService : ApplicationService
{
    private readonly ApplicationLogRepository _applicationLogRepository;

    public ApplicationLogDataSeederAppService(ApplicationLogRepository applicationLogRepository)
    {
        _applicationLogRepository = applicationLogRepository;
    }

    public async Task<string> SeedDummyApplicationLogsAsync(int count = 50)
    {
        try
        {
            Logger.LogInformation("Starting to seed {Count} dummy ApplicationLog records", count);

            var dummyLogs = new List<ApplicationLog>();
            var baseTime = DateTime.Now.AddHours(-12); // Recent data within last 12 hours
            
            var logLevels = new[] { "Information", "Warning", "Error", "Debug" };
            var httpMethods = new[] { "GET", "POST", "PUT", "DELETE" };
            var requestPaths = new[] { 
                "/api/users", "/api/orders", "/api/products", "/api/dashboard", 
                "/api/reports", "/api/settings", "/api/auth/login", "/api/auth/logout" 
            };
            var messages = new[] {
                "HTTP Request completed successfully",
                "User authentication completed",
                "Database query executed",
                "API endpoint accessed",
                "Business operation completed",
                "Validation error occurred",
                "Resource not found",
                "Internal server error"
            };

            var random = new Random();

            for (int i = 0; i < count; i++)
            {
                var level = logLevels[random.Next(logLevels.Length)];
                var httpMethod = httpMethods[random.Next(httpMethods.Length)];
                var requestPath = requestPaths[random.Next(requestPaths.Length)];
                var message = messages[random.Next(messages.Length)];
                
                // Create timestamps spread over the last 12 hours
                var timestamp = baseTime.AddHours(random.Next(0, 12)); // Last 12 hours
                
                var applicationLog = new ApplicationLog(
                    message: $"{message} - {httpMethod} {requestPath}",
                    level: level,
                    timeStamp: timestamp,
                    exception: level == "Error" ? "System.Exception: Sample error for demonstration" : null,
                    properties: $"{{\"RequestId\":\"{Guid.NewGuid()}\",\"UserId\":\"user_{random.Next(1, 10)}\",\"MachineName\":\"WebServer01\"}}"
                );

                // Set HTTP-specific properties
                applicationLog.HttpMethod = httpMethod;
                applicationLog.RequestPath = requestPath;
                applicationLog.ResponseStatusCode = level == "Error" ? 500 : 
                                                  level == "Warning" ? 404 : 200;
                // Create some slow requests (5+ seconds) for demonstration
                applicationLog.Duration = level == "Error" ? random.Next(5000, 15000) : // Slow error requests
                                         level == "Warning" ? random.Next(2000, 8000) :   // Medium to slow warning requests
                                         random.Next(50, 3000); // Normal to slow information requests
                applicationLog.RequestId = Guid.NewGuid().ToString();
                applicationLog.CorrelationId = Guid.NewGuid().ToString();
                applicationLog.UserId = $"user_{random.Next(1, 10)}";

                dummyLogs.Add(applicationLog);
            }

            var insertedCount = await _applicationLogRepository.InsertManyWithoutAuditingAsync(dummyLogs);
            
            Logger.LogInformation("Successfully seeded {InsertedCount} dummy ApplicationLog records", insertedCount);
            
            return $"Successfully created {insertedCount} dummy ApplicationLog records directly to database";
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error seeding dummy ApplicationLog records");
            return $"Error: {ex.Message}";
        }
    }
}