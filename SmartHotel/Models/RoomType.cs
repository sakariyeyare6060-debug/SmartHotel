using System.ComponentModel.DataAnnotations;

namespace SmartHotel.Models;

public class RoomType : BaseEntity
{
    [Required, StringLength(50)]
    public string Name { get; set; } = string.Empty;

    [StringLength(250)]
    public string? Description { get; set; }

    [StringLength(250)]
    public string? Amenities { get; set; }

    public decimal BasePrice { get; set; }

    public int Capacity { get; set; } = 1;

    public ICollection<Room> Rooms { get; set; } = new List<Room>();
}
