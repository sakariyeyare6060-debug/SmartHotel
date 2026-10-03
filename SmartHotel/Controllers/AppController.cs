using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace SmartHotel.Controllers;

public abstract class AppController : Controller
{
    protected string CurrentUser => User.GetDisplayName();

    protected void Success(string message) => TempData["Success"] = message;
    protected void Error(string message) => TempData["Error"] = message;

    protected string FirstModelError() =>
        ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).FirstOrDefault(m => !string.IsNullOrEmpty(m))
        ?? "Please check the form and try again.";

    protected static List<SelectListItem> GuestOptions(IEnumerable<Guest> guests) =>
        guests.Select(g => new SelectListItem($"{g.FullName} · {g.Phone}", g.Id.ToString())).ToList();

    protected static List<ViewModels.RoomOption> RoomOptions(IEnumerable<Room> rooms) =>
        rooms.Select(r => new ViewModels.RoomOption(r.Id, r.RoomNumber, r.RoomType?.Name ?? "", r.PricePerNight,
            r.RoomType?.Capacity ?? 1, r.Status)).ToList();
}
