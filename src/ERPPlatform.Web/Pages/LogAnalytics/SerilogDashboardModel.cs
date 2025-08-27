using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Volo.Abp.AspNetCore.Mvc.UI.RazorPages;
using Volo.Abp.AspNetCore.Mvc.UI.Alerts;
using ERPPlatform.LogAnalytics;

namespace ERPPlatform.Web.Pages.LogAnalytics;

[Authorize]
public class SerilogDashboardModel : AbpPageModel
{
    private readonly ISerilogAnalyticsAppService _serilogAnalyticsAppService;

    public SerilogDashboardModel(ISerilogAnalyticsAppService serilogAnalyticsAppService)
    {
        _serilogAnalyticsAppService = serilogAnalyticsAppService;
    }

    /// <summary>
    /// Dashboard configuration data for client-side
    /// </summary>
    public DashboardConfiguration Configuration { get; set; } = new();

    public async Task<IActionResult> OnGetAsync()
    {
        try
        {
            // Initialize dashboard configuration
            Configuration = new DashboardConfiguration
            {
                RefreshInterval = 60000, // 1 minute default
                MaxRecordsPerQuery = 1000,
                DefaultDateRangeHours = 24,
                EnableRealTimeUpdates = true,
                ShowPerformanceMetrics = true,
                ShowErrorAnalytics = true
            };

            Logger.LogInformation("Serilog Dashboard page loaded for user {UserId}", CurrentUser?.Id);

            return Page();
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error loading Serilog Dashboard page");
            ShowAlert("Error loading dashboard. Please try again.", AlertType.Danger);
            return Page();
        }
    }

    /// <summary>
    /// Handle AJAX requests for quick dashboard data
    /// </summary>
    public async Task<IActionResult> OnGetQuickStatsAsync()
    {
        try
        {
            var dashboard = await _serilogAnalyticsAppService.GetSerilogDashboardAsync();
            
            var quickStats = new
            {
                totalLogs = dashboard.Statistics.TotalLogs,
                avgResponseTime = Math.Round(dashboard.Statistics.AvgResponseTime, 2),
                errorRate = Math.Round(dashboard.Statistics.ErrorRate, 2),
                systemHealth = dashboard.Performance.HealthStatus,
                lastUpdated = dashboard.GeneratedAt
            };

            return new JsonResult(quickStats);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error getting quick stats");
            return new JsonResult(new { error = "Failed to load quick stats" });
        }
    }

    /// <summary>
    /// Configuration class for dashboard client-side behavior
    /// </summary>
    public class DashboardConfiguration
    {
        public int RefreshInterval { get; set; }
        public int MaxRecordsPerQuery { get; set; }
        public int DefaultDateRangeHours { get; set; }
        public bool EnableRealTimeUpdates { get; set; }
        public bool ShowPerformanceMetrics { get; set; }
        public bool ShowErrorAnalytics { get; set; }
    }

    private void ShowAlert(string message, AlertType type)
    {
        // ABP alert system integration
        Alerts.Add(type, message);
    }
}