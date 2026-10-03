using System.ComponentModel.DataAnnotations;

namespace SmartHotel.Models;

/// <summary>An employee. Every staff member also owns a login account.</summary>
public class Staff : BaseEntity
{
    [Required, StringLength(100)]
    public string FullName { get; set; } = string.Empty;

    [Required, StringLength(150)]
    public string Email { get; set; } = string.Empty;

    [StringLength(30)]
    public string? Phone { get; set; }

    [Required, StringLength(50)]
    public string Username { get; set; } = string.Empty;

    [Required]
    public string PasswordHash { get; set; } = string.Empty;

    public UserRole Role { get; set; } = UserRole.Receptionist;

    public bool IsActive { get; set; } = true;

    public DateTime HireDate { get; set; } = DateTime.Today;

    public DateTime? LastLoginAt { get; set; }

    public int FailedLoginAttempts { get; set; }

    public DateTime? LockoutEnd { get; set; }

    /// <summary>Changes whenever credentials, role or status change; invalidates existing sessions.</summary>
    [Required, StringLength(64)]
    public string SecurityStamp { get; set; } = Guid.NewGuid().ToString("N");
}
