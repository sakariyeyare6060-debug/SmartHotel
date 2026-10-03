using System.ComponentModel.DataAnnotations;

namespace SmartHotel.ViewModels;

public class GuestListViewModel
{
    public PagedResult<Guest> Guests { get; set; } = PagedResult<Guest>.Empty();
    public string? Search { get; set; }
}

public class GuestFormViewModel
{
    public int? Id { get; set; }

    [Required, StringLength(50), Display(Name = "First name")]
    public string FirstName { get; set; } = string.Empty;

    [Required, StringLength(50), Display(Name = "Last name")]
    public string LastName { get; set; } = string.Empty;

    [EmailAddress, StringLength(150)]
    public string? Email { get; set; }

    [Required, Phone, StringLength(30)]
    public string Phone { get; set; } = string.Empty;

    [StringLength(30), Display(Name = "ID type")]
    public string? IdType { get; set; } = "National ID";

    [StringLength(50), Display(Name = "ID number")]
    public string? IdNumber { get; set; }

    [StringLength(60)]
    public string? Nationality { get; set; }

    [StringLength(250)]
    public string? Address { get; set; }

    [DataType(DataType.Date), Display(Name = "Date of birth")]
    public DateTime? DateOfBirth { get; set; }

    [StringLength(500)]
    public string? Notes { get; set; }

    public bool IsEdit => Id.HasValue;

    public static readonly string[] IdTypes = ["National ID", "Passport", "Driver License", "Residence Permit", "Other"];
}

public class GuestDetailsViewModel
{
    public Guest Guest { get; set; } = default!;
    public List<Booking> Bookings { get; set; } = [];
    public int TotalStays => Bookings.Count(b => b.Status == BookingStatus.CheckedOut);
    public int TotalNights => Bookings.Where(b => b.Status is BookingStatus.CheckedOut or BookingStatus.CheckedIn).Sum(b => b.Nights);
    public decimal TotalSpent => Bookings.Where(b => b.Invoice != null).Sum(b => b.Invoice!.AmountPaid);
}
