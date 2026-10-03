using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartHotel.Services;

namespace SmartHotel.Controllers;

[Authorize(Policy = Policies.FrontDesk)]
public class DashboardController(IDashboardService dashboard) : AppController
{
    public async Task<IActionResult> Index() => View(await dashboard.GetAsync());
}
