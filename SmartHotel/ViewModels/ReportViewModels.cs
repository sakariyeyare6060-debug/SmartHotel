using System.ComponentModel.DataAnnotations;

namespace SmartHotel.ViewModels;

public enum ReportMode { Daily = 0, Monthly = 1, Custom = 2 }

public class ReportFilter
{
    public ReportMode Mode { get; set; } = ReportMode.Daily;

    [DataType(DataType.Date)]
    public DateTime? Date { get; set; }

    /// <summary>yyyy-MM (from &lt;input type="month"&gt;).</summary>
    public string? Month { get; set; }

    [DataType(DataType.Date)]
    public DateTime? From { get; set; }

    [DataType(DataType.Date)]
    public DateTime? To { get; set; }
}

public record NamedValue(string Name, int Count, decimal Amount);

public class ReportViewModel
{
    public ReportFilter Filter { get; set; } = new();
    public string PeriodLabel { get; set; } = string.Empty;
    public DateTime From { get; set; }
    public DateTime To { get; set; }

    public int TotalBookings { get; set; }
    public int CancelledBookings { get; set; }
    public decimal TotalRevenue { get; set; }
    public decimal RoomRevenue { get; set; }
    public double OccupancyRate { get; set; }
    public int RoomNightsSold { get; set; }
    public decimal AverageDailyRate { get; set; }
    public int CheckIns { get; set; }
    public int CheckOuts { get; set; }
    public int NewGuests { get; set; }

    public string SeriesTitle { get; set; } = "Revenue";
    public List<string> SeriesLabels { get; set; } = [];
    public List<decimal> SeriesValues { get; set; } = [];

    public List<NamedValue> PaymentsByMethod { get; set; } = [];
    public List<NamedValue> BookingsByStatus { get; set; } = [];
    public List<NamedValue> RoomTypePerformance { get; set; } = [];
    public List<NamedValue> TopGuests { get; set; } = [];
}
