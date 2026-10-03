using Microsoft.AspNetCore.Identity;
using SmartHotel.Services;

namespace SmartHotel.Data;

/// <summary>
/// Creates the default accounts and (optionally) a realistic year of sample data.
/// Runs automatically on first launch; does nothing once staff accounts exist.
/// </summary>
public static class DbSeeder
{
    private static readonly string[] FirstNames =
        ["Hodan", "Khadar", "Ifrah", "Yusuf", "Sahra", "Abdi", "Nimco", "Hassan", "Deeqa", "Ali", "Hibo", "Mahad",
         "Ayaan", "Liban", "Najma", "Bashir", "Farhia", "Ismail", "Ubah", "Mustafa", "Asha", "Zakariye", "Idil", "Samir"];

    private static readonly string[] LastNames =
        ["Mohamed", "Hassan", "Ali", "Yusuf", "Farah", "Abdi", "Jama", "Warsame", "Osman", "Ahmed", "Nur", "Ismail",
         "Aden", "Hussein", "Omar", "Elmi"];

    private static readonly string[] Nationalities =
        ["Somali", "Somali", "Somali", "Kenyan", "Ethiopian", "Djiboutian", "Emirati", "British", "American", "Turkish"];

    public static async Task SeedAsync(AppDbContext db, IPasswordHasher<Staff> hasher, HotelSettings settings,
        bool sampleData, ILogger logger)
    {
        if (await db.Staff.AnyAsync()) return;

        logger.LogInformation("Seeding default staff accounts...");
        SeedStaff(db, hasher);
        await db.SaveChangesAsync();

        if (!sampleData || await db.Rooms.AnyAsync()) return;

        logger.LogInformation("Seeding sample hotel data...");
        await SeedSampleDataAsync(db, settings);
        logger.LogInformation("Sample data created.");
    }

    private static void SeedStaff(AppDbContext db, IPasswordHasher<Staff> hasher)
    {
        Staff Make(string name, string email, string phone, string username, UserRole role, string password, int yearsAgo)
        {
            var s = new Staff
            {
                FullName = name, Email = email, Phone = phone, Username = username, Role = role,
                HireDate = DateTime.Today.AddYears(-yearsAgo).AddDays(-37 * yearsAgo)
            };
            s.PasswordHash = hasher.HashPassword(s, password);
            return s;
        }

        db.Staff.AddRange(
            Make("System Administrator", "admin@smarthotel.com", "0610000000", "admin", UserRole.Admin, "Admin@123", 4),
            Make("Abdirahman Ali", "abdirahman@hotel.com", "0612345001", "manager", UserRole.Manager, "Manager@123", 3),
            Make("Fatima Mohamed", "fatima@hotel.com", "0612345002", "reception", UserRole.Receptionist, "Reception@123", 2),
            Make("Mohamed Hassan", "mohamed@hotel.com", "0612345003", "housekeeping", UserRole.Housekeeping, "House@123", 2),
            Make("Libaan Yusuf", "libaan@hotel.com", "0612345004", "maintenance", UserRole.Maintenance, "Maint@123", 1));
    }

