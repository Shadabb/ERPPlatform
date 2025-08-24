using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;

namespace ERPPlatform.Web.Pages;

public class IndexModel : ERPPlatformPageModel
{
    public void OnGet()
    {

    }

    public async Task OnPostLoginAsync()
    {
        await HttpContext.ChallengeAsync("oidc");
    }
}
