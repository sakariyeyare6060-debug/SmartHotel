using System.ComponentModel.DataAnnotations;

namespace SmartHotel.ViewModels;

/// <summary>Shortcuts behind the Payment &amp; Billing summary cards.</summary>
public enum InvoiceView { Collected, Outstanding, PaidToday }

public class InvoiceListViewModel
{
    public PagedResult<Invoice> Invoices { get; set; } = PagedResult<Invoice>.Empty();
    public string? Search { get; set; }
    public InvoiceStatus? Status { get; set; }
    public InvoiceView? View { get; set; }
    public BillingSummary Summary { get; set; } = new();
}

public class BillingSummary
{
    public decimal TotalInvoiced { get; set; }
    public decimal TotalCollected { get; set; }
    public decimal Outstanding { get; set; }
    public decimal CollectedToday { get; set; }
}

public class InvoiceCreateViewModel
{
    [Required(ErrorMessage = "Select a booking."), Display(Name = "Booking")]
    public int? BookingId { get; set; }

    [Range(0, 1000000), Display(Name = "Extra charges")]
    public decimal ExtraCharges { get; set; }

    [Range(0, 1000000)]
    public decimal Discount { get; set; }

    [StringLength(500)]
    public string? Notes { get; set; }

    public List<Booking> Bookings { get; set; } = [];
}

public class InvoiceAdjustViewModel
{
    public int Id { get; set; }

    [Range(0, 1000000), Display(Name = "Extra charges")]
    public decimal ExtraCharges { get; set; }

    [Range(0, 1000000)]
    public decimal Discount { get; set; }

    [StringLength(500)]
    public string? Notes { get; set; }
}

public class PaymentFormViewModel
{
    public int InvoiceId { get; set; }

    [Range(0.01, 1000000, ErrorMessage = "Enter an amount greater than zero.")]
    public decimal Amount { get; set; }

    public PaymentMethod Method { get; set; } = PaymentMethod.Cash;

    [StringLength(100)]
    public string? Reference { get; set; }
}

public class InvoiceDetailsViewModel
{
    public Invoice Invoice { get; set; } = default!;
    public PaymentFormViewModel Payment { get; set; } = new();
    public InvoiceAdjustViewModel Adjust { get; set; } = new();
    public HotelSettings Hotel { get; set; } = new();
}

public class ReceiptViewModel
{
    public Payment Payment { get; set; } = default!;
    public HotelSettings Hotel { get; set; } = new();
}