    private static async Task SeedSampleDataAsync(AppDbContext db, HotelSettings settings)
    {
        var rnd = new Random(20250526);
        var today = DateTime.Today;
        var now = DateTime.Now;
        const string frontDesk = "Fatima Mohamed";

        // ---- Room types & rooms (5 floors x 10 rooms) ----
        var single = new RoomType { Name = "Single", BasePrice = 50, Capacity = 1, Description = "Cozy room with a single bed and work desk.", Amenities = "Wi-Fi, TV, Air conditioning" };
        var dbl = new RoomType { Name = "Double", BasePrice = 80, Capacity = 2, Description = "Queen bed, ideal for couples.", Amenities = "Wi-Fi, TV, Air conditioning, Mini fridge" };
        var deluxe = new RoomType { Name = "Deluxe", BasePrice = 120, Capacity = 3, Description = "Spacious room with city view and lounge area.", Amenities = "Wi-Fi, Smart TV, Mini bar, City view" };
        var suite = new RoomType { Name = "Suite", BasePrice = 200, Capacity = 4, Description = "Separate living room, king bed and premium bathroom.", Amenities = "Wi-Fi, Smart TV, Mini bar, Jacuzzi, Sea view" };
        db.RoomTypes.AddRange(single, dbl, deluxe, suite);

        var rooms = new List<Room>();
        for (var floor = 1; floor <= 5; floor++)
        for (var n = 1; n <= 10; n++)
        {
            var type = n switch { <= 3 => single, <= 6 => dbl, <= 9 => deluxe, _ => suite };
            rooms.Add(new Room
            {
                RoomNumber = $"{floor}{n:D2}",
                Floor = floor,
                RoomType = type,
                PricePerNight = type.BasePrice + (floor >= 4 ? 10 : 0),
                Description = $"{type.Name} room on floor {floor}"
            });
        }
        db.Rooms.AddRange(rooms);

        // ---- Guests ----
        var guests = new List<Guest>();
        var names = new List<(string First, string Last)>
            { ("Ahmed", "Mohamed"), ("Amina", "Hassan"), ("Said", "Ali"), ("Fatima", "Yusuf"), ("Omar", "Farah") };
        while (names.Count < 48)
        {
            var candidate = (FirstNames[rnd.Next(FirstNames.Length)], LastNames[rnd.Next(LastNames.Length)]);
            if (!names.Contains(candidate)) names.Add(candidate);
        }
        for (var i = 0; i < names.Count; i++)
        {
            var (first, last) = names[i];
            var passport = rnd.Next(3) == 0;
            guests.Add(new Guest
            {
                FirstName = first,
                LastName = last,
                Email = i < 5 ? $"{first.ToLowerInvariant()}@gmail.com" : $"{first.ToLowerInvariant()}.{last.ToLowerInvariant()}@gmail.com",
                Phone = $"0612345{678 + i:D3}",
                IdType = passport ? "Passport" : "National ID",
                IdNumber = passport ? $"P{rnd.Next(1000000, 9999999)}" : $"SO{rnd.Next(10000000, 99999999)}",
                Nationality = Nationalities[rnd.Next(Nationalities.Length)],
                Address = "Mogadishu",
                DateOfBirth = today.AddYears(-rnd.Next(22, 65)).AddDays(-rnd.Next(0, 365)),
                CreatedAt = now.AddDays(-rnd.Next(0, 400))
            });
        }
        db.Guests.AddRange(guests);

        // ---- Bookings, invoices & payments ----
        var calendar = rooms.ToDictionary(r => r, _ => new List<(DateTime In, DateTime Out)>());
        bool Reserve(Room r, DateTime ci, DateTime co)
        {
            if (calendar[r].Any(x => x.In < co && ci < x.Out)) return false;
            calendar[r].Add((ci, co));
            return true;
        }

        Booking NewBooking(Guest g, Room r, DateTime ci, DateTime co, BookingStatus status)
        {
            var created = ci.AddDays(-rnd.Next(1, 21)).AddHours(rnd.Next(8, 20));
            if (created > now) created = now.AddMinutes(-rnd.Next(30, 60 * 48));
            var b = new Booking
            {
                Guest = g, Room = r, CheckInDate = ci, CheckOutDate = co, Status = status,
                Adults = Math.Max(1, Math.Min(r.RoomType!.Capacity, 1 + rnd.Next(0, 2))),
                PricePerNight = r.PricePerNight, CreatedAt = created, CreatedBy = frontDesk
            };
            b.TotalAmount = b.PricePerNight * b.Nights;
            db.Bookings.Add(b);
            return b;
        }

        Invoice NewInvoice(Booking b, DateTime issued)
        {
            var inv = new Invoice { Booking = b, TaxRate = settings.TaxRate, IssuedAt = issued, DueDate = b.CheckOutDate, CreatedAt = issued };
            if (rnd.Next(6) == 0) inv.ExtraCharges = rnd.Next(1, 6) * 10; // minibar, laundry...
            BillingCalculator.Recalculate(inv, b);
            b.Invoice = inv;
            db.Invoices.Add(inv);
            return inv;
        }

        void Pay(Invoice inv, decimal amount, DateTime at)
        {
            var method = (PaymentMethod)rnd.Next(0, 4);
            var p = new Payment
            {
                Invoice = inv, Amount = Math.Round(amount, 2), Method = method, PaidAt = at, CreatedAt = at,
                ReceivedBy = frontDesk,
                Reference = method == PaymentMethod.Cash ? null : $"TX{rnd.Next(100000, 999999)}"
            };
            inv.Payments.Add(p);
            inv.AmountPaid += p.Amount;
            BillingCalculator.UpdateStatus(inv);
        }

        var shuffledRooms = rooms.OrderBy(_ => rnd.Next()).ToList();
        var shuffledGuests = guests.OrderBy(_ => rnd.Next()).ToList();

        // 34 in-house guests (68% occupancy)
        var inHouse = new List<Booking>();
        for (var i = 0; i < 34; i++)
        {
            var room = shuffledRooms[i];
            var ci = today.AddDays(-rnd.Next(0, 4));
            var co = today.AddDays(rnd.Next(1, 6));
            Reserve(room, ci, co);
            var b = NewBooking(shuffledGuests[i], room, ci, co, BookingStatus.CheckedIn);
            b.ActualCheckIn = ci.AddHours(14).AddMinutes(rnd.Next(0, 300));
            if (b.ActualCheckIn > now) b.ActualCheckIn = now.AddMinutes(-rnd.Next(10, 120));
            room.Status = RoomStatus.Occupied;
            var inv = NewInvoice(b, b.ActualCheckIn.Value);
            switch (i % 3)
            {
                case 0: Pay(inv, inv.Total, b.ActualCheckIn.Value.AddMinutes(5)); break;
                case 1: Pay(inv, Math.Round(inv.Total / 2, 0), b.ActualCheckIn.Value.AddMinutes(5)); break;
            }
            inHouse.Add(b);
        }

        // Remaining rooms: 2 dirty (checked out today), 1 maintenance, 1 clean, rest available.
        var dirty = shuffledRooms.Skip(34).Take(2).ToList();
        var maintenanceRoom = shuffledRooms[36];
        var cleanRoom = shuffledRooms[37];
        foreach (var room in dirty)
        {
            var ci = today.AddDays(-2);
            Reserve(room, ci, today);
            var b = NewBooking(shuffledGuests[34 + dirty.IndexOf(room)], room, ci, today, BookingStatus.CheckedOut);
            b.ActualCheckIn = ci.AddHours(15);
            b.ActualCheckOut = today.AddHours(10).AddMinutes(30);
            if (b.ActualCheckOut > now) b.ActualCheckOut = now.AddMinutes(-90);
            var inv = NewInvoice(b, b.ActualCheckIn.Value);
            Pay(inv, inv.Total, b.ActualCheckOut.Value);
            room.Status = RoomStatus.Dirty;
            db.HousekeepingLogs.Add(new HousekeepingLog { Room = room, FromStatus = RoomStatus.Occupied, ToStatus = RoomStatus.Dirty, Note = $"Guest checked out", ChangedBy = frontDesk, CreatedAt = b.ActualCheckOut.Value });
        }
        maintenanceRoom.Status = RoomStatus.Maintenance;
        db.HousekeepingLogs.Add(new HousekeepingLog { Room = maintenanceRoom, FromStatus = RoomStatus.Available, ToStatus = RoomStatus.Maintenance, Note = "Air conditioning not cooling", ChangedBy = "Libaan Yusuf", CreatedAt = now.AddHours(-1) });
        cleanRoom.Status = RoomStatus.Clean;
        db.HousekeepingLogs.Add(new HousekeepingLog { Room = cleanRoom, FromStatus = RoomStatus.Dirty, ToStatus = RoomStatus.Clean, Note = "Room cleaned", ChangedBy = "Mohamed Hassan", CreatedAt = now.AddHours(-3) });

        // Today's arrivals and upcoming reservations
        var freeRooms = shuffledRooms.Skip(37).ToList(); // clean + available rooms
        var arrivalGuests = shuffledGuests.Skip(36).ToList();
        for (var i = 0; i < 3; i++)
        {
            var room = freeRooms[i];
            var co = today.AddDays(rnd.Next(2, 5));
            Reserve(room, today, co);
            NewBooking(arrivalGuests[i], room, today, co, i == 2 ? BookingStatus.Pending : BookingStatus.Confirmed);
        }

        var bookable = rooms.Where(r => r != maintenanceRoom).ToList();
        int upcoming = 0, attempts = 0;
        while (upcoming < 10 && attempts++ < 200)
        {
            var room = bookable[rnd.Next(bookable.Count)];
            var ci = today.AddDays(rnd.Next(1, 31));
            var co = ci.AddDays(rnd.Next(1, 6));
            if (!Reserve(room, ci, co)) continue;
            var status = upcoming switch { < 6 => BookingStatus.Confirmed, < 8 => BookingStatus.Pending, _ => BookingStatus.Cancelled };
            var b = NewBooking(guests[rnd.Next(guests.Count)], room, ci, co, status);
            if (status == BookingStatus.Cancelled)
            {
                b.CancelledAt = b.CreatedAt.AddDays(1) > now ? now.AddHours(-2) : b.CreatedAt.AddDays(1);
                b.CancellationReason = "Change of travel plans";
                calendar[room].Remove((ci, co));
            }
            else if (status == BookingStatus.Confirmed && upcoming % 2 == 0)
            {
                var inv = NewInvoice(b, b.CreatedAt);
                Pay(inv, Math.Round(inv.Total * 0.3m, 0), b.CreatedAt.AddMinutes(10)); // deposit
            }
            upcoming++;
        }

        // A year of history (checked-out stays)
        int history = 0;
        attempts = 0;
        while (history < 1500 && attempts++ < 10000)
        {
            var room = rooms[rnd.Next(rooms.Count)];
            var nights = rnd.Next(1, 6);
            var ci = today.AddDays(-rnd.Next(nights + 1, 366));
            var co = ci.AddDays(nights);
            if (co >= today.AddDays(-1) || !Reserve(room, ci, co)) continue;

            var guest = guests[rnd.Next(guests.Count)];
            if (rnd.Next(20) == 0)
            {
                var cancelled = NewBooking(guest, room, ci, co, BookingStatus.Cancelled);
                cancelled.CancelledAt = cancelled.CreatedAt.AddDays(2);
                cancelled.CancellationReason = "Guest request";
                calendar[room].Remove((ci, co));
            }
            else
            {
                var b = NewBooking(guest, room, ci, co, BookingStatus.CheckedOut);
                b.ActualCheckIn = ci.AddHours(14).AddMinutes(rnd.Next(0, 240));
                b.ActualCheckOut = co.AddHours(10).AddMinutes(rnd.Next(0, 60));
                var inv = NewInvoice(b, b.ActualCheckIn.Value);
                if (rnd.Next(4) == 0)
                {
                    var deposit = Math.Round(inv.Total * 0.5m, 0);
                    Pay(inv, deposit, b.ActualCheckIn.Value.AddMinutes(10));
                    Pay(inv, inv.Total - deposit, b.ActualCheckOut.Value);
                }
                else
                {
                    Pay(inv, inv.Total, b.ActualCheckOut.Value);
                }
            }
            history++;
        }

        foreach (var b in inHouse.Take(3))
            db.HousekeepingLogs.Add(new HousekeepingLog { Room = b.Room, FromStatus = RoomStatus.Available, ToStatus = RoomStatus.Occupied, Note = "Guest checked in", ChangedBy = frontDesk, CreatedAt = b.ActualCheckIn!.Value });

        await db.SaveChangesAsync(); // assigns BK-/INV-/RCP- numbers as well

        // ---- Notifications (need the generated numbers) ----
        var latestBooking = inHouse[0];
        var paidInvoice = inHouse.Select(b => b.Invoice!).First(i => i.Status == InvoiceStatus.Paid);
        db.Notifications.AddRange(
            new Notification { Type = NotificationType.Booking, Title = "New booking", Message = $"New booking received from {guests[0].FullName}", Link = "/Bookings", CreatedAt = now.AddMinutes(-2) },
            new Notification { Type = NotificationType.Payment, Title = "Payment received", Message = $"Payment received - Invoice #{paidInvoice.InvoiceNumber} {Ui.Money(paidInvoice.AmountPaid)}", Link = $"/Payments/Details/{paidInvoice.Id}", CreatedAt = now.AddMinutes(-5) },
            new Notification { Type = NotificationType.CheckIn, Title = "Guest checked in", Message = $"Guest checked in - Room {latestBooking.Room!.RoomNumber}", Link = $"/Bookings/Details/{latestBooking.Id}", CreatedAt = now.AddMinutes(-12) },
            new Notification { Type = NotificationType.Maintenance, Title = "Maintenance request", Message = $"Maintenance request - Room {maintenanceRoom.RoomNumber}: Air conditioning not cooling", Link = "/Housekeeping?status=Maintenance", CreatedAt = now.AddHours(-1) },
            new Notification { Type = NotificationType.CheckOut, Title = "Check-out completed", Message = $"Check-out completed - Room {dirty[0].RoomNumber}", Link = "/FrontDesk?tab=checkout", CreatedAt = now.AddHours(-2), IsRead = true },
            new Notification { Type = NotificationType.Housekeeping, Title = "Room cleaned", Message = $"Room {cleanRoom.RoomNumber} cleaned and ready for inspection", Link = "/Housekeeping", CreatedAt = now.AddHours(-3), IsRead = true },
            new Notification { Type = NotificationType.System, Title = "Welcome", Message = "Welcome to Smart Hotel Management System. Sample data has been loaded.", CreatedAt = now.AddDays(-1), IsRead = true });
        await db.SaveChangesAsync();
    }
}
