using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SmartHotel.Models;

public class Payment : BaseEntity, INumbered
{
    [Required, StringLength(20)]
    public string ReceiptNumber { get; set; } = DocumentNumber.Temp();

    public int InvoiceId { get; set; }
    public Invoice? Invoice { get; set; }

    public decimal Amount { get; set; }

    public PaymentMethod Method { get; set; } = PaymentMethod.Cash;

    public DateTime PaidAt { get; set; } = DateTime.Now;

    [StringLength(100)]
    public string? Reference { get; set; }

    [StringLength(100)]
    public string? ReceivedBy { get; set; }

    [NotMapped]
    public bool NeedsNumber => ReceiptNumber.StartsWith(DocumentNumber.TempPrefix);

    public void AssignNumber() => ReceiptNumber = $"RCP-{Id:D5}";
}
