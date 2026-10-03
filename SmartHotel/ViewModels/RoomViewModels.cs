using System.ComponentModel.DataAnnotations;

namespace SmartHotel.ViewModels;

public record RoomOption(int Id, string RoomNumber, string TypeName, decimal Price, int Capacity, RoomStatus Status)
{
    public string Label => $"Room {RoomNumber} · {TypeName} · {Ui.Money(Price)}/night";
}

public class RoomListViewModel
{
    public PagedResult<Room> Rooms { get; set; } = PagedResult<Room>.Empty();
    public string? Search { get; set; }
    public int? RoomTypeId { get; set; }
    public RoomStatus? Status { get; set; }
    /// <summary>Available + Clean rooms (the dashboard's "Available Rooms" figure).</summary>
    public bool Ready { get; set; }
    public List<RoomType> RoomTypes { get; set; } = [];
}

public class RoomFormViewModel
{
    public int? Id { get; set; }

    [Required, StringLength(10), Display(Name = "Room number")]
    [RegularExpression(@"^[A-Za-z0-9\-]+$", ErrorMessage = "Use letters, digits or '-' only.")]
    public string RoomNumber { get; set; } = string.Empty;

    [Range(0, 200)]
    public int Floor { get; set; } = 1;

    [Required(ErrorMessage = "Select a room type."), Display(Name = "Room type")]
    public int? RoomTypeId { get; set; }

    [Range(1, 100000, ErrorMessage = "Price must be between 1 and 100,000."), Display(Name = "Price per night")]
    public decimal PricePerNight { get; set; }

    [Display(Name = "Initial status")]
    public RoomStatus Status { get; set; } = RoomStatus.Available;

    [StringLength(500)]
    public string? Description { get; set; }

    public bool IsEdit => Id.HasValue;
    public List<RoomType> RoomTypes { get; set; } = [];
}

public class RoomTypeFormViewModel
{
    public int? Id { get; set; }

    [Required, StringLength(50)]
    public string Name { get; set; } = string.Empty;

    [StringLength(250)]
    public string? Description { get; set; }

    [StringLength(250)]
    public string? Amenities { get; set; }

    [Range(1, 100000), Display(Name = "Base price")]
    public decimal BasePrice { get; set; }

    [Range(1, 20), Display(Name = "Max guests")]
    public int Capacity { get; set; } = 1;
}

public class RoomTypesViewModel
{
    public List<RoomType> Types { get; set; } = [];
    public Dictionary<int, int> RoomCounts { get; set; } = [];
    public RoomTypeFormViewModel Form { get; set; } = new();
}
