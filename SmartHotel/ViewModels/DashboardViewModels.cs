namespace SmartHotel.ViewModels;

public class DashboardViewModel
{
    public int TotalRooms { get; set; }
    public int OccupiedRooms { get; set; }
    public int AvailableRooms { get; set; }
    public int TotalGuests { get; set; }
    public int InHouseGuests { get; set; }
    public decimal MonthRevenue { get; set; }
    public int ArrivalsToday { get; set; }
    public int DeparturesToday { get; set; }
    public int PendingBookings { get; set; }
    public decimal OutstandingBalance { get; set; }

    public int OccupancyRate => TotalRooms == 0 ? 0 : (int)Math.Round(OccupiedRooms * 100.0 / TotalRooms);

    public List<string> RevenueLabels { get; set; } = [];
    /// <summary>yyyy-MM keys matching <see cref="RevenueLabels"/>, used to link bars to the monthly report.</summary>
    public List<string> RevenueMonths { get; set; } = [];
    public List<decimal> RevenueValues { get; set; } = [];
    public Dictionary<RoomStatus, int> RoomStatusCounts { get; set; } = [];
    public List<Booking> RecentBookings { get; set; } = [];
}

public record StatCardModel(string Label, string Value, string Icon, string Tone, string? Hint = null, string? Url = null);

public class NotificationBellViewModel
{
    public int UnreadCount { get; set; }
    public List<Notification> Latest { get; set; } = [];
}
