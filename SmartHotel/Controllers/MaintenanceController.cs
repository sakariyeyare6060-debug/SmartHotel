using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartHotel.Services;
using SmartHotel.ViewModels;

namespace SmartHotel.Controllers;

/// <summary>Maintenance: rooms out of service, repair reports and their costs.</summary>
[Authorize(Policy = Policies.Maintenance)]
public class MaintenanceController(IMaintenanceService maintenance) : AppController
{
    public async Task<IActionResult> Index() => View(await BuildAsync(new RepairFormViewModel()));

    [HttpPost]
    public async Task<IActionResult> Resolve([Bind(Prefix = "Form")] RepairFormViewModel model)
    {
        if (ModelState.IsValid)
        {
            var result = await maintenance.ResolveAsync(model, CurrentUser);
            if (result.Succeeded)
            {
                var r = result.Value!;
                Success($"Repair saved for Room {r.Room?.RoomNumber} ({Ui.Money(r.Cost)}). The room was sent to housekeeping for cleaning.");
                return RedirectToAction(nameof(Index));
            }
            ModelState.AddModelError(string.Empty, result.Error!);
        }

        // Re-show the page with the form open so nothing typed is lost.
        Error(FirstModelError());
        return View(nameof(Index), await BuildAsync(model));
    }

    private async Task<MaintenanceViewModel> BuildAsync(RepairFormViewModel form)
    {
        var (count, cost) = await maintenance.GetMonthTotalsAsync();
        return new MaintenanceViewModel
        {
            Rooms = await maintenance.GetRoomsAsync(),
            RecentRepairs = await maintenance.GetRecentRepairsAsync(15),
            RepairsThisMonth = count,
            CostThisMonth = cost,
            Form = form
        };
    }
}
