using Microsoft.Extensions.Options;
using SmartHotel.Repositories;
using SmartHotel.ViewModels;

namespace SmartHotel.Services;

public interface IBillingService
{
    Task<PagedResult<Invoice>> SearchAsync(string? search, InvoiceStatus? status, int page, int pageSize = 10, InvoiceView? view = null);
    Task<BillingSummary> GetSummaryAsync();
    Task<Invoice?> GetAsync(int id);
    Task<List<Booking>> GetBookingsWithoutInvoiceAsync();
    Task<ServiceResult<Invoice>> CreateAsync(InvoiceCreateViewModel model);
    Task<ServiceResult> AdjustAsync(InvoiceAdjustViewModel model);
    Task<ServiceResult<Payment>> AddPaymentAsync(PaymentFormViewModel model, string user);
    Task<Payment?> GetPaymentAsync(int id);

    /// <summary>Returns the booking's invoice, creating it if necessary (no save). Booking.Invoice must be loaded.</summary>
    Invoice EnsureInvoice(Booking booking);

    /// <summary>Applies a payment to a tracked invoice (no save).</summary>
    Payment ApplyPayment(Invoice invoice, decimal amount, PaymentMethod method, string? reference, string user);
}

public class BillingService(
    IRepository<Invoice> invoices,
    IRepository<Payment> payments,
    IBookingRepository bookings,
    INotificationService notifications,
    IOptions<HotelSettings> settings) : IBillingService
{
    private IQueryable<Invoice> WithDetails(IQueryable<Invoice> q) =>
        q.Include(i => i.Booking).ThenInclude(b => b!.Guest)
         .Include(i => i.Booking).ThenInclude(b => b!.Room).ThenInclude(r => r!.RoomType);

    public Task<PagedResult<Invoice>> SearchAsync(string? search, InvoiceStatus? status, int page, int pageSize = 10, InvoiceView? view = null)
    {
        var q = WithDetails(invoices.QueryNoTracking());
        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim();
            q = q.Where(i => i.InvoiceNumber.Contains(s) ||
                             i.Booking!.BookingNumber.Contains(s) ||
                             i.Booking.Guest!.FirstName.Contains(s) || i.Booking.Guest.LastName.Contains(s) ||
                             (i.Booking.Guest.FirstName + " " + i.Booking.Guest.LastName).Contains(s) ||
                             i.Booking.Room!.RoomNumber.Contains(s));
        }
        if (status.HasValue) q = q.Where(i => i.Status == status.Value);

        // Same rules as GetSummaryAsync, so each card's list adds up to its figure.
        var today = DateTime.Today;
        q = view switch
        {
            InvoiceView.Collected => q.Where(i => i.Status != InvoiceStatus.Void && i.AmountPaid > 0),
            InvoiceView.Outstanding => q.Where(i => i.Status != InvoiceStatus.Void && i.Total > i.AmountPaid),
            InvoiceView.PaidToday => q.Where(i => i.Payments.Any(p => p.PaidAt >= today)),
            _ => q
        };
        return q.OrderByDescending(i => i.IssuedAt).ThenByDescending(i => i.Id).ToPagedAsync(page, pageSize);
    }

    public async Task<BillingSummary> GetSummaryAsync()
    {
        var active = invoices.QueryNoTracking().Where(i => i.Status != InvoiceStatus.Void);
        var today = DateTime.Today;
        var total = await active.SumAsync(i => (decimal?)i.Total) ?? 0;
        var paid = await active.SumAsync(i => (decimal?)i.AmountPaid) ?? 0;

        return new BillingSummary
        {
            TotalInvoiced = total,
            TotalCollected = paid,
            Outstanding = Math.Max(0, total - paid),
            CollectedToday = await payments.QueryNoTracking()
                .Where(p => p.PaidAt >= today).SumAsync(p => (decimal?)p.Amount) ?? 0
        };
    }

    public Task<Invoice?> GetAsync(int id) =>
        WithDetails(invoices.QueryNoTracking()).Include(i => i.Payments).FirstOrDefaultAsync(i => i.Id == id);

    public Task<List<Booking>> GetBookingsWithoutInvoiceAsync() =>
        bookings.QueryWithDetails().AsNoTracking()
            .Where(b => b.Invoice == null && b.Status != BookingStatus.Cancelled)
            .OrderBy(b => b.CheckInDate)
            .ToListAsync();

    public async Task<ServiceResult<Invoice>> CreateAsync(InvoiceCreateViewModel model)
    {
        var booking = await bookings.QueryWithDetails().FirstOrDefaultAsync(b => b.Id == model.BookingId);
        if (booking is null) return ServiceResult<Invoice>.Fail("Booking not found.");
        if (booking.Status == BookingStatus.Cancelled) return ServiceResult<Invoice>.Fail("Cannot invoice a cancelled booking.");
        if (booking.Invoice is not null)
            return ServiceResult<Invoice>.Fail($"Booking {booking.BookingNumber} already has invoice {booking.Invoice.InvoiceNumber}.");
        if (model.Discount > booking.TotalAmount + model.ExtraCharges)
            return ServiceResult<Invoice>.Fail("Discount cannot be greater than the charges.");

        var invoice = EnsureInvoice(booking);
        invoice.ExtraCharges = model.ExtraCharges;
        invoice.Discount = model.Discount;
        invoice.Notes = model.Notes?.Trim();
        BillingCalculator.Recalculate(invoice, booking);

        await invoices.SaveChangesAsync();
        return ServiceResult<Invoice>.Ok(invoice);
    }

    public async Task<ServiceResult> AdjustAsync(InvoiceAdjustViewModel model)
    {
        var invoice = await invoices.Query().Include(i => i.Booking).FirstOrDefaultAsync(i => i.Id == model.Id);
        if (invoice is null) return ServiceResult.Fail("Invoice not found.");
        if (invoice.Status == InvoiceStatus.Void) return ServiceResult.Fail("A void invoice cannot be changed.");
        if (model.Discount > invoice.Subtotal + model.ExtraCharges)
            return ServiceResult.Fail("Discount cannot be greater than the charges.");

        invoice.ExtraCharges = model.ExtraCharges;
        invoice.Discount = model.Discount;
        invoice.Notes = model.Notes?.Trim();
        BillingCalculator.Recalculate(invoice, invoice.Booking!);

        if (invoice.Total + BillingCalculator.Tolerance < invoice.AmountPaid)
            return ServiceResult.Fail($"The new total ({Ui.Money(invoice.Total)}) is less than the amount already paid ({Ui.Money(invoice.AmountPaid)}).");

        await invoices.SaveChangesAsync();
        return ServiceResult.Ok();
    }

    public async Task<ServiceResult<Payment>> AddPaymentAsync(PaymentFormViewModel model, string user)
    {
        var invoice = await invoices.Query().Include(i => i.Booking).ThenInclude(b => b!.Guest)
            .FirstOrDefaultAsync(i => i.Id == model.InvoiceId);
        if (invoice is null) return ServiceResult<Payment>.Fail("Invoice not found.");
        if (invoice.Status == InvoiceStatus.Void) return ServiceResult<Payment>.Fail("Cannot take payment on a void invoice.");
        if (invoice.Balance <= BillingCalculator.Tolerance) return ServiceResult<Payment>.Fail("This invoice is already fully paid.");
        if (model.Amount > invoice.Balance + BillingCalculator.Tolerance)
            return ServiceResult<Payment>.Fail($"Amount exceeds the outstanding balance of {Ui.Money(invoice.Balance)}.");

        var payment = ApplyPayment(invoice, Math.Round(model.Amount, 2), model.Method, model.Reference, user);
        await invoices.SaveChangesAsync();

        await notifications.CreateAsync(NotificationType.Payment, "Payment received",
            $"Payment received - Invoice #{invoice.InvoiceNumber} {Ui.Money(payment.Amount)} ({invoice.Booking?.Guest?.FullName})",
            $"/Payments/Details/{invoice.Id}");
        return ServiceResult<Payment>.Ok(payment);
    }

    public Task<Payment?> GetPaymentAsync(int id) =>
        payments.QueryNoTracking()
            .Include(p => p.Invoice).ThenInclude(i => i!.Booking).ThenInclude(b => b!.Guest)
            .Include(p => p.Invoice).ThenInclude(i => i!.Booking).ThenInclude(b => b!.Room)
            .FirstOrDefaultAsync(p => p.Id == id);

    public Invoice EnsureInvoice(Booking booking)
    {
        if (booking.Invoice is { } existing)
        {
            if (existing.Status != InvoiceStatus.Void) BillingCalculator.Recalculate(existing, booking);
            return existing;
        }

        var invoice = new Invoice
        {
            Booking = booking,
            TaxRate = settings.Value.TaxRate,
            IssuedAt = DateTime.Now,
            DueDate = booking.CheckOutDate
        };
        BillingCalculator.Recalculate(invoice, booking);
        booking.Invoice = invoice;
        invoices.Add(invoice);
        return invoice;
    }

    public Payment ApplyPayment(Invoice invoice, decimal amount, PaymentMethod method, string? reference, string user)
    {
        var payment = new Payment
        {
            Invoice = invoice,
            Amount = amount,
            Method = method,
            Reference = string.IsNullOrWhiteSpace(reference) ? null : reference.Trim(),
            ReceivedBy = user,
            PaidAt = DateTime.Now
        };
        payments.Add(payment);
        invoice.AmountPaid += amount;
        BillingCalculator.UpdateStatus(invoice);
        return payment;
    }
}
