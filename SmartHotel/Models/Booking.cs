using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SmartHotel.Models;

public class Booking : BaseEntity, INumbered
{
    [Required, StringLength(20)]
    public string BookingNumber { get; set; } = DocumentNumber.Temp();

    public int GuestId { get; set; }
    public Guest? Guest { get; set; }

    public int RoomId { get; set; }
    public Room? Room { get; set; }

    public DateTime CheckInDate { get; set; }
    public DateTime CheckOutDate { get; set; }

    public int Adults { get; set; } = 1;
    public int Children { get; set; }

    public BookingStatus Status { get; set; } = BookingStatus.Pending;

    /// <summary>Nightly rate captured when the booking was made.</summary>
    public decimal PricePerNight { get; set; }
    public decimal TotalAmount { get; set; }

    [StringLength(500)]
    public string? SpecialRequests { get; set; }

    public DateTime? ActualCheckIn { get; set; }
    public DateTime? ActualCheckOut { get; set; }
    public DateTime? CancelledAt { get; set; }

    [StringLength(250)]
    public string? CancellationReason { get; set; }

    [StringLength(100)]
    public string? CreatedBy { get; set; }

    public Invoice? Invoice { get; set; }

    [NotMapped]
    public int Nights => Math.Max(1, (CheckOutDate.Date - CheckInDate.Date).Days);

    [NotMapped]
    public bool NeedsNumber => BookingNumber.StartsWith(DocumentNumber.TempPrefix);

    public void AssignNumber() => BookingNumber = $"BK-{Id:D4}";
}
