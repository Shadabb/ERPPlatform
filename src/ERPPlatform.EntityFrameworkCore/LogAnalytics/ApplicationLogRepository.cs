using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ERPPlatform.EntityFrameworkCore;
using ERPPlatform.LogAnalytics;
using Microsoft.EntityFrameworkCore;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore;

namespace ERPPlatform.EntityFrameworkCore.LogAnalytics;

public class ApplicationLogRepository : EfCoreRepository<ERPPlatformDbContext, ApplicationLog, int>, ITransientDependency
{
    public ApplicationLogRepository(IDbContextProvider<ERPPlatformDbContext> dbContextProvider) : base(dbContextProvider)
    {
    }

    public async Task<int> InsertManyWithoutAuditingAsync(IEnumerable<ApplicationLog> entities)
    {
        var dbContext = await GetDbContextAsync();
        var entityList = entities.ToList();
        var insertedCount = 0;

        // Use raw SQL to insert records directly without EF tracking
        foreach (var entity in entityList)
        {
            // Store LOCAL time consistently (convert to Unspecified for PostgreSQL compatibility)
            var creationTime = DateTime.SpecifyKind(DateTime.Now, DateTimeKind.Unspecified);
            var timeStamp = DateTime.SpecifyKind(entity.TimeStamp, DateTimeKind.Unspecified);
            
            var sql = @"
                INSERT INTO ""ApplicationLogs"" 
                (""Message"", ""Level"", ""TimeStamp"", ""Exception"", ""Properties"", ""LogEvent"", ""UserId"", ""RequestId"", ""CorrelationId"", ""HttpMethod"", ""RequestPath"", ""ResponseStatusCode"", ""Duration"", ""CreationTime"", ""ConcurrencyStamp"", ""ExtraProperties"")
                VALUES 
                (@Message, @Level, @TimeStamp, @Exception, @Properties, @LogEvent, @UserId, @RequestId, @CorrelationId, @HttpMethod, @RequestPath, @ResponseStatusCode, @Duration, @CreationTime, @ConcurrencyStamp, @ExtraProperties)";
            
            var rowsAffected = await dbContext.Database.ExecuteSqlRawAsync(sql,
                new Npgsql.NpgsqlParameter("@Message", entity.Message ?? (object)DBNull.Value),
                new Npgsql.NpgsqlParameter("@Level", entity.Level ?? (object)DBNull.Value),
                new Npgsql.NpgsqlParameter("@TimeStamp", timeStamp),
                new Npgsql.NpgsqlParameter("@Exception", entity.Exception ?? (object)DBNull.Value),
                new Npgsql.NpgsqlParameter("@Properties", entity.Properties ?? (object)DBNull.Value),
                new Npgsql.NpgsqlParameter("@LogEvent", entity.LogEvent ?? (object)DBNull.Value),
                new Npgsql.NpgsqlParameter("@UserId", entity.UserId ?? (object)DBNull.Value),
                new Npgsql.NpgsqlParameter("@RequestId", entity.RequestId ?? (object)DBNull.Value),
                new Npgsql.NpgsqlParameter("@CorrelationId", entity.CorrelationId ?? (object)DBNull.Value),
                new Npgsql.NpgsqlParameter("@HttpMethod", entity.HttpMethod ?? (object)DBNull.Value),
                new Npgsql.NpgsqlParameter("@RequestPath", entity.RequestPath ?? (object)DBNull.Value),
                new Npgsql.NpgsqlParameter("@ResponseStatusCode", entity.ResponseStatusCode ?? (object)DBNull.Value),
                new Npgsql.NpgsqlParameter("@Duration", entity.Duration ?? (object)DBNull.Value),
                new Npgsql.NpgsqlParameter("@CreationTime", creationTime),
                new Npgsql.NpgsqlParameter("@ConcurrencyStamp", Guid.NewGuid().ToString("N")[..10]),
                new Npgsql.NpgsqlParameter("@ExtraProperties", "{}")
            );
            
            insertedCount += rowsAffected;
        }

        return insertedCount;
    }
}