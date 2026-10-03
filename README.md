# Smart Hotel Management System

A hotel management web application built with **C#, ASP.NET Core MVC (.NET 9), SQL Server and Entity Framework Core**.

## Modules

| Module | What it does |
|---|---|
| Dashboard | Rooms, occupancy, guests, monthly revenue, 12-month revenue chart, room-status chart, recent bookings, quick actions |
| Room Management | Room CRUD, room types (price, capacity, amenities), search and filter by type/status, pagination |
| Booking Management | Create / edit / cancel / confirm / delete reservations, date search, overlap and capacity checks, live room availability |
| Guest Management | Guest profiles (ID type & number, phone, email, nationality), booking history, total nights and amount paid |
| Check-in / Check-out | Today's arrivals, walk-in check-in, in-house list, check-out with settlement. Room status changes automatically (Occupied → Dirty) |
| Payment & Billing | Invoices with tax, extra charges, discounts and balances; partial payments; printable invoices and receipts |
| Housekeeping | Available, Occupied, Dirty, Clean and Maintenance board with an audit trail of status changes |
| Staff Management | Employees, roles, login accounts, activation, password reset |
| Reports | Daily / Monthly / Custom: bookings, revenue, occupancy, ADR, payments by method, room-type performance, top guests, CSV export |
| Notifications | Booking, payment, check-in/out, housekeeping and maintenance alerts (bell menu + page) |
| REST API | `/api/rooms`, `/api/bookings`, `/api/dashboard` (JSON, same login and roles) |

## Roles

| Role | Access |
|---|---|
| **Admin** | Everything, including managing other administrators |
| **Manager** | Bookings, rooms and room types, guests, billing, staff (not admins), reports |
| **Receptionist** | Dashboard, rooms (view), bookings, guests, check-in/out, billing |
| **Housekeeping / Maintenance** | Housekeeping board and notifications |

## Default accounts (created on first run)

| Username | Password | Role |
|---|---|---|
| `admin` | `Admin@123` | Admin |
| `manager` | `Manager@123` | Manager |
| `reception` | `Reception@123` | Receptionist |
| `housekeeping` | `House@123` | Housekeeping |
| `maintenance` | `Maint@123` | Maintenance |

> **Change these passwords before going live** (profile menu → *Change password*, or *Staff Management → Reset password*).

## Setup

### Requirements
- **.NET 9 SDK**: https://dotnet.microsoft.com/download/dotnet/9.0
- **SQL Server**: LocalDB (installed with Visual Studio), SQL Server Express or full SQL Server
- Visual Studio 2022 (17.12+), VS Code or Rider

### Steps
1. Open `SmartHotel.sln` in Visual Studio (or open the folder in VS Code).
2. Update the connection string in `SmartHotel/appsettings.json`:
   ```json
   "ConnectionStrings": {
     "DefaultConnection": "Server=(localdb)\\MSSQLLocalDB;Database=SmartHotelDb;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True"
   }
   ```
   Examples:
   - SQL Express: `Server=.\\SQLEXPRESS;Database=SmartHotelDb;Trusted_Connection=True;TrustServerCertificate=True`
   - SQL login: `Server=localhost;Database=SmartHotelDb;User Id=sa;Password=YourPassword;TrustServerCertificate=True`
3. Create the database. **Either:**
   - Just run the app. Migrations are applied automatically on startup (`Database:MigrateOnStartup = true`). **Or**
   - Run them yourself:
     ```bash
     dotnet tool restore
     dotnet ef database update --project SmartHotel
     ```
   - **Or** run `database/SmartHotelDb.sql` in SQL Server Management Studio.
4. Sample data (50 rooms, 48 guests, about 1,500 bookings with invoices and payments over 12 months, staff and notifications) is seeded automatically on the first launch. Set `Database:SeedSampleData` to `false` to start with only the admin accounts.
5. Run it:
   ```bash
   dotnet run --project SmartHotel
   ```
   Or press **F5** in Visual Studio, then open https://localhost:7070 (or http://localhost:5070).
   If the browser warns about the HTTPS certificate, run `dotnet dev-certs https --trust` once.

### Quick demo without SQL Server
Set `"Database": { "UseInMemory": true }` in `appsettings.json` (or set the environment variable `Database__UseInMemory=true`).
The app then runs on an in-memory database, which is reset every time the app restarts.

