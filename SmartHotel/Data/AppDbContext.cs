namespace SmartHotel.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<RoomType> RoomTypes => Set<RoomType>();
    public DbSet<Room> Rooms => Set<Room>();
    public DbSet<Guest> Guests => Set<Guest>();
    public DbSet<Booking> Bookings => Set<Booking>();
    public DbSet<Invoice> Invoices => Set<Invoice>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<Staff> Staff => Set<Staff>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<HousekeepingLog> HousekeepingLogs => Set<HousekeepingLog>();
    public DbSet<MaintenanceRecord> MaintenanceRecords => Set<MaintenanceRecord>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<RoomType>(e =>
        {
            e.HasIndex(x => x.Name).IsUnique();
            e.Property(x => x.BasePrice).HasPrecision(18, 2);
        });

        b.Entity<Room>(e =>
        {
            e.HasIndex(x => x.RoomNumber).IsUnique();
            e.HasIndex(x => x.Status);
            e.Property(x => x.PricePerNight).HasPrecision(18, 2);
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
            e.HasOne(x => x.RoomType).WithMany(t => t.Rooms)
                .HasForeignKey(x => x.RoomTypeId).OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<Guest>(e =>
        {
            e.HasIndex(x => x.Email);
            e.HasIndex(x => x.Phone);
            e.HasIndex(x => new { x.LastName, x.FirstName });
        });

        b.Entity<Booking>(e =>
        {
            e.HasIndex(x => x.BookingNumber).IsUnique();
            e.HasIndex(x => new { x.RoomId, x.CheckInDate, x.CheckOutDate });
            e.HasIndex(x => x.Status);
            e.Property(x => x.CheckInDate).HasColumnType("date");
            e.Property(x => x.CheckOutDate).HasColumnType("date");
            e.Property(x => x.PricePerNight).HasPrecision(18, 2);
            e.Property(x => x.TotalAmount).HasPrecision(18, 2);
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
            e.HasOne(x => x.Guest).WithMany(g => g.Bookings)
                .HasForeignKey(x => x.GuestId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Room).WithMany(r => r.Bookings)
                .HasForeignKey(x => x.RoomId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Invoice).WithOne(i => i.Booking)
                .HasForeignKey<Invoice>(i => i.BookingId).OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<Invoice>(e =>
        {
            e.HasIndex(x => x.InvoiceNumber).IsUnique();
            e.HasIndex(x => x.Status);
            foreach (var p in new[] { nameof(Invoice.Subtotal), nameof(Invoice.ExtraCharges), nameof(Invoice.Discount),
                                      nameof(Invoice.TaxAmount), nameof(Invoice.Total), nameof(Invoice.AmountPaid) })
                e.Property(p).HasPrecision(18, 2);
            e.Property(x => x.TaxRate).HasPrecision(5, 4);
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
        });

        b.Entity<Payment>(e =>
        {
            e.HasIndex(x => x.ReceiptNumber).IsUnique();
            e.HasIndex(x => x.PaidAt);
            e.Property(x => x.Amount).HasPrecision(18, 2);
            e.Property(x => x.Method).HasConversion<string>().HasMaxLength(20);
            e.HasOne(x => x.Invoice).WithMany(i => i.Payments)
                .HasForeignKey(x => x.InvoiceId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<Staff>(e =>
        {
            e.HasIndex(x => x.Username).IsUnique();
            e.HasIndex(x => x.Email).IsUnique();
            e.Property(x => x.Role).HasConversion<string>().HasMaxLength(20);
        });

        b.Entity<Notification>(e =>
        {
            e.HasIndex(x => new { x.IsRead, x.CreatedAt });
            e.Property(x => x.Type).HasConversion<string>().HasMaxLength(20);
        });

        b.Entity<HousekeepingLog>(e =>
        {
            e.Property(x => x.FromStatus).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.ToStatus).HasConversion<string>().HasMaxLength(20);
            e.HasOne(x => x.Room).WithMany(r => r.HousekeepingLogs)
                .HasForeignKey(x => x.RoomId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<MaintenanceRecord>(e =>
        {
            e.HasIndex(x => x.CreatedAt);
            e.Property(x => x.Cost).HasPrecision(18, 2);
            e.Property(x => x.FaultType).HasConversion<string>().HasMaxLength(30);
            e.HasOne(x => x.Room).WithMany()
                .HasForeignKey(x => x.RoomId).OnDelete(DeleteBehavior.Cascade);
        });
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        var numbered = PrepareSave();
        var result = base.SaveChanges(acceptAllChangesOnSuccess);
        if (AssignNumbers(numbered)) result += base.SaveChanges(acceptAllChangesOnSuccess);
        return result;
    }

    public override async Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        var numbered = PrepareSave();
        var result = await base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
        if (AssignNumbers(numbered)) result += await base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
        return result;
    }

    private List<INumbered> PrepareSave()
    {
        foreach (var entry in ChangeTracker.Entries<BaseEntity>())
        {
            if (entry.State == EntityState.Modified) entry.Entity.UpdatedAt = DateTime.Now;
        }

        return ChangeTracker.Entries()
            .Where(e => e.State == EntityState.Added && e.Entity is INumbered)
            .Select(e => (INumbered)e.Entity)
            .ToList();
    }

    private static bool AssignNumbers(List<INumbered> added)
    {
        var any = false;
        foreach (var entity in added.Where(e => e.NeedsNumber))
        {
            entity.AssignNumber();
            any = true;
        }
        return any;
    }
}
