using SmartHotel.Repositories;
using SmartHotel.ViewModels;

namespace SmartHotel.Services;

public interface IDashboardService
{
    Task<DashboardViewModel> GetAsync();
}

public class DashboardService(
    IHousekeepingService housekeeping,
    IBookingRepository bookings,
    IRepository<Guest> guests,
    IRepository<Payment> payments,
    IRepository<Invoice> invoices) : IDashboardService
{
    public async Task<DashboardViewModel> GetAsync()
    {
        var today = DateTime.Today;
        var monthStart = new DateTime(today.Year, today.Month, 1);
        var chartStart = monthStart.AddMonths(-11);

        var counts = await housekeeping.GetStatusCountsAsync();
        var vm = new DashboardViewModel
        {
            RoomStatusCounts = counts,
            TotalRooms = counts.Values.Sum(),
            OccupiedRooms = counts[RoomStatus.Occupied],
            AvailableRooms = counts[RoomStatus.Available] + counts[RoomStatus.Clean],
            TotalGuests = await guests.QueryNoTracking().CountAsync(),
            InHouseGuests = await bookings.QueryNoTracking()
                .Where(b => b.Status == BookingStatus.CheckedIn).SumAsync(b => b.Adults + b.Children),
            MonthRevenue = await payments.QueryNoTracking()
                .Where(p => p.PaidAt >= monthStart).SumAsync(p => (decimal?)p.Amount) ?? 0,
            ArrivalsToday = await bookings.QueryNoTracking().CountAsync(b =>
                (b.Status == BookingStatus.Pending || b.Status == BookingStatus.Confirmed) && b.CheckInDate == today),
            DeparturesToday = await bookings.QueryNoTracking().CountAsync(b =>
                b.Status == BookingStatus.CheckedIn && b.CheckOutDate <= today),
            PendingBookings = await bookings.QueryNoTracking().CountAsync(b => b.Status == BookingStatus.Pending),
            RecentBookings = await bookings.QueryWithDetails().AsNoTracking()
                .OrderByDescending(b => b.CreatedAt).Take(6).ToListAsync()
        };

        var outstanding = invoices.QueryNoTracking().Where(i => i.Status == InvoiceStatus.Unpaid || i.Status == InvoiceStatus.PartiallyPaid);
        vm.OutstandingBalance = (await outstanding.SumAsync(i => (decimal?)i.Total) ?? 0) -
                                (await outstanding.SumAsync(i => (decimal?)i.AmountPaid) ?? 0);

        var chartPayments = await payments.QueryNoTracking()
            .Where(p => p.PaidAt >= chartStart)
            .Select(p => new { p.PaidAt, p.Amount })
            .ToListAsync();

        for (var m = chartStart; m <= monthStart; m = m.AddMonths(1))
        {
            vm.RevenueLabels.Add(m.ToString("MMM yy"));
            vm.RevenueMonths.Add(m.ToString("yyyy-MM"));
            vm.RevenueValues.Add(chartPayments.Where(p => p.PaidAt.Year == m.Year && p.PaidAt.Month == m.Month).Sum(p => p.Amount));
        }

        return vm;
    }
}
