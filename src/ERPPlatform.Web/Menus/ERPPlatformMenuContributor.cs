using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ERPPlatform.Localization;
using ERPPlatform.MultiTenancy;
using Volo.Abp.Account.Localization;
using Volo.Abp.Authorization.Permissions;
using Volo.Abp.Identity.Web.Navigation;
using Volo.Abp.SettingManagement.Web.Navigation;
using Volo.Abp.TenantManagement.Web.Navigation;
using Volo.Abp.UI.Navigation;
using Volo.Abp.Users;

namespace ERPPlatform.Web.Menus;

public class ERPPlatformMenuContributor : IMenuContributor
{
    private readonly IConfiguration _configuration;

    public ERPPlatformMenuContributor(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public async Task ConfigureMenuAsync(MenuConfigurationContext context)
    {
        if (context.Menu.Name == StandardMenus.Main)
        {
            await ConfigureMainMenuAsync(context);
        }
        else if (context.Menu.Name == StandardMenus.User)
        {
            await ConfigureUserMenuAsync(context);
        }
    }

    private Task ConfigureMainMenuAsync(MenuConfigurationContext context)
    {
        var administration = context.Menu.GetAdministration();
        var l = context.GetLocalizer<ERPPlatformResource>();

        context.Menu.Items.Insert(
            0,
            new ApplicationMenuItem(
                ERPPlatformMenus.Home,
                l["Menu:Home"],
                "~/",
                icon: "fas fa-home",
                order: 0
            )
        );

        // Add Log Analytics with sub-menu for both ABP Audit Logs and Serilog Analytics
        var logAnalyticsMenu = new ApplicationMenuItem(
            ERPPlatformMenus.LogAnalytics,
            l["Menu:LogAnalytics"],
            "#",
            icon: "fas fa-chart-line",
            order: 1
        );

        logAnalyticsMenu.AddItem(
            new ApplicationMenuItem(
                "LogAnalytics.AuditDashboard",
                l["Menu:AuditLogsDashboard"],
                "~/log-analytics/dashboard",
                icon: "fas fa-users-cog",
                order: 1
            )
        );

        logAnalyticsMenu.AddItem(
            new ApplicationMenuItem(
                "LogAnalytics.SerilogDashboard",
                l["Menu:SerilogDashboard"],
                "~/log-analytics/serilog-dashboard",
                icon: "fas fa-server",
                order: 2
            )
        );

        context.Menu.AddItem(logAnalyticsMenu);

        if (MultiTenancyConsts.IsEnabled)
        {
            administration.SetSubItemOrder(TenantManagementMenuNames.GroupName, 1);
        }
        else
        {
            administration.TryRemoveMenuItem(TenantManagementMenuNames.GroupName);
        }

        administration.SetSubItemOrder(IdentityMenuNames.GroupName, 2);
        administration.SetSubItemOrder(SettingManagementMenuNames.GroupName, 3);

        return Task.CompletedTask;
    }

    private Task ConfigureUserMenuAsync(MenuConfigurationContext context)
    {
        var l = context.GetLocalizer<ERPPlatformResource>();
        var accountStringLocalizer = context.GetLocalizer<AccountResource>();
        var authServerUrl = _configuration["AuthServer:Authority"] ?? "";

        context.Menu.AddItem(new ApplicationMenuItem("Account.Manage", accountStringLocalizer["MyAccount"],
            $"{authServerUrl.EnsureEndsWith('/')}Account/Manage?returnUrl={_configuration["App:SelfUrl"]}", icon: "fa fa-cog", order: 1000, null, "_blank").RequireAuthenticated());
        context.Menu.AddItem(new ApplicationMenuItem("Account.Logout", l["Logout"], url: "~/Account/Logout", icon: "fa fa-power-off", order: int.MaxValue - 1000).RequireAuthenticated());

        return Task.CompletedTask;
    }
}
