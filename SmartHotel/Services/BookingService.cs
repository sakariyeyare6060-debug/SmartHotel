using SmartHotel.Repositories;
using SmartHotel.ViewModels;

namespace SmartHotel.Services;

public interface IBookingService
{
    Task<PagedResult<Booking>> SearchAsync(string? search, DateTime? from, DateTime? to, BookingStatus? status, int page, int pageSize = 10);
    Task<Booking?> GetAsync(int id);
    Task<List<Booking>> GetArrivalsAsync(DateTime date);
    Task<List<Booking>> GetInHouseAsync();
    Task<ServiceResult<Booking>> CreateAsync(BookingFormViewModel model, string user);
    Task<ServiceResult> UpdateAsync(BookingFormViewModel model);
    Task<ServiceResult<string>> CancelAsync(int id, string? reason, string user);
    Task<ServiceResult> ConfirmAsync(int id);
    Task<ServiceResult> DeleteAsync(int id);
    Task<ServiceResult> CheckInAsync(int id, string user);
    Task<ServiceResult<Booking>> WalkInAsync(WalkInViewModel model, string user);
    Task<ServiceResult> CheckOutAsync(CheckOutFormModel model, string user);
}

public class BookingService(
    IBookingRepository bookings,
    IRepository<Guest> guests,
    IRepository<Room> rooms,
    IBillingService billing,
    IHousekeepingService housekeeping,
    INotificationService notifications) : IBookingService
{
    private const int MaxNights = 90;

    public Task<PagedResult<Booking>> SearchAsync(string? search, DateTime? from, DateTime? to, BookingStatus? status, int page, int pageSize = 10)
    {
        var q = bookings.QueryWithDetails().AsNoTracking();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim();
            q = q.Where(b => b.BookingNumber.Contains(s) ||
                             b.Guest!.FirstName.Contains(s) || b.Guest.LastName.Contains(s) ||
                             (b.Guest.FirstName + " " + b.Guest.LastName).Contains(s) ||
                             b.Guest.Phone.Contains(s) ||
                             b.Room!.RoomNumber.Contains(s));
        }
        if (from.HasValue) q = q.Where(b => b.CheckInDate >= from.Value.Date);
        if (to.HasValue) q = q.Where(b => b.CheckInDate <= to.Value.Date);
        if (status.HasValue) q = q.Where(b => b.Status == status.Value);

        return q.OrderByDescending(b => b.CheckInDate).ThenByDescending(b => b.Id).ToPagedAsync(page, pageSize);
    }

    public Task<Booking?> GetAsync(int id) =>
        bookings.QueryWithDetails().AsNoTracking()
            .Include(b => b.Invoice).ThenInclude(i => i!.Payments)
            .FirstOrDefaultAsync(b => b.Id == id);

    public Task<List<Booking>> GetArrivalsAsync(DateTime date)
    {
        var day = date.Date;
        return bookings.QueryWithDetails().AsNoTracking()
            .Where(b => (b.Status == BookingStatus.Pending || b.Status == BookingStatus.Confirmed) &&
                        b.CheckInDate <= day && b.CheckOutDate > day)
            .OrderBy(b => b.CheckInDate).ThenBy(b => b.Room!.RoomNumber)
            .ToListAsync();
    }

    public Task<List<Booking>> GetInHouseAsync() =>
        bookings.QueryWithDetails().AsNoTracking()
            .Where(b => b.Status == BookingStatus.CheckedIn)
            .OrderBy(b => b.CheckOutDate).ThenBy(b => b.Room!.RoomNumber)
            .ToListAsync();

    public async Task<ServiceResult<Booking>> CreateAsync(BookingFormViewModel model, string user)
    {
        var check = await ValidateAsync(model.GuestId ?? 0, model.RoomId ?? 0, model.CheckInDate, model.CheckOutDate,
            model.Adults, model.Children, excludeBookingId: null, allowPastCheckIn: false);
        if (check.Error is not null) return ServiceResult<Booking>.Fail(check.Error);

        var booking = NewBooking(check.Guest!, check.Room!, model.CheckInDate, model.CheckOutDate, model.Adults, model.Children,
            model.Status == BookingStatus.Pending ? BookingStatus.Pending : BookingStatus.Confirmed, model.SpecialRequests, user);
        await bookings.AddAsync(booking);
        await bookings.SaveChangesAsync();

        await notifications.CreateAsync(NotificationType.Booking, "New booking",
            $"New booking {booking.BookingNumber} received from {check.Guest!.FullName} - Room {check.Room!.RoomNumber}",
            $"/Bookings/Details/{booking.Id}");
        return ServiceResult<Booking>.Ok(booking);
    }

    public async Task<ServiceResult> UpdateAsync(BookingFormViewModel model)
    {
        var booking = await bookings.QueryWithDetails().FirstOrDefaultAsync(b => b.Id == model.Id);
        if (booking is null) return ServiceResult.Fail("Booking not found.");
        if (booking.Status is BookingStatus.Cancelled or BookingStatus.CheckedOut)
            return ServiceResult.Fail($"Booking {booking.BookingNumber} is {booking.Status.DisplayName()} and can no longer be modified.");

        var inHouse = booking.Status == BookingStatus.CheckedIn;
        if (inHouse && model.CheckOutDate.Date < DateTime.Today)
            return ServiceResult.Fail("Check-out date for an in-house guest cannot be in the past.");

        // In-house guests keep their guest, room and arrival date; only the stay length/party can change.
        var guestId = inHouse ? booking.GuestId : model.GuestId ?? 0;
        var roomId = inHouse ? booking.RoomId : model.RoomId ?? 0;
        var checkIn = inHouse ? booking.CheckInDate : model.CheckInDate.Date;
        var allowPast = inHouse || checkIn == booking.CheckInDate.Date;

        var check = await ValidateAsync(guestId, roomId, checkIn, model.CheckOutDate, model.Adults, model.Children,
            booking.Id, allowPast, requireNotMaintenance: !inHouse);
        if (check.Error is not null) return ServiceResult.Fail(check.Error);

        if (booking.RoomId != roomId)
        {
            booking.Room = check.Room;
            booking.RoomId = roomId;
            booking.PricePerNight = check.Room!.PricePerNight;
        }
        booking.Guest = check.Guest;
        booking.GuestId = guestId;
        booking.CheckInDate = checkIn;
        booking.CheckOutDate = model.CheckOutDate.Date;
        booking.Adults = model.Adults;
        booking.Children = model.Children;
        booking.SpecialRequests = model.SpecialRequests?.Trim();
        if (!inHouse && model.Status is BookingStatus.Pending or BookingStatus.Confirmed)
            booking.Status = model.Status;

        booking.TotalAmount = booking.PricePerNight * booking.Nights;
        if (booking.Invoice is { Status: not InvoiceStatus.Void } invoice)
            BillingCalculator.Recalculate(invoice, booking);

        await bookings.SaveChangesAsync();
        return ServiceResult.Ok();
    }

    public async Task<ServiceResult<string>> CancelAsync(int id, string? reason, string user)
    {
        var booking = await bookings.QueryWithDetails().FirstOrDefaultAsync(b => b.Id == id);
        if (booking is null) return ServiceResult<string>.Fail("Booking not found.");
        if (booking.Status is not (BookingStatus.Pending or BookingStatus.Confirmed))
            return ServiceResult<string>.Fail($"Only pending or confirmed bookings can be cancelled (current: {booking.Status.DisplayName()}).");

        booking.Status = BookingStatus.Cancelled;
        booking.CancelledAt = DateTime.Now;
        booking.CancellationReason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim()[..Math.Min(reason.Trim().Length, 250)];

        var message = $"Booking {booking.BookingNumber} was cancelled.";
        if (booking.Invoice is { Status: not InvoiceStatus.Void } invoice)
        {
            if (invoice.AmountPaid > 0)
                message += $" {Ui.Money(invoice.AmountPaid)} was already paid on {invoice.InvoiceNumber} — process a refund if applicable.";
            else
                invoice.Status = InvoiceStatus.Void;
        }

        notifications.Add(NotificationType.Booking, "Booking cancelled",
            $"Booking {booking.BookingNumber} for {booking.Guest!.FullName} was cancelled by {user}",
            $"/Bookings/Details/{booking.Id}");
        await bookings.SaveChangesAsync();
        return ServiceResult<string>.Ok(message);
    }

    public async Task<ServiceResult> ConfirmAsync(int id)
    {
        var booking = await bookings.GetByIdAsync(id);
        if (booking is null) return ServiceResult.Fail("Booking not found.");
        if (booking.Status != BookingStatus.Pending) return ServiceResult.Fail("Only pending bookings can be confirmed.");

        booking.Status = BookingStatus.Confirmed;
        await bookings.SaveChangesAsync();
        return ServiceResult.Ok();
    }

    public async Task<ServiceResult> DeleteAsync(int id)
    {
        if (!await bookings.QueryNoTracking().AnyAsync(b => b.Id == id)) return ServiceResult.Fail("Booking not found.");

        await bookings.RemoveWithBillingAsync(b => b.Id == id);
        await bookings.SaveChangesAsync();
        return ServiceResult.Ok();
    }

    public async Task<ServiceResult> CheckInAsync(int id, string user)
    {
        var booking = await bookings.QueryWithDetails().FirstOrDefaultAsync(b => b.Id == id);
        if (booking is null) return ServiceResult.Fail("Booking not found.");
        if (booking.Status is not (BookingStatus.Pending or BookingStatus.Confirmed))
            return ServiceResult.Fail($"Booking {booking.BookingNumber} cannot be checked in (status: {booking.Status.DisplayName()}).");

        var today = DateTime.Today;
        if (booking.CheckInDate.Date > today)
            return ServiceResult.Fail($"Booking {booking.BookingNumber} starts on {Ui.Date(booking.CheckInDate)}. Edit the dates for an early arrival.");
        if (booking.CheckOutDate.Date <= today)
            return ServiceResult.Fail($"The stay for booking {booking.BookingNumber} has already ended.");

        var room = booking.Room!;
        if (!room.IsReady)
            return ServiceResult.Fail($"Room {room.RoomNumber} is {room.Status.DisplayName()} and not ready for a guest.");

        booking.Status = BookingStatus.CheckedIn;
        booking.ActualCheckIn = DateTime.Now;
        housekeeping.TrackStatusChange(room, RoomStatus.Occupied, $"Guest checked in ({booking.BookingNumber})", user);
        billing.EnsureInvoice(booking);

        notifications.Add(NotificationType.CheckIn, "Guest checked in",
            $"{booking.Guest!.FullName} checked in - Room {room.RoomNumber}", $"/Bookings/Details/{booking.Id}");
        await bookings.SaveChangesAsync();
        return ServiceResult.Ok();
    }

    public async Task<ServiceResult<Booking>> WalkInAsync(WalkInViewModel model, string user)
    {
        var today = DateTime.Today;
        var check = await ValidateAsync(model.GuestId ?? 0, model.RoomId ?? 0, today, model.CheckOutDate,
            model.Adults, model.Children, excludeBookingId: null, allowPastCheckIn: false);
        if (check.Error is not null) return ServiceResult<Booking>.Fail(check.Error);

        var room = check.Room!;
        if (!room.IsReady)
            return ServiceResult<Booking>.Fail($"Room {room.RoomNumber} is {room.Status.DisplayName()} and not ready for a guest.");

        var booking = NewBooking(check.Guest!, room, today, model.CheckOutDate, model.Adults, model.Children,
            BookingStatus.CheckedIn, "Walk-in", user);
        booking.ActualCheckIn = DateTime.Now;
        await bookings.AddAsync(booking);

        housekeeping.TrackStatusChange(room, RoomStatus.Occupied, "Walk-in check-in", user);
        billing.EnsureInvoice(booking);
        await bookings.SaveChangesAsync();

        await notifications.CreateAsync(NotificationType.CheckIn, "Walk-in guest checked in",
            $"{check.Guest!.FullName} checked in - Room {room.RoomNumber} ({booking.BookingNumber})",
            $"/Bookings/Details/{booking.Id}");
        return ServiceResult<Booking>.Ok(booking);
    }

    public async Task<ServiceResult> CheckOutAsync(CheckOutFormModel model, string user)
    {
        var booking = await bookings.QueryWithDetails().FirstOrDefaultAsync(b => b.Id == model.BookingId);
        if (booking is null) return ServiceResult.Fail("Booking not found.");
        if (booking.Status != BookingStatus.CheckedIn)
            return ServiceResult.Fail($"Booking {booking.BookingNumber} is not checked in.");

        var invoice = billing.EnsureInvoice(booking);
        var payment = Math.Round(model.PaymentAmount, 2);
        if (payment < 0) return ServiceResult.Fail("Payment amount cannot be negative.");
        if (payment > invoice.Balance + BillingCalculator.Tolerance)
            return ServiceResult.Fail($"Payment exceeds the outstanding balance of {Ui.Money(invoice.Balance)}.");
        if (invoice.Balance - payment > BillingCalculator.Tolerance)
            return ServiceResult.Fail($"An outstanding balance of {Ui.Money(invoice.Balance - payment)} must be settled before check-out.");

        if (payment > 0) billing.ApplyPayment(invoice, payment, model.Method, model.Reference, user);

        var room = booking.Room!;
        booking.Status = BookingStatus.CheckedOut;
        booking.ActualCheckOut = DateTime.Now;
        housekeeping.TrackStatusChange(room, RoomStatus.Dirty, $"Guest checked out ({booking.BookingNumber})", user);

        notifications.Add(NotificationType.CheckOut, "Check-out completed",
            $"Check-out completed - Room {room.RoomNumber} ({booking.Guest!.FullName})", $"/Bookings/Details/{booking.Id}");
        notifications.Add(NotificationType.Housekeeping, "Room needs cleaning",
            $"Room {room.RoomNumber} marked as Dirty after check-out", "/Housekeeping?status=Dirty");
        await bookings.SaveChangesAsync();
        return ServiceResult.Ok();
    }

    private sealed record BookingCheck(string? Error, Guest? Guest = null, Room? Room = null);

    private async Task<BookingCheck> ValidateAsync(int guestId, int roomId, DateTime checkIn, DateTime checkOut,
        int adults, int children, int? excludeBookingId, bool allowPastCheckIn, bool requireNotMaintenance = true)
    {
        checkIn = checkIn.Date;
        checkOut = checkOut.Date;

        if (checkOut <= checkIn) return new("Check-out date must be after the check-in date.");
        if ((checkOut - checkIn).TotalDays > MaxNights) return new($"A single booking cannot exceed {MaxNights} nights.");
        if (!allowPastCheckIn && checkIn < DateTime.Today) return new("Check-in date cannot be in the past.");
        if (adults < 1) return new("At least one adult is required.");

        var guest = await guests.GetByIdAsync(guestId);
        if (guest is null) return new("Selected guest does not exist.");

        var room = await rooms.Query().Include(r => r.RoomType).FirstOrDefaultAsync(r => r.Id == roomId);
        if (room is null) return new("Selected room does not exist.");
        if (requireNotMaintenance && room.Status == RoomStatus.Maintenance)
            return new($"Room {room.RoomNumber} is under maintenance.");

        var capacity = room.RoomType?.Capacity ?? 1;
        if (adults + children > capacity)
            return new($"Room {room.RoomNumber} ({room.RoomType?.Name}) holds at most {capacity} guest(s).");

        if (await bookings.HasOverlapAsync(roomId, checkIn, checkOut, excludeBookingId))
            return new($"Room {room.RoomNumber} is already booked for some of the selected nights.");

        return new(null, guest, room);
    }

    private static Booking NewBooking(Guest guest, Room room, DateTime checkIn, DateTime checkOut, int adults, int children,
        BookingStatus status, string? requests, string user)
    {
        var booking = new Booking
        {
            Guest = guest,
            GuestId = guest.Id,
            Room = room,
            RoomId = room.Id,
            CheckInDate = checkIn.Date,
            CheckOutDate = checkOut.Date,
            Adults = adults,
            Children = children,
            Status = status,
            PricePerNight = room.PricePerNight,
            SpecialRequests = string.IsNullOrWhiteSpace(requests) ? null : requests.Trim(),
            CreatedBy = user
        };
        booking.TotalAmount = booking.PricePerNight * booking.Nights;
        return booking;
    }
}
