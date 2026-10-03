using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SmartHotel.Models;

public class Invoice : BaseEntity, INumbered
{
    [Required, StringLength(20)]
    public string InvoiceNumber { get; set; } = DocumentNumber.Temp();

    public int BookingId { get; set; }
    public Booking? Booking { get; set; }

    public DateTime IssuedAt { get; set; } = DateTime.Now;
    public DateTime? DueDate { get; set; }

    /// <summary>Room charges (nights x rate).</summary>
    public decimal Subtotal { get; set; }
    public decimal ExtraCharges { get; set; }
    public decimal Discount { get; set; }
    public decimal TaxRate { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal Total { get; set; }
    public decimal AmountPaid { get; set; }

    public InvoiceStatus Status { get; set; } = InvoiceStatus.Unpaid;

    [StringLength(500)]
    public string? Notes { get; set; }

    public ICollection<Payment> Payments { get; set; } = new List<Payment>();

    [NotMapped]
    public decimal Balance => Total - AmountPaid;

    [NotMapped]
    public bool NeedsNumber => InvoiceNumber.StartsWith(DocumentNumber.TempPrefix);

    public void AssignNumber() => InvoiceNumber = $"INV-{Id:D4}";
}
