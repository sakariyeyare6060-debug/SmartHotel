using System.ComponentModel.DataAnnotations;

namespace SmartHotel.ViewModels;

/// <summary>A room under maintenance, with the issue noted when it was taken out of service.</summary>
public record MaintenanceRoom(Room Room, string? ReportedIssue, string? ReportedBy, DateTime? ReportedAt);

public class MaintenanceViewModel
{
    public List<MaintenanceRoom> Rooms { get; set; } = [];
    public List<MaintenanceRecord> RecentRepairs { get; set; } = [];
    public int RepairsThisMonth { get; set; }
    public decimal CostThisMonth { get; set; }
    public RepairFormViewModel Form { get; set; } = new();
}

public class RepairFormViewModel
{
    [Required]
    public int RoomId { get; set; }

    [Required(ErrorMessage = "Choose the type of fault."), Display(Name = "Fault type")]
    public MaintenanceFaultType? FaultType { get; set; }

    [Required(ErrorMessage = "Describe the fault."), StringLength(500), Display(Name = "Fault description")]
    public string FaultDescription { get; set; } = string.Empty;

    [StringLength(500), Display(Name = "Work done")]
    public string? WorkDone { get; set; }

    [Range(0, 1_000_000, ErrorMessage = "Cost must be between 0 and 1,000,000."), Display(Name = "Repair cost")]
    public decimal Cost { get; set; }
}
