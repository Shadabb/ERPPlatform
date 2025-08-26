using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp.AspNetCore.Mvc.UI.RazorPages;

namespace ERPPlatform.Web.Pages.LogAnalytics;

[Authorize]
public class DashboardModel : AbpPageModel
{
    public void OnGet()
    {
        // Page initialization logic can go here
    }
}