namespace SmartHotel.Infrastructure;

public class HotelSettings
{
    public string Name { get; set; } = "Smart Hotel";
    public string Currency { get; set; } = "$";
    public decimal TaxRate { get; set; } = 0.10m;
    public string CheckInTime { get; set; } = "14:00";
    public string CheckOutTime { get; set; } = "11:00";
    public string? Address { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
}
