using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SmartHotel.Models;

public class Guest : BaseEntity
{
    [Required, StringLength(50)]
    public string FirstName { get; set; } = string.Empty;

    [Required, StringLength(50)]
    public string LastName { get; set; } = string.Empty;

    [StringLength(150)]
    public string? Email { get; set; }

    [Required, StringLength(30)]
    public string Phone { get; set; } = string.Empty;

    [StringLength(30)]
    public string? IdType { get; set; }

    [StringLength(50)]
    public string? IdNumber { get; set; }

    [StringLength(60)]
    public string? Nationality { get; set; }

    [StringLength(250)]
    public string? Address { get; set; }

    public DateTime? DateOfBirth { get; set; }

    [StringLength(500)]
    public string? Notes { get; set; }

    public ICollection<Booking> Bookings { get; set; } = new List<Booking>();

    [NotMapped]
    public string FullName => $"{FirstName} {LastName}".Trim();

    [NotMapped]
    public string Code => $"G-{Id:D3}";
}
