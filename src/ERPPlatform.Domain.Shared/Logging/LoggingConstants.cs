namespace ERPPlatform.Logging;

/// <summary>
/// Constants for logging system following ABP naming conventions
/// </summary>
public static class LoggingConstants
{
    /// <summary>
    /// Logging categories for structured logging
    /// </summary>
    public static class Categories
    {
        public const string BusinessOperation = "BusinessOperation";
        public const string UserActivity = "UserActivity";
        public const string Performance = "Performance";
        public const string Security = "Security";
        public const string System = "System";
        public const string ApiRequest = "ApiRequest";
    }

    /// <summary>
    /// Log levels for business operations
    /// </summary>
    public static class BusinessOperations
    {
        public const string Create = "Create";
        public const string Update = "Update";
        public const string Delete = "Delete";
        public const string Read = "Read";
        public const string Import = "Import";
        public const string Export = "Export";
        public const string Process = "Process";
    }

    /// <summary>
    /// Security event types
    /// </summary>
    public static class SecurityEvents
    {
        public const string Login = "Login";
        public const string Logout = "Logout";
        public const string LoginFailed = "LoginFailed";
        public const string UnauthorizedAccess = "UnauthorizedAccess";
        public const string PermissionDenied = "PermissionDenied";
        public const string DataAccess = "DataAccess";
        public const string ConfigurationChange = "ConfigurationChange";
    }

    /// <summary>
    /// Performance thresholds in milliseconds
    /// </summary>
    public static class PerformanceThresholds
    {
        public const int SlowQuery = 1000;
        public const int SlowOperation = 5000;
        public const int VerySlowOperation = 10000;
    }

    /// <summary>
    /// Property names for structured logging
    /// </summary>
    public static class PropertyNames
    {
        public const string UserId = "UserId";
        public const string UserName = "UserName";
        public const string TenantId = "TenantId";
        public const string EntityType = "EntityType";
        public const string EntityId = "EntityId";
        public const string Operation = "Operation";
        public const string Category = "Category";
        public const string Duration = "Duration";
        public const string IpAddress = "IpAddress";
        public const string UserAgent = "UserAgent";
        public const string RequestId = "RequestId";
        public const string TraceId = "TraceId";
        public const string Timestamp = "Timestamp";
    }
}