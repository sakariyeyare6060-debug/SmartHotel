using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SmartHotel.Models;

public class Room : BaseEntity
{
    [Required, StringLength(10)]
    public string RoomNumber { get; set; } = string.Empty;

    public int Floor { get; set; }

    public int RoomTypeId { get; set; }
    public RoomType? RoomType { get; set; }

    public decimal PricePerNight { get; set; }

    public RoomStatus Status { get; set; } = RoomStatus.Available;

    [StringLength(500)]
    public string? Description { get; set; }

    public ICollection<Booking> Bookings { get; set; } = new List<Booking>();
    public ICollection<HousekeepingLog> HousekeepingLogs { get; set; } = new List<HousekeepingLog>();

    /// <summary>Room can receive a guest right now.</summary>
    [NotMapped]
    public bool IsReady => Status is RoomStatus.Available or RoomStatus.Clean;
}
