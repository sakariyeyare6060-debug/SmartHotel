using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartHotel.Services;
using SmartHotel.ViewModels;

namespace SmartHotel.Controllers;

[Authorize(Policy = Policies.FrontDesk)]
public class BookingsController(IBookingService bookings, IGuestService guests, IRoomService rooms) : AppController
{
    public async Task<IActionResult> Index(string? search, DateTime? from, DateTime? to, BookingStatus? status, int page = 1) =>
        View(new BookingListViewModel
        {
            Search = search, From = from, To = to, Status = status,
            Bookings = await bookings.SearchAsync(search, from, to, status, page)
        });

    public async Task<IActionResult> Details(int id)
    {
        var booking = await bookings.GetAsync(id);
        return booking is null ? NotFound() : View(booking);
    }

    [HttpGet]
    public async Task<IActionResult> Create(int? guestId, int? roomId, DateTime? checkIn)
    {
        var model = new BookingFormViewModel { GuestId = guestId, RoomId = roomId };
        if (checkIn.HasValue && checkIn.Value.Date >= DateTime.Today)
        {
            model.CheckInDate = checkIn.Value.Date;
            model.CheckOutDate = checkIn.Value.Date.AddDays(1);
        }
        return View("Form", await PopulateAsync(model));
    }

    [HttpPost]
    public async Task<IActionResult> Create(BookingFormViewModel model)
    {
        model.Id = null;
        if (!ModelState.IsValid) return View("Form", await PopulateAsync(model));

        var result = await bookings.CreateAsync(model, CurrentUser);
        if (!result.Succeeded)
        {
            ModelState.AddModelError(string.Empty, result.Error!);
            return View("Form", await PopulateAsync(model));
        }
        Success($"Booking {result.Value!.BookingNumber} created.");
        return RedirectToAction(nameof(Details), new { id = result.Value.Id });
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var b = await bookings.GetAsync(id);
        if (b is null) return NotFound();
        if (b.Status is BookingStatus.Cancelled or BookingStatus.CheckedOut)
        {
            Error($"Booking {b.BookingNumber} is {b.Status.DisplayName()} and can no longer be edited.");
            return RedirectToAction(nameof(Details), new { id });
        }

        return View("Form", await PopulateAsync(new BookingFormViewModel
        {
            Id = b.Id, BookingNumber = b.BookingNumber, IsCheckedIn = b.Status == BookingStatus.CheckedIn,
            GuestId = b.GuestId, RoomId = b.RoomId, CheckInDate = b.CheckInDate, CheckOutDate = b.CheckOutDate,
            Adults = b.Adults, Children = b.Children, Status = b.Status, SpecialRequests = b.SpecialRequests
        }));
    }

    [HttpPost]
    public async Task<IActionResult> Edit(int id, BookingFormViewModel model)
    {
        model.Id = id;
        if (!ModelState.IsValid) return View("Form", await PopulateAsync(model));

        var result = await bookings.UpdateAsync(model);
        if (!result.Succeeded)
        {
            ModelState.AddModelError(string.Empty, result.Error!);
            return View("Form", await PopulateAsync(model));
        }
        Success("Booking updated.");
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    public async Task<IActionResult> Confirm(int id)
    {
        var result = await bookings.ConfirmAsync(id);
        if (result.Succeeded) Success("Booking confirmed.");
        else Error(result.Error!);
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    public async Task<IActionResult> Cancel(int id, string? reason)
    {
        var result = await bookings.CancelAsync(id, reason, CurrentUser);
        if (result.Succeeded) Success(result.Value!);
        else Error(result.Error!);
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost, Authorize(Policy = Policies.Management)]
    public async Task<IActionResult> Delete(int id)
    {
        var result = await bookings.DeleteAsync(id);
        if (!result.Succeeded)
        {
            Error(result.Error!);
            return RedirectToAction(nameof(Details), new { id });
        }
        Success("Booking deleted.");
        return RedirectToAction(nameof(Index));
    }

    private async Task<BookingFormViewModel> PopulateAsync(BookingFormViewModel model)
    {
        model.Guests = GuestOptions(await guests.GetAllForSelectAsync());

        var checkOut = model.CheckOutDate > model.CheckInDate ? model.CheckOutDate : model.CheckInDate.AddDays(1);
        var available = await rooms.GetAvailableRoomsAsync(model.CheckInDate, checkOut, model.Id);
        if (model.RoomId.HasValue && available.All(r => r.Id != model.RoomId) && await rooms.GetAsync(model.RoomId.Value) is { } current)
            available.Insert(0, current);
        model.Rooms = RoomOptions(available);
        return model;
    }
}