## Project structure (clean, layered)

```
SmartHotel.sln
├── .config/dotnet-tools.json      dotnet-ef (local tool)
├── database/SmartHotelDb.sql      idempotent SQL script of all migrations
└── SmartHotel/
    ├── Models/                    domain entities + enums
    ├── ViewModels/                form/list/report models (with validation) + API DTOs
    ├── Data/                      AppDbContext, DbSeeder, Migrations/
    ├── Repositories/              IRepository<T>, Repository<T>, BookingRepository
    ├── Services/                  business logic (Booking, Billing, Room, Guest, Housekeeping,
    │                              Staff/Auth, Dashboard, Report, Notification)
    ├── Controllers/               MVC controllers + Api/ REST controllers
    ├── Infrastructure/            security (roles, policies, cookie validation), paging, UI helpers
    ├── Views/                     Razor views (Bootstrap 5, Bootstrap Icons, Chart.js)
    └── wwwroot/                   css, js, images
```

Request flow: **Controller → Service (business rules, returns `ServiceResult`) → Repository → EF Core `AppDbContext` → SQL Server**.

## Business rules

- A room cannot be double-booked: overlapping active bookings are rejected, as are rooms under maintenance and parties larger than the room capacity.
- Check-in only happens on or after the arrival date and only into a *ready* room (Available or Clean). The room becomes **Occupied** and an invoice is created.
- Check-out requires the balance to be settled (a payment can be taken on the check-out screen). The room becomes **Dirty** and housekeeping is notified.
- Invoices use the nightly rate captured at booking time. Invoice total = (room charges + extras − discount) × (1 + tax rate). The tax rate is set in `Hotel:TaxRate`.
- Cancelling an unpaid booking voids its invoice. If it was already paid, the cancellation message flags that a refund may be due.
- Document numbers are generated automatically: `BK-0001`, `INV-0001`, `RCP-00001`.

## Security

- Cookie authentication with PBKDF2 password hashing (ASP.NET Core Identity `PasswordHasher`).
- Role-based authorization policies; every page requires login by default (fallback policy).
- Account lockout for 15 minutes after 5 failed logins, plus rate limiting on the login endpoint.
- A security stamp is checked on every request, so disabling a user, changing their role or resetting their password ends their existing sessions.
- Anti-forgery tokens on every POST, including API calls (send header `X-CSRF-TOKEN`).
- HttpOnly and SameSite cookies (Secure outside Development), HSTS, security headers, and CSV formula-injection protection.

## REST API (examples)

All endpoints require a signed-in session. POST/PUT requests also need the `X-CSRF-TOKEN` header (its value is in `<meta name="csrf-token">`).

```
GET  /api/dashboard
GET  /api/rooms?search=&status=Available&page=1
GET  /api/rooms/5
GET  /api/rooms/available?checkIn=2026-10-01&checkOut=2026-10-03
PUT  /api/rooms/5/status            { "status": "Dirty", "note": "..." }
GET  /api/bookings?search=&from=&to=&status=Confirmed
GET  /api/bookings/12
GET  /api/bookings/arrivals
GET  /api/bookings/in-house
POST /api/bookings                  { "guestId":1, "roomId":5, "checkInDate":"2026-10-01", "checkOutDate":"2026-10-03", "adults":1 }
POST /api/bookings/12/cancel        { "reason": "..." }
POST /api/bookings/12/check-in
GET  /api/notifications/unread-count
```

## Configuration (`appsettings.json`)

| Key | Default | Meaning |
|---|---|---|
| `ConnectionStrings:DefaultConnection` | LocalDB | SQL Server connection |
| `Database:MigrateOnStartup` | `true` | Apply EF migrations when the app starts |
| `Database:SeedSampleData` | `true` | Load demo data into an empty database |
| `Database:UseInMemory` | `false` | Run without SQL Server (demo only) |
| `Hotel:Name / Address / Phone / Email` | | Shown on invoices and receipts |
| `Hotel:Currency` | `$` | Currency symbol |
| `Hotel:TaxRate` | `0.10` | 10% tax on invoices |

## Adding a database change

```bash
dotnet ef migrations add YourChangeName --project SmartHotel --output-dir Data/Migrations
dotnet ef database update --project SmartHotel
```
