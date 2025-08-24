using ERPPlatform.Localization;
using Volo.Abp.AspNetCore.Mvc.UI.RazorPages;

namespace ERPPlatform.Web.Pages;

public abstract class ERPPlatformPageModel : AbpPageModel
{
    protected ERPPlatformPageModel()
    {
        LocalizationResourceType = typeof(ERPPlatformResource);
    }
}
