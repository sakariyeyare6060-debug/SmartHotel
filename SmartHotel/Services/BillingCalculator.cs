namespace SmartHotel.Services;

/// <summary>Pure invoice arithmetic, shared by services and the seeder.</summary>
public static class BillingCalculator
{
    public const decimal Tolerance = 0.005m;

    public static void Recalculate(Invoice invoice, Booking booking)
    {
        booking.TotalAmount = Math.Round(booking.PricePerNight * booking.Nights, 2);
        invoice.Subtotal = booking.TotalAmount;

        var taxable = Math.Max(0m, invoice.Subtotal + invoice.ExtraCharges - invoice.Discount);
        invoice.TaxAmount = Math.Round(taxable * invoice.TaxRate, 2, MidpointRounding.AwayFromZero);
        invoice.Total = taxable + invoice.TaxAmount;
        UpdateStatus(invoice);
    }

    public static void UpdateStatus(Invoice invoice)
    {
        if (invoice.Status == InvoiceStatus.Void) return;

        invoice.Status = invoice.AmountPaid <= 0
            ? InvoiceStatus.Unpaid
            : invoice.AmountPaid + Tolerance >= invoice.Total
                ? InvoiceStatus.Paid
                : InvoiceStatus.PartiallyPaid;
    }
}
