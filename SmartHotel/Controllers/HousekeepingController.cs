using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartHotel.Services;
using SmartHotel.ViewModels;

namespace SmartHotel.Controllers;

[Authorize(Policy = Policies.Housekeeping)]
public class HousekeepingController(IHousekeepingService housekeeping) : AppController
{
    public async Task<IActionResult> Index(RoomStatus? status, int? floor) =>
        View(new HousekeepingViewModel
        {
            Status = status,
            Floor = floor,
            Counts = await housekeeping.GetStatusCountsAsync(),
            Rooms = await housekeeping.GetRoomsAsync(status, floor),
            Floors = await housekeeping.GetFloorsAsync(),
            RecentLogs = await housekeeping.GetRecentLogsAsync(12)
        });

    [HttpPost]
    public async Task<IActionResult> UpdateStatus(RoomStatusUpdateViewModel model, RoomStatus? filterStatus, int? filterFloor)
    {
        if (!ModelState.IsValid)
        {
            Error(FirstModelError());
        }
        else
        {
            var result = await housekeeping.UpdateStatusAsync(model.RoomId, model.Status, model.Note, CurrentUser);
            if (result.Succeeded) Success($"Room status updated to {model.Status.DisplayName()}.");
            else Error(result.Error!);
        }
        return RedirectToAction(nameof(Index), new { status = filterStatus, floor = filterFloor });
    }
}
