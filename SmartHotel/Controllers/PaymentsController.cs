using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using SmartHotel.Services;
using SmartHotel.ViewModels;

namespace SmartHotel.Controllers;

/// <summary>Payment &amp; Billing: invoices, payments and receipts.</summary>
[Authorize(Policy = Policies.FrontDesk)]
public class PaymentsController(IBillingService billing, IOptions<HotelSettings> hotel) : AppController
{
    public async Task<IActionResult> Index(string? search, InvoiceStatus? status, InvoiceView? view, int page = 1) =>
        View(new InvoiceListViewModel
        {
            Search = search,
            Status = status,
            View = view,
            Invoices = await billing.SearchAsync(search, status, page, view: view),
            Summary = await billing.GetSummaryAsync()
        });

    [HttpGet]
    public async Task<IActionResult> Create(int? bookingId) =>
        View(new InvoiceCreateViewModel { BookingId = bookingId, Bookings = await billing.GetBookingsWithoutInvoiceAsync() });

    [HttpPost]
    public async Task<IActionResult> Create(InvoiceCreateViewModel model)
    {
        if (ModelState.IsValid)
        {
            var result = await billing.CreateAsync(model);
            if (result.Succeeded)
            {
                Success($"Invoice {result.Value!.InvoiceNumber} created.");
                return RedirectToAction(nameof(Details), new { id = result.Value.Id });
            }
            ModelState.AddModelError(string.Empty, result.Error!);
        }
        model.Bookings = await billing.GetBookingsWithoutInvoiceAsync();
        return View(model);
    }

    public async Task<IActionResult> Details(int id)
    {
        var invoice = await billing.GetAsync(id);
        if (invoice is null) return NotFound();

        return View(new InvoiceDetailsViewModel
        {
            Invoice = invoice,
            Hotel = hotel.Value,
            Payment = new PaymentFormViewModel { InvoiceId = id, Amount = Math.Max(0, invoice.Balance) },
            Adjust = new InvoiceAdjustViewModel { Id = id, ExtraCharges = invoice.ExtraCharges, Discount = invoice.Discount, Notes = invoice.Notes }
        });
    }

    [HttpPost]
    public async Task<IActionResult> AddPayment([Bind(Prefix = "Payment")] PaymentFormViewModel model)
    {
        if (!ModelState.IsValid)
        {
            Error(FirstModelError());
            return RedirectToAction(nameof(Details), new { id = model.InvoiceId });
        }

        var result = await billing.AddPaymentAsync(model, CurrentUser);
        if (result.Succeeded) Success($"Payment of {Ui.Money(result.Value!.Amount)} recorded. Receipt {result.Value.ReceiptNumber}.");
        else Error(result.Error!);
        return RedirectToAction(nameof(Details), new { id = model.InvoiceId });
    }

    [HttpPost]
    public async Task<IActionResult> Adjust([Bind(Prefix = "Adjust")] InvoiceAdjustViewModel model)
    {
        if (!ModelState.IsValid)
        {
            Error(FirstModelError());
            return RedirectToAction(nameof(Details), new { id = model.Id });
        }

        var result = await billing.AdjustAsync(model);
        if (result.Succeeded) Success("Invoice updated and totals recalculated.");
        else Error(result.Error!);
        return RedirectToAction(nameof(Details), new { id = model.Id });
    }

    public async Task<IActionResult> Receipt(int id)
    {
        var payment = await billing.GetPaymentAsync(id);
        return payment is null ? NotFound() : View(new ReceiptViewModel { Payment = payment, Hotel = hotel.Value });
    }
}
