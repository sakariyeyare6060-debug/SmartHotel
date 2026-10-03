using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartHotel.Services;

namespace SmartHotel.Controllers.Api;

[ApiController]
[Route("api")]
[Produces("application/json")]
public class DashboardApiController(IDashboardService dashboard, INotificationService notifications) : ControllerBase
{
    /// <summary>GET /api/dashboard — headline statistics.</summary>
    [HttpGet("dashboard"), Authorize(Policy = Policies.FrontDesk)]
    public async Task<IActionResult> GetStats()
    {
        var d = await dashboard.GetAsync();
        return Ok(new
        {
            d.TotalRooms, d.OccupiedRooms, d.AvailableRooms, d.OccupancyRate, d.TotalGuests, d.InHouseGuests,
            d.MonthRevenue, d.OutstandingBalance, d.ArrivalsToday, d.DeparturesToday, d.PendingBookings,
            RoomStatus = d.RoomStatusCounts.ToDictionary(k => k.Key.ToString(), v => v.Value),
            Revenue = d.RevenueLabels.Zip(d.RevenueValues, (month, amount) => new { month, amount })
        });
    }

    /// <summary>GET /api/notifications/unread-count</summary>
    [HttpGet("notifications/unread-count"), Authorize]
    public async Task<IActionResult> UnreadCount() => Ok(new { count = await notifications.GetUnreadCountAsync() });
}
