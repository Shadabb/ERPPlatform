namespace ERPPlatform.Permissions;

public static class ERPPlatformPermissions
{
    public const string GroupName = "ERPPlatform";

    public static class LogAnalytics
    {
        public const string Default = GroupName + ".LogAnalytics";
        public const string Dashboard = Default + ".Dashboard";
        public const string SerilogDashboard = Default + ".SerilogDashboard";
        public const string ViewLogs = Default + ".ViewLogs";
        public const string SearchLogs = Default + ".SearchLogs";
        public const string ExportLogs = Default + ".ExportLogs";
        public const string ManageConfiguration = Default + ".ManageConfiguration";
    }

    public static class AuditLogs
    {
        public const string Default = GroupName + ".AuditLogs";
        public const string View = Default + ".View";
        public const string Export = Default + ".Export";
        public const string Delete = Default + ".Delete";
    }

    //Add your own permission names. Example:
    //public const string MyPermission1 = GroupName + ".MyPermission1";
}
