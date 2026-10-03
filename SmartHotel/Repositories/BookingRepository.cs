using System.Linq.Expressions;
using SmartHotel.Data;

namespace SmartHotel.Repositories;

public interface IBookingRepository : IRepository<Booking>
{
    /// <summary>Bookings with guest, room (and type) and invoice loaded.</summary>
    IQueryable<Booking> QueryWithDetails();

    /// <summary>True when another active booking holds the room for any night in [checkIn, checkOut).</summary>
    Task<bool> HasOverlapAsync(int roomId, DateTime checkIn, DateTime checkOut, int? excludeBookingId = null);

    /// <summary>
    /// Removes the matching bookings together with their invoices and payments (no save).
    /// A room still occupied by one of them is left Dirty for housekeeping.
    /// </summary>
    Task RemoveWithBillingAsync(Expression<Func<Booking, bool>> predicate);
}

public class BookingRepository(AppDbContext db) : Repository<Booking>(db), IBookingRepository
{
    public IQueryable<Booking> QueryWithDetails() =>
        Set.Include(b => b.Guest)
           .Include(b => b.Room).ThenInclude(r => r!.RoomType)
           .Include(b => b.Invoice);

    public Task<bool> HasOverlapAsync(int roomId, DateTime checkIn, DateTime checkOut, int? excludeBookingId = null)
    {
        var from = checkIn.Date;
        var to = checkOut.Date;
        return Set.AnyAsync(b =>
            b.RoomId == roomId &&
            (excludeBookingId == null || b.Id != excludeBookingId) &&
            b.Status != BookingStatus.Cancelled &&
            b.Status != BookingStatus.CheckedOut &&
            b.CheckInDate < to && from < b.CheckOutDate);
    }

    public async Task RemoveWithBillingAsync(Expression<Func<Booking, bool>> predicate)
    {
        var list = await Set.Include(b => b.Room)
            .Include(b => b.Invoice).ThenInclude(i => i!.Payments)
            .Where(predicate).ToListAsync();

        foreach (var b in list)
        {
            if (b.Status == BookingStatus.CheckedIn && b.Room is { Status: RoomStatus.Occupied } room)
                room.Status = RoomStatus.Dirty;
            if (b.Invoice is { } invoice)
            {
                Db.Payments.RemoveRange(invoice.Payments);
                Db.Invoices.Remove(invoice);
            }
            Set.Remove(b);
        }
    }
}
