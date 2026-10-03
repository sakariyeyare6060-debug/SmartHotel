using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace SmartHotel.ViewModels;

public class BookingListViewModel
{
    public PagedResult<Booking> Bookings { get; set; } = PagedResult<Booking>.Empty();
    public string? Search { get; set; }
    public DateTime? From { get; set; }
    public DateTime? To { get; set; }
    public BookingStatus? Status { get; set; }
}

public class BookingFormViewModel : IValidatableObject
{
    public int? Id { get; set; }
    public string? BookingNumber { get; set; }
    public bool IsCheckedIn { get; set; }

    [Required(ErrorMessage = "Select a guest."), Display(Name = "Guest")]
    public int? GuestId { get; set; }

    [Required(ErrorMessage = "Select a room."), Display(Name = "Room")]
    public int? RoomId { get; set; }

    [Required, DataType(DataType.Date), Display(Name = "Check-in date")]
    public DateTime CheckInDate { get; set; } = DateTime.Today;

    [Required, DataType(DataType.Date), Display(Name = "Check-out date")]
    public DateTime CheckOutDate { get; set; } = DateTime.Today.AddDays(1);

    [Range(1, 10)]
    public int Adults { get; set; } = 1;

    [Range(0, 10)]
    public int Children { get; set; }

    public BookingStatus Status { get; set; } = BookingStatus.Confirmed;

    [StringLength(500), Display(Name = "Special requests")]
    public string? SpecialRequests { get; set; }

    public bool IsEdit => Id.HasValue;
    public List<SelectListItem> Guests { get; set; } = [];
    public List<RoomOption> Rooms { get; set; } = [];

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (CheckOutDate.Date <= CheckInDate.Date)
            yield return new ValidationResult("Check-out must be after check-in.", [nameof(CheckOutDate)]);
        else if ((CheckOutDate.Date - CheckInDate.Date).TotalDays > 90)
            yield return new ValidationResult("A single booking cannot exceed 90 nights.", [nameof(CheckOutDate)]);
    }
}

public class WalkInViewModel : IValidatableObject
{
    [Required(ErrorMessage = "Select a guest."), Display(Name = "Guest")]
    public int? GuestId { get; set; }

    [Required(ErrorMessage = "Select a room."), Display(Name = "Room")]
    public int? RoomId { get; set; }

    [DataType(DataType.Date), Display(Name = "Check-out date")]
    public DateTime CheckOutDate { get; set; } = DateTime.Today.AddDays(1);

    [Range(1, 10)]
    public int Adults { get; set; } = 1;

    [Range(0, 10)]
    public int Children { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (CheckOutDate.Date <= DateTime.Today)
            yield return new ValidationResult("Check-out must be after today.", [nameof(CheckOutDate)]);
    }
}

public class FrontDeskViewModel
{
    public string Tab { get; set; } = "checkin";
    public WalkInViewModel WalkIn { get; set; } = new();
    public List<SelectListItem> Guests { get; set; } = [];
    public List<RoomOption> ReadyRooms { get; set; } = [];
    public List<Booking> Arrivals { get; set; } = [];
    public List<Booking> InHouse { get; set; } = [];
    public int DeparturesToday => InHouse.Count(b => b.CheckOutDate.Date <= DateTime.Today);
}

public class CheckOutViewModel
{
    public Booking Booking { get; set; } = default!;
    public Invoice Invoice { get; set; } = default!;
    public CheckOutFormModel Form { get; set; } = new();
}

public class CheckOutFormModel
{
    public int BookingId { get; set; }

    [Range(0, 1000000), Display(Name = "Payment amount")]
    public decimal PaymentAmount { get; set; }

    [Display(Name = "Payment method")]
    public PaymentMethod Method { get; set; } = PaymentMethod.Cash;

    [StringLength(100)]
    public string? Reference { get; set; }
}
