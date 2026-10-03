using System.ComponentModel.DataAnnotations;

namespace SmartHotel.ViewModels;

public class HousekeepingViewModel
{
    public Dictionary<RoomStatus, int> Counts { get; set; } = [];
    public List<Room> Rooms { get; set; } = [];
    public List<HousekeepingLog> RecentLogs { get; set; } = [];
    public RoomStatus? Status { get; set; }
    public int? Floor { get; set; }
    public List<int> Floors { get; set; } = [];
}

public class RoomStatusUpdateViewModel
{
    [Required]
    public int RoomId { get; set; }

    [Required]
    public RoomStatus Status { get; set; }

    [StringLength(250)]
    public string? Note { get; set; }
}

public class StaffListViewModel
{
    public PagedResult<Staff> Staff { get; set; } = PagedResult<Staff>.Empty();
    public string? Search { get; set; }
    public UserRole? Role { get; set; }
}

public class StaffFormViewModel : IValidatableObject
{
    public int? Id { get; set; }

    [Required, StringLength(100), Display(Name = "Full name")]
    public string FullName { get; set; } = string.Empty;

    [Required, EmailAddress, StringLength(150)]
    public string Email { get; set; } = string.Empty;

    [Phone, StringLength(30)]
    public string? Phone { get; set; }

    [Required, StringLength(50, MinimumLength = 3)]
    [RegularExpression(@"^[a-zA-Z0-9._-]+$", ErrorMessage = "Username may contain letters, digits, '.', '_' and '-'.")]
    public string Username { get; set; } = string.Empty;

    public UserRole Role { get; set; } = UserRole.Receptionist;

    [Display(Name = "Account active")]
    public bool IsActive { get; set; } = true;

    [DataType(DataType.Date), Display(Name = "Hire date")]
    public DateTime HireDate { get; set; } = DateTime.Today;

    [DataType(DataType.Password)]
    [RegularExpression(PasswordRules.Pattern, ErrorMessage = PasswordRules.Message)]
    public string? Password { get; set; }

    [DataType(DataType.Password), Display(Name = "Confirm password")]
    [Compare(nameof(Password), ErrorMessage = "Passwords do not match.")]
    public string? ConfirmPassword { get; set; }

    public bool IsEdit => Id.HasValue;

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (!IsEdit && string.IsNullOrWhiteSpace(Password))
            yield return new ValidationResult("A password is required for new accounts.", [nameof(Password)]);
    }
}

public class ResetPasswordViewModel
{
    public int Id { get; set; }
    public string? FullName { get; set; }

    [Required, DataType(DataType.Password), Display(Name = "New password")]
    [RegularExpression(PasswordRules.Pattern, ErrorMessage = PasswordRules.Message)]
    public string NewPassword { get; set; } = string.Empty;

    [Required, DataType(DataType.Password), Display(Name = "Confirm password")]
    [Compare(nameof(NewPassword), ErrorMessage = "Passwords do not match.")]
    public string ConfirmPassword { get; set; } = string.Empty;
}

public class NotificationListViewModel
{
    public PagedResult<Notification> Notifications { get; set; } = PagedResult<Notification>.Empty();
    public bool UnreadOnly { get; set; }
    public int UnreadCount { get; set; }
}
