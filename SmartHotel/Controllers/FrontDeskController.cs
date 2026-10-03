using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartHotel.Services;
using SmartHotel.ViewModels;

namespace SmartHotel.Controllers;

/// <summary>Check-in / Check-out desk.</summary>
[Authorize(Policy = Policies.FrontDesk)]
public class FrontDeskController(
    IBookingService bookings,
    IGuestService guests,
    IRoomService rooms) : AppController
{
    public async Task<IActionResult> Index(string tab = "checkin", int? guestId = null) =>
        View(await BuildAsync(new WalkInViewModel { GuestId = guestId }, tab));

    [HttpPost]
    public async Task<IActionResult> WalkIn([Bind(Prefix = "WalkIn")] WalkInViewModel model)
    {
        if (!ModelState.IsValid) return View("Index", await BuildAsync(model, "checkin"));

        var result = await bookings.WalkInAsync(model, CurrentUser);
        if (!result.Succeeded)
        {
            ModelState.AddModelError(string.Empty, result.Error!);
            return View("Index", await BuildAsync(model, "checkin"));
        }
        Success($"{result.Value!.Guest!.FullName} checked in to room {result.Value.Room!.RoomNumber} ({result.Value.BookingNumber}).");
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public async Task<IActionResult> CheckIn(int id, string? returnUrl = null)
    {
        var result = await bookings.CheckInAsync(id, CurrentUser);
        if (result.Succeeded) Success("Guest checked in. Room status set to Occupied.");
        else Error(result.Error!);

        return !string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl)
            ? Redirect(returnUrl)
            : RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> CheckOut(int id)
    {
        var booking = await bookings.GetAsync(id);
        if (booking is null) return NotFound();
        if (booking.Status != BookingStatus.CheckedIn)
        {
            Error($"Booking {booking.BookingNumber} is not checked in.");
            return RedirectToAction(nameof(Index), new { tab = "checkout" });
        }

        // Preview invoice (the real one is created at check-in; this covers legacy data).
        var invoice = booking.Invoice ?? new Invoice { Subtotal = booking.TotalAmount, Total = booking.TotalAmount };
        return View(new CheckOutViewModel
        {
            Booking = booking,
            Invoice = invoice,
            Form = new CheckOutFormModel { BookingId = id, PaymentAmount = Math.Max(0, invoice.Balance) }
        });
    }

    [HttpPost]
    public async Task<IActionResult> CheckOut([Bind(Prefix = "Form")] CheckOutFormModel model)
    {
        if (!ModelState.IsValid)
        {
            Error(FirstModelError());
            return RedirectToAction(nameof(CheckOut), new { id = model.BookingId });
        }

        var result = await bookings.CheckOutAsync(model, CurrentUser);
        if (!result.Succeeded)
        {
            Error(result.Error!);
            return RedirectToAction(nameof(CheckOut), new { id = model.BookingId });
        }
        Success("Check-out completed. The room has been sent to housekeeping.");
        return RedirectToAction(nameof(Index), new { tab = "checkout" });
    }

    private async Task<FrontDeskViewModel> BuildAsync(WalkInViewModel walkIn, string tab)
    {
        var today = DateTime.Today;
        var checkOut = walkIn.CheckOutDate > today ? walkIn.CheckOutDate : today.AddDays(1);
        return new FrontDeskViewModel
        {
            Tab = tab == "checkout" ? "checkout" : "checkin",
            WalkIn = walkIn,
            Guests = GuestOptions(await guests.GetAllForSelectAsync()),
            ReadyRooms = RoomOptions(await rooms.GetAvailableRoomsAsync(today, checkOut)),
            Arrivals = await bookings.GetArrivalsAsync(today),
            InHouse = await bookings.GetInHouseAsync()
        };
    }
}
