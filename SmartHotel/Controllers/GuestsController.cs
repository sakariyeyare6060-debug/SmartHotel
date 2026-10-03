using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartHotel.Services;
using SmartHotel.ViewModels;

namespace SmartHotel.Controllers;

[Authorize(Policy = Policies.FrontDesk)]
public class GuestsController(IGuestService guests) : AppController
{
    public async Task<IActionResult> Index(string? search, int page = 1) =>
        View(new GuestListViewModel { Search = search, Guests = await guests.SearchAsync(search, page) });

    public async Task<IActionResult> Details(int id)
    {
        var vm = await guests.GetDetailsAsync(id);
        return vm is null ? NotFound() : View(vm);
    }

    [HttpGet]
    public IActionResult Create(string? returnUrl = null)
    {
        ViewData["ReturnUrl"] = returnUrl;
        return View("Form", new GuestFormViewModel());
    }

    [HttpPost]
    public async Task<IActionResult> Create(GuestFormViewModel model, string? returnUrl = null)
    {
        model.Id = null;
        ViewData["ReturnUrl"] = returnUrl;
        if (!ModelState.IsValid) return View("Form", model);

        var result = await guests.CreateAsync(model);
        if (!result.Succeeded)
        {
            ModelState.AddModelError(string.Empty, result.Error!);
            return View("Form", model);
        }

        Success($"Guest {result.Value!.FullName} was registered.");
        if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
            return Redirect(returnUrl + (returnUrl.Contains('?') ? "&" : "?") + "guestId=" + result.Value.Id);
        return RedirectToAction(nameof(Details), new { id = result.Value.Id });
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var g = await guests.GetAsync(id);
        if (g is null) return NotFound();

        return View("Form", new GuestFormViewModel
        {
            Id = g.Id, FirstName = g.FirstName, LastName = g.LastName, Email = g.Email, Phone = g.Phone,
            IdType = g.IdType, IdNumber = g.IdNumber, Nationality = g.Nationality, Address = g.Address,
            DateOfBirth = g.DateOfBirth, Notes = g.Notes
        });
    }

    [HttpPost]
    public async Task<IActionResult> Edit(int id, GuestFormViewModel model)
    {
        model.Id = id;
        if (!ModelState.IsValid) return View("Form", model);

        var result = await guests.UpdateAsync(model);
        if (!result.Succeeded)
        {
            ModelState.AddModelError(string.Empty, result.Error!);
            return View("Form", model);
        }
        Success("Guest details updated.");
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost, Authorize(Policy = Policies.Management)]
    public async Task<IActionResult> Delete(int id)
    {
        var result = await guests.DeleteAsync(id);
        if (result.Succeeded) Success("Guest deleted.");
        else Error(result.Error!);
        return RedirectToAction(nameof(Index));
    }
}
