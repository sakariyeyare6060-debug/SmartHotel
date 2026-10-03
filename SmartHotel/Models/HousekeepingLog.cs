using System.ComponentModel.DataAnnotations;

namespace SmartHotel.Models;

/// <summary>Audit trail of room status changes.</summary>
public class HousekeepingLog : BaseEntity
{
    public int RoomId { get; set; }
    public Room? Room { get; set; }

    public RoomStatus FromStatus { get; set; }
    public RoomStatus ToStatus { get; set; }

    [StringLength(250)]
    public string? Note { get; set; }

    [StringLength(100)]
    public string? ChangedBy { get; set; }
}
