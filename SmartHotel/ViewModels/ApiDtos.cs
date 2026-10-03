using System.ComponentModel.DataAnnotations;

namespace SmartHotel.ViewModels;

public record RoomDto(int Id, string RoomNumber, int Floor, string RoomType, int Capacity, decimal PricePerNight, RoomStatus Status)
{
    public static RoomDto From(Room r) =>
        new(r.Id, r.RoomNumber, r.Floor, r.RoomType?.Name ?? "", r.RoomType?.Capacity ?? 1, r.PricePerNight, r.Status);
}

public record BookingDto(
    int Id, string BookingNumber, int GuestId, string GuestName, int RoomId, string RoomNumber,
    DateTime CheckInDate, DateTime CheckOutDate, int Nights, int Adults, int Children,
    BookingStatus Status, decimal TotalAmount, string? InvoiceNumber, decimal? Balance)
{
    public static BookingDto From(Booking b) => new(
        b.Id, b.BookingNumber, b.GuestId, b.Guest?.FullName ?? "", b.RoomId, b.Room?.RoomNumber ?? "",
        b.CheckInDate, b.CheckOutDate, b.Nights, b.Adults, b.Children, b.Status, b.TotalAmount,
        b.Invoice?.InvoiceNumber, b.Invoice?.Balance);
}

public record PagedResponse<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount, int TotalPages);

public class CreateBookingRequest
{
    [Required] public int GuestId { get; set; }
    [Required] public int RoomId { get; set; }
    [Required] public DateTime CheckInDate { get; set; }
    [Required] public DateTime CheckOutDate { get; set; }
    [Range(1, 10)] public int Adults { get; set; } = 1;
    [Range(0, 10)] public int Children { get; set; }
    public bool Confirmed { get; set; } = true;
    [StringLength(500)] public string? SpecialRequests { get; set; }
}

public class CancelBookingRequest
{
    [StringLength(250)] public string? Reason { get; set; }
}

public class UpdateRoomStatusRequest
{
    [Required] public RoomStatus Status { get; set; }
    [StringLength(250)] public string? Note { get; set; }
}
