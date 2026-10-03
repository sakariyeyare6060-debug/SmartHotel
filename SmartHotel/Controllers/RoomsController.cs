using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartHotel.Services;
using SmartHotel.ViewModels;

namespace SmartHotel.Controllers;

[Authorize(Policy = Policies.FrontDesk)]
public class RoomsController(IRoomService rooms) : AppController
{
    public async Task<IActionResult> Index(string? search, int? roomTypeId, RoomStatus? status, bool ready = false, int page = 1) =>
        View(new RoomListViewModel
        {
            Search = search,
            RoomTypeId = roomTypeId,
            Status = status,
            Ready = ready && !status.HasValue,
            Rooms = await rooms.SearchAsync(search, roomTypeId, status, page, ready: ready),
            RoomTypes = await rooms.GetRoomTypesAsync()
        });

    [HttpGet, Authorize(Policy = Policies.Management)]
    public async Task<IActionResult> Create() => View("Form", await PopulateAsync(new RoomFormViewModel()));

    [HttpPost, Authorize(Policy = Policies.Management)]
    public async Task<IActionResult> Create(RoomFormViewModel model)
    {
        model.Id = null;
        if (!ModelState.IsValid) return View("Form", await PopulateAsync(model));

        var result = await rooms.CreateAsync(model, CurrentUser);
        if (!result.Succeeded)
        {
            ModelState.AddModelError(string.Empty, result.Error!);
            return View("Form", await PopulateAsync(model));
        }
        Success($"Room {result.Value!.RoomNumber} was added.");
        return RedirectToAction(nameof(Index));
    }

    [HttpGet, Authorize(Policy = Policies.Management)]
    public async Task<IActionResult> Edit(int id)
    {
        var room = await rooms.GetAsync(id);
        if (room is null) return NotFound();

        return View("Form", await PopulateAsync(new RoomFormViewModel
        {
            Id = room.Id,
            RoomNumber = room.RoomNumber,
            Floor = room.Floor,
            RoomTypeId = room.RoomTypeId,
            PricePerNight = room.PricePerNight,
            Status = room.Status,
            Description = room.Description
        }));
    }

    [HttpPost, Authorize(Policy = Policies.Management)]
    public async Task<IActionResult> Edit(int id, RoomFormViewModel model)
    {
        model.Id = id;
        if (!ModelState.IsValid) return View("Form", await PopulateAsync(model));

        var result = await rooms.UpdateAsync(model);
        if (!result.Succeeded)
        {
            ModelState.AddModelError(string.Empty, result.Error!);
            return View("Form", await PopulateAsync(model));
        }
        Success($"Room {model.RoomNumber.ToUpperInvariant()} was updated.");
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, Authorize(Policy = Policies.Management)]
    public async Task<IActionResult> Delete(int id)
    {
        var result = await rooms.DeleteAsync(id);
        if (result.Succeeded) Success("Room deleted.");
        else Error(result.Error!);
        return RedirectToAction(nameof(Index));
    }

    // ----- Room types -----

    [HttpGet, Authorize(Policy = Policies.Management)]
    public async Task<IActionResult> Types(int? editId)
    {
        var form = new RoomTypeFormViewModel();
        if (editId.HasValue && await rooms.GetRoomTypeAsync(editId.Value) is { } type)
        {
            form = new RoomTypeFormViewModel
            {
                Id = type.Id, Name = type.Name, Description = type.Description, Amenities = type.Amenities,
                BasePrice = type.BasePrice, Capacity = type.Capacity
            };
        }
        return View(await BuildTypesAsync(form));
    }

    [HttpPost, Authorize(Policy = Policies.Management)]
    public async Task<IActionResult> SaveType([Bind(Prefix = "Form")] RoomTypeFormViewModel model)
    {
        if (!ModelState.IsValid) return View("Types", await BuildTypesAsync(model));

        var result = await rooms.SaveRoomTypeAsync(model);
        if (!result.Succeeded)
        {
            ModelState.AddModelError(string.Empty, result.Error!);
            return View("Types", await BuildTypesAsync(model));
        }
        Success($"Room type '{model.Name}' saved.");
        return RedirectToAction(nameof(Types));
    }

    [HttpPost, Authorize(Policy = Policies.Management)]
    public async Task<IActionResult> DeleteType(int id)
    {
        var result = await rooms.DeleteRoomTypeAsync(id);
        if (result.Succeeded) Success("Room type deleted.");
        else Error(result.Error!);
        return RedirectToAction(nameof(Types));
    }

    private async Task<RoomFormViewModel> PopulateAsync(RoomFormViewModel model)
    {
        model.RoomTypes = await rooms.GetRoomTypesAsync();
        return model;
    }

    private async Task<RoomTypesViewModel> BuildTypesAsync(RoomTypeFormViewModel form) => new()
    {
        Types = await rooms.GetRoomTypesAsync(),
        RoomCounts = await rooms.GetRoomCountsByTypeAsync(),
        Form = form
    };
}
