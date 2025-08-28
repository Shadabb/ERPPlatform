using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace ERPPlatform.Logging;

/// <summary>
/// Service interface for logging business operations following ABP standards
/// </summary>
public interface IBusinessOperationLogAppService : IApplicationService
{
    /// <summary>
    /// Logs a business operation with structured data
    /// </summary>
    /// <param name="logData">Business operation log data</param>
    Task LogOperationAsync(BusinessOperationLogDto logData);

    /// <summary>
    /// Logs a successful business operation
    /// </summary>
    /// <param name="operation">Type of operation (Create, Update, Delete, etc.)</param>
    /// <param name="entityType">Type of entity being operated on</param>
    /// <param name="entityId">ID of the entity</param>
    /// <param name="description">Optional description of the operation</param>
    Task LogSuccessAsync(string operation, string entityType, string entityId, string? description = null);

    /// <summary>
    /// Logs a failed business operation
    /// </summary>
    /// <param name="operation">Type of operation that failed</param>
    /// <param name="entityType">Type of entity being operated on</param>
    /// <param name="entityId">ID of the entity</param>
    /// <param name="errorMessage">Error message describing the failure</param>
    /// <param name="description">Optional description of the operation</param>
    Task LogFailureAsync(string operation, string entityType, string entityId, string errorMessage, string? description = null);
}