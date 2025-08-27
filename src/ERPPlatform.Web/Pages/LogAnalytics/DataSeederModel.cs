using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Volo.Abp.AspNetCore.Mvc.UI.RazorPages;
using ERPPlatform.LogAnalytics;

namespace ERPPlatform.Web.Pages.LogAnalytics;

[Authorize]
public class DataSeederModel : AbpPageModel
{
    private readonly IApplicationLogDataSeederAppService _seederService;

    public DataSeederModel(IApplicationLogDataSeederAppService seederService)
    {
        _seederService = seederService;
    }

    [BindProperty]
    public string Message { get; set; } = "";

    public void OnGet()
    {
        // Initial page load
    }

    public async Task<IActionResult> OnPostSeedDataAsync(int count = 20)
    {
        try
        {
            var result = await _seederService.SeedDummyDataAsync(count);
            Message = result;
            Logger.LogInformation("Successfully seeded {Count} ApplicationLog records", count);
        }
        catch (Exception ex)
        {
            Message = $"Error seeding data: {ex.Message}";
            Logger.LogError(ex, "Error seeding ApplicationLog data");
        }

        return Page();
    }

    public async Task<IActionResult> OnPostClearDataAsync()
    {
        try
        {
            var result = await _seederService.ClearDummyDataAsync();
            Message = result;
            Logger.LogInformation("Successfully cleared dummy ApplicationLog records");
        }
        catch (Exception ex)
        {
            Message = $"Error clearing data: {ex.Message}";
            Logger.LogError(ex, "Error clearing ApplicationLog data");
        }

        return Page();
    }
}