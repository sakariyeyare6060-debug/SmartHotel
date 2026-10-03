using System.Globalization;
using System.Text;
using SmartHotel.Repositories;
using SmartHotel.ViewModels;

namespace SmartHotel.Services;

public interface IReportService
{
    Task<ReportViewModel> GenerateAsync(ReportFilter filter);
    Task<(string FileName, byte[] Content)> ExportCsvAsync(ReportFilter filter, string kind);
}

public class ReportService(
    IBookingRepository bookings,
    IRepository<Room> rooms,
    IRepository<Guest> guests,
    IRepository<Payment> payments) : IReportService
{
    public async Task<ReportViewModel> GenerateAsync(ReportFilter filter)
    {
        var (from, to, label) = ResolveRange(filter);
        var days = (to - from).Days;
        var vm = new ReportViewModel { Filter = filter, From = from, To = to.AddDays(-1), PeriodLabel = label };

        // Occupancy & room revenue: room-nights that fall inside the period.
        var roomCount = await rooms.QueryNoTracking().CountAsync();
        var stays = await bookings.QueryNoTracking()
            .Where(b => b.Status != BookingStatus.Cancelled && b.CheckInDate < to && b.CheckOutDate > from)
            .Select(b => new { b.CheckInDate, b.CheckOutDate, b.PricePerNight, TypeName = b.Room!.RoomType!.Name })
            .ToListAsync();

        var nightsByStay = stays.Select(s => new
        {
            s.TypeName,
            s.PricePerNight,
            Nights = Math.Max(0, ((s.CheckOutDate < to ? s.CheckOutDate : to) - (s.CheckInDate > from ? s.CheckInDate : from)).Days)
        }).ToList();

        vm.RoomNightsSold = nightsByStay.Sum(s => s.Nights);
        vm.RoomRevenue = nightsByStay.Sum(s => s.Nights * s.PricePerNight);
        vm.OccupancyRate = roomCount * days == 0 ? 0 : Math.Round(vm.RoomNightsSold * 100.0 / (roomCount * days), 1);
        vm.AverageDailyRate = vm.RoomNightsSold == 0 ? 0 : Math.Round(vm.RoomRevenue / vm.RoomNightsSold, 2);
        vm.RoomTypePerformance = nightsByStay
            .GroupBy(s => s.TypeName)
            .Select(g => new NamedValue(g.Key, g.Sum(x => x.Nights), g.Sum(x => x.Nights * x.PricePerNight)))
            .OrderByDescending(x => x.Amount).ToList();

        // Bookings arriving in the period.
        var arrivals = await bookings.QueryNoTracking()
            .Where(b => b.CheckInDate >= from && b.CheckInDate < to)
            .Select(b => new { b.Status, b.TotalAmount })
            .ToListAsync();
        vm.TotalBookings = arrivals.Count(b => b.Status != BookingStatus.Cancelled);
        vm.CancelledBookings = arrivals.Count(b => b.Status == BookingStatus.Cancelled);
        vm.BookingsByStatus = arrivals.GroupBy(b => b.Status)
            .Select(g => new NamedValue(g.Key.DisplayName(), g.Count(), g.Sum(x => x.TotalAmount)))
            .OrderByDescending(x => x.Count).ToList();

        // Money actually collected in the period.
        var periodPayments = await payments.QueryNoTracking()
            .Where(p => p.PaidAt >= from && p.PaidAt < to)
            .Select(p => new
            {
                p.Amount,
                p.Method,
                GuestId = p.Invoice!.Booking!.GuestId,
                GuestName = p.Invoice.Booking.Guest!.FirstName + " " + p.Invoice.Booking.Guest.LastName
            })
            .ToListAsync();
        vm.TotalRevenue = periodPayments.Sum(p => p.Amount);
        vm.PaymentsByMethod = periodPayments.GroupBy(p => p.Method)
            .Select(g => new NamedValue(g.Key.DisplayName(), g.Count(), g.Sum(x => x.Amount)))
            .OrderByDescending(x => x.Amount).ToList();
        vm.TopGuests = periodPayments.GroupBy(p => new { p.GuestId, p.GuestName })
            .Select(g => new NamedValue(g.Key.GuestName, g.Count(), g.Sum(x => x.Amount)))
            .OrderByDescending(x => x.Amount).Take(5).ToList();

        vm.CheckIns = await bookings.QueryNoTracking().CountAsync(b => b.ActualCheckIn >= from && b.ActualCheckIn < to);
        vm.CheckOuts = await bookings.QueryNoTracking().CountAsync(b => b.ActualCheckOut >= from && b.ActualCheckOut < to);
        vm.NewGuests = await guests.QueryNoTracking().CountAsync(g => g.CreatedAt >= from && g.CreatedAt < to);

        await BuildSeriesAsync(vm, filter.Mode, from, to);
        return vm;
    }

    private async Task BuildSeriesAsync(ReportViewModel vm, ReportMode mode, DateTime from, DateTime to)
    {
        // A daily report shows the trend of the 7 days ending on that day.
        var seriesFrom = mode == ReportMode.Daily ? to.AddDays(-7) : from;
        var byMonth = (to - seriesFrom).Days > 62;

        var list = await payments.QueryNoTracking()
            .Where(p => p.PaidAt >= seriesFrom && p.PaidAt < to)
            .Select(p => new { p.PaidAt, p.Amount })
            .ToListAsync();

        if (byMonth)
        {
            vm.SeriesTitle = "Revenue by month";
            for (var m = new DateTime(seriesFrom.Year, seriesFrom.Month, 1); m < to; m = m.AddMonths(1))
            {
                vm.SeriesLabels.Add(m.ToString("MMM yy"));
                vm.SeriesValues.Add(list.Where(p => p.PaidAt.Year == m.Year && p.PaidAt.Month == m.Month).Sum(p => p.Amount));
            }
        }
        else
        {
            vm.SeriesTitle = mode == ReportMode.Daily ? "Revenue (last 7 days)" : "Revenue by day";
            for (var d = seriesFrom; d < to; d = d.AddDays(1))
            {
                vm.SeriesLabels.Add(d.ToString("MMM dd"));
                vm.SeriesValues.Add(list.Where(p => p.PaidAt.Date == d).Sum(p => p.Amount));
            }
        }
    }

    public async Task<(string FileName, byte[] Content)> ExportCsvAsync(ReportFilter filter, string kind)
    {
        var (from, to, _) = ResolveRange(filter);
        var sb = new StringBuilder();
        var stamp = $"{from:yyyyMMdd}-{to.AddDays(-1):yyyyMMdd}";

        switch (kind.ToLowerInvariant())
        {
            case "payments":
            {
                sb.AppendLine("Receipt,Date,Invoice,Booking,Guest,Method,Amount,Reference,Received By");
                var rows = await payments.QueryNoTracking()
                    .Include(p => p.Invoice).ThenInclude(i => i!.Booking).ThenInclude(b => b!.Guest)
                    .Where(p => p.PaidAt >= from && p.PaidAt < to)
                    .OrderBy(p => p.PaidAt).ToListAsync();
                foreach (var p in rows)
                    Row(sb, p.ReceiptNumber, p.PaidAt.ToString("yyyy-MM-dd HH:mm"), p.Invoice!.InvoiceNumber,
                        p.Invoice.Booking!.BookingNumber, p.Invoice.Booking.Guest!.FullName, p.Method.DisplayName(),
                        p.Amount.ToString("0.00", CultureInfo.InvariantCulture), p.Reference, p.ReceivedBy);
                return ($"payments-{stamp}.csv", Encode(sb));
            }
            case "guests":
            {
                sb.AppendLine("Guest,Phone,Email,Nationality,Stays,Nights,Amount Booked");
                var rows = await bookings.QueryWithDetails().AsNoTracking()
                    .Where(b => b.Status != BookingStatus.Cancelled && b.CheckInDate < to && b.CheckOutDate > from)
                    .ToListAsync();
                foreach (var g in rows.GroupBy(b => b.Guest!.Id).OrderByDescending(g => g.Sum(b => b.TotalAmount)))
                {
                    var guest = g.First().Guest!;
                    Row(sb, guest.FullName, guest.Phone, guest.Email, guest.Nationality, g.Count().ToString(),
                        g.Sum(b => b.Nights).ToString(), g.Sum(b => b.TotalAmount).ToString("0.00", CultureInfo.InvariantCulture));
                }
                return ($"guests-{stamp}.csv", Encode(sb));
            }
            default:
            {
                sb.AppendLine("Booking,Guest,Phone,Room,Room Type,Check-in,Check-out,Nights,Status,Amount");
                var rows = await bookings.QueryWithDetails().AsNoTracking()
                    .Where(b => b.CheckInDate >= from && b.CheckInDate < to)
                    .OrderBy(b => b.CheckInDate).ToListAsync();
                foreach (var b in rows)
                    Row(sb, b.BookingNumber, b.Guest!.FullName, b.Guest.Phone, b.Room!.RoomNumber, b.Room.RoomType?.Name,
                        b.CheckInDate.ToString("yyyy-MM-dd"), b.CheckOutDate.ToString("yyyy-MM-dd"), b.Nights.ToString(),
                        b.Status.DisplayName(), b.TotalAmount.ToString("0.00", CultureInfo.InvariantCulture));
                return ($"bookings-{stamp}.csv", Encode(sb));
            }
        }
    }

    /// <summary>Returns [from, to) for the selected report mode.</summary>
    public static (DateTime From, DateTime To, string Label) ResolveRange(ReportFilter f)
    {
        var today = DateTime.Today;
        switch (f.Mode)
        {
            case ReportMode.Monthly:
                var month = DateTime.TryParseExact(f.Month, "yyyy-MM", CultureInfo.InvariantCulture, DateTimeStyles.None, out var m)
                    ? m : new DateTime(today.Year, today.Month, 1);
                f.Month = month.ToString("yyyy-MM");
                return (month, month.AddMonths(1), month.ToString("MMMM yyyy"));

            case ReportMode.Custom:
                var from = (f.From ?? today.AddDays(-29)).Date;
                var to = (f.To ?? today).Date;
                if (to < from) (from, to) = (to, from);
                if ((to - from).TotalDays > 366) from = to.AddDays(-366);
                f.From = from;
                f.To = to;
                return (from, to.AddDays(1), $"{from:dd MMM yyyy} – {to:dd MMM yyyy}");

            default:
                var day = (f.Date ?? today).Date;
                f.Date = day;
                return (day, day.AddDays(1), day.ToString("dddd, dd MMMM yyyy"));
        }
    }

    private static void Row(StringBuilder sb, params string?[] values) =>
        sb.AppendLine(string.Join(",", values.Select(Escape)));

    private static string Escape(string? value)
    {
        if (string.IsNullOrEmpty(value)) return "";
        // Neutralise spreadsheet formula injection.
        if ("=+-@".Contains(value[0])) value = "'" + value;
        return value.IndexOfAny([',', '"', '\n', '\r']) >= 0 ? $"\"{value.Replace("\"", "\"\"")}\"" : value;
    }

    private static byte[] Encode(StringBuilder sb) => Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(sb.ToString())).ToArray();
}
