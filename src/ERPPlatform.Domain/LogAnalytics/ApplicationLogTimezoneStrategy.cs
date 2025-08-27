using System;

namespace ERPPlatform.LogAnalytics;

/// <summary>
/// Documents the timezone strategy for ApplicationLog entities
/// 
/// STRATEGY: LOCAL TIMEZONE STORAGE
/// 
/// DECISION: Store all timestamps in the server's local timezone
/// REASON: Simplicity - what you see is what you get
/// 
/// ASSUMPTIONS:
/// 1. All servers run in the same timezone
/// 2. Application serves users primarily in one timezone
/// 3. Server timezone will not change
/// 4. Team understands all times are server-local
/// 
/// IMPORTANT NOTES:
/// - Database stores: timestamp without time zone (server local time)
/// - Display shows: same time as stored (no conversion needed)
/// - APIs return: server local time (document this clearly)
/// - Logs from different servers: may show different times for same moment
/// 
/// MIGRATION IMPACT:
/// - If you later want UTC, you'll need to convert existing data
/// - If server moves timezones, historical data becomes confusing
/// 
/// ALTERNATIVES CONSIDERED:
/// - UTC storage: More complex but timezone-independent
/// - Timezone-aware storage: Requires schema changes
/// </summary>
public static class ApplicationLogTimezoneStrategy
{
    /// <summary>
    /// Current strategy: LOCAL timezone storage
    /// </summary>
    public const string CURRENT_STRATEGY = "LOCAL_TIMEZONE_STORAGE";
    
    /// <summary>
    /// Server timezone info for documentation
    /// </summary>
    public static TimeZoneInfo ServerTimezone => TimeZoneInfo.Local;
    
    /// <summary>
    /// Gets current server time for consistent logging
    /// </summary>
    public static DateTime GetLogTime() => DateTime.Now;
    
    /// <summary>
    /// Formats time for consistent display
    /// </summary>
    public static string FormatLogTime(DateTime dateTime) => 
        dateTime.ToString("yyyy-MM-dd HH:mm:ss");
    
    /// <summary>
    /// Creates a properly formatted log timestamp
    /// </summary>
    public static DateTime CreateLogTimestamp() => 
        DateTime.SpecifyKind(GetLogTime(), DateTimeKind.Unspecified);
}