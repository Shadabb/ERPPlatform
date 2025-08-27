using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp.AspNetCore.Mvc;
using ERPPlatform.LogAnalytics;

namespace ERPPlatform.Controllers;

[Route("api/app/application-log-seeder")]
public class ApplicationLogDataSeederController : AbpController
{
    private readonly ApplicationLogDataSeederAppService _seederService;

    public ApplicationLogDataSeederController(ApplicationLogDataSeederAppService seederService)
    {
        _seederService = seederService;
    }

    [HttpGet("seed-dummy-data")]
    public async Task<string> SeedDummyDataAsync(int count = 50)
    {
        return await _seederService.SeedDummyApplicationLogsAsync(count);
    }
}