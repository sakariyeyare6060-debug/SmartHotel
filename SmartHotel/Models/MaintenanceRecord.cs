using System.ComponentModel.DataAnnotations;

namespace SmartHotel.Models;

/// <summary>A repair carried out by maintenance staff on a room that was under maintenance.</summary>
public class MaintenanceRecord : BaseEntity
{
    public int RoomId { get; set; }
    public Room? Room { get; set; }

    public MaintenanceFaultType FaultType { get; set; }

    [Required, StringLength(500)]
    public string FaultDescription { get; set; } = string.Empty;

    [StringLength(500)]
    public string? WorkDone { get; set; }

    public decimal Cost { get; set; }

    /// <summary>The note left by whoever put the room under maintenance.</summary>
    [StringLength(250)]
    public string? ReportedIssue { get; set; }

    public DateTime? ReportedAt { get; set; }

    [StringLength(100)]
    public string ResolvedBy { get; set; } = string.Empty;
}
