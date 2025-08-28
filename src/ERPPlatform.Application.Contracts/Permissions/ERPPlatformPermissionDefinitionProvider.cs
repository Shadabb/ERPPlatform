using ERPPlatform.Localization;
using Volo.Abp.Authorization.Permissions;
using Volo.Abp.Localization;

namespace ERPPlatform.Permissions;

public class ERPPlatformPermissionDefinitionProvider : PermissionDefinitionProvider
{
    public override void Define(IPermissionDefinitionContext context)
    {
        var myGroup = context.AddGroup(ERPPlatformPermissions.GroupName, L("Permission:ERPPlatform"));

        // Log Analytics Permissions
        var logAnalyticsPermission = myGroup.AddPermission(ERPPlatformPermissions.LogAnalytics.Default, L("Permission:LogAnalytics"));
        logAnalyticsPermission.AddChild(ERPPlatformPermissions.LogAnalytics.Dashboard, L("Permission:LogAnalytics.Dashboard"));
        logAnalyticsPermission.AddChild(ERPPlatformPermissions.LogAnalytics.SerilogDashboard, L("Permission:LogAnalytics.SerilogDashboard"));
        logAnalyticsPermission.AddChild(ERPPlatformPermissions.LogAnalytics.ViewLogs, L("Permission:LogAnalytics.ViewLogs"));
        logAnalyticsPermission.AddChild(ERPPlatformPermissions.LogAnalytics.SearchLogs, L("Permission:LogAnalytics.SearchLogs"));
        logAnalyticsPermission.AddChild(ERPPlatformPermissions.LogAnalytics.ExportLogs, L("Permission:LogAnalytics.ExportLogs"));
        logAnalyticsPermission.AddChild(ERPPlatformPermissions.LogAnalytics.ManageConfiguration, L("Permission:LogAnalytics.ManageConfiguration"));

        // Audit Logs Permissions
        var auditLogsPermission = myGroup.AddPermission(ERPPlatformPermissions.AuditLogs.Default, L("Permission:AuditLogs"));
        auditLogsPermission.AddChild(ERPPlatformPermissions.AuditLogs.View, L("Permission:AuditLogs.View"));
        auditLogsPermission.AddChild(ERPPlatformPermissions.AuditLogs.Export, L("Permission:AuditLogs.Export"));
        auditLogsPermission.AddChild(ERPPlatformPermissions.AuditLogs.Delete, L("Permission:AuditLogs.Delete"));
    }

    private static LocalizableString L(string name)
    {
        return LocalizableString.Create<ERPPlatformResource>(name);
    }
}
