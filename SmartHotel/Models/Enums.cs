using System.ComponentModel.DataAnnotations;

namespace SmartHotel.Models;

public enum RoomStatus
{
    Available = 0,
    Occupied = 1,
    Dirty = 2,
    Clean = 3,
    Maintenance = 4
}

public enum BookingStatus
{
    Pending = 0,
    Confirmed = 1,
    [Display(Name = "Checked In")] CheckedIn = 2,
    [Display(Name = "Checked Out")] CheckedOut = 3,
    Cancelled = 4
}

public enum InvoiceStatus
{
    Unpaid = 0,
    [Display(Name = "Partially Paid")] PartiallyPaid = 1,
    Paid = 2,
    Void = 3
}

public enum PaymentMethod
{
    Cash = 0,
    Card = 1,
    [Display(Name = "Mobile Money")] MobileMoney = 2,
    [Display(Name = "Bank Transfer")] BankTransfer = 3
}

public enum UserRole
{
    Admin = 0,
    Manager = 1,
    Receptionist = 2,
    Housekeeping = 3,
    Maintenance = 4
}

public enum MaintenanceFaultType
{
    Plumbing = 0,
    Electrical = 1,
    [Display(Name = "Air Conditioning")] AirConditioning = 2,
    Furniture = 3,
    Appliance = 4,
    [Display(Name = "Doors & Locks")] DoorsAndLocks = 5,
    [Display(Name = "Walls & Painting")] WallsAndPainting = 6,
    Other = 7
}

public enum NotificationType
{
    Booking = 0,
    Payment = 1,
    [Display(Name = "Check-in")] CheckIn = 2,
    [Display(Name = "Check-out")] CheckOut = 3,
    Maintenance = 4,
    Housekeeping = 5,
    System = 6
}
