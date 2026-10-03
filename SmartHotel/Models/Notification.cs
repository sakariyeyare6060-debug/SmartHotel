using System.ComponentModel.DataAnnotations;

namespace SmartHotel.Models;

public class Notification : BaseEntity
{
    [Required, StringLength(150)]
    public string Title { get; set; } = string.Empty;

    [Required, StringLength(500)]
    public string Message { get; set; } = string.Empty;

    public NotificationType Type { get; set; }

    public bool IsRead { get; set; }

    [StringLength(250)]
    public string? Link { get; set; }
}
