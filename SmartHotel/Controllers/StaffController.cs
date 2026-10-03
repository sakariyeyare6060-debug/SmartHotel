using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartHotel.Services;
using SmartHotel.ViewModels;

namespace SmartHotel.Controllers;

[Authorize(Policy = Policies.Management)]
public class StaffController(IStaffService staff) : AppController
{
    public async Task<IActionResult> Index(string? search, UserRole? role, int page = 1) =>
        View(new StaffListViewModel { Search = search, Role = role, Staff = await staff.SearchAsync(search, role, page) });

    [HttpGet]
    public IActionResult Create() => View("Form", new StaffFormViewModel());

    [HttpPost]
    public async Task<IActionResult> Create(StaffFormViewModel model)
    {
        model.Id = null;
        if (!ModelState.IsValid) return View("Form", model);

        var result = await staff.CreateAsync(model, User.IsAdmin());
        if (!result.Succeeded)
        {
            ModelState.AddModelError(string.Empty, result.Error!);
            return View("Form", model);
        }
        Success($"Account '{result.Value!.Username}' created for {result.Value.FullName}.");
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var s = await staff.GetAsync(id);
        if (s is null) return NotFound();
        return View("Form", new StaffFormViewModel
        {
            Id = s.Id, FullName = s.FullName, Email = s.Email, Phone = s.Phone, Username = s.Username,
            Role = s.Role, IsActive = s.IsActive, HireDate = s.HireDate
        });
    }

    [HttpPost]
    public async Task<IActionResult> Edit(int id, StaffFormViewModel model)
    {
        model.Id = id;
        if (!ModelState.IsValid) return View("Form", model);

        var result = await staff.UpdateAsync(model, User.GetUserId(), User.IsAdmin());
        if (!result.Succeeded)
        {
            ModelState.AddModelError(string.Empty, result.Error!);
            return View("Form", model);
        }
        Success($"{model.FullName} updated.");
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public async Task<IActionResult> Delete(int id)
    {
        var result = await staff.DeleteAsync(id, User.GetUserId(), User.IsAdmin());
        if (result.Succeeded) Success("Staff account deleted.");
        else Error(result.Error!);
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> ResetPassword(int id)
    {
        var s = await staff.GetAsync(id);
        return s is null ? NotFound() : View(new ResetPasswordViewModel { Id = s.Id, FullName = s.FullName });
    }

    [HttpPost]
    public async Task<IActionResult> ResetPassword(ResetPasswordViewModel model)
    {
        if (!ModelState.IsValid) return View(model);

        var result = await staff.ResetPasswordAsync(model.Id, model.NewPassword, User.IsAdmin());
        if (!result.Succeeded)
        {
            ModelState.AddModelError(string.Empty, result.Error!);
            return View(model);
        }
        Success($"Password reset for {model.FullName}. Their active sessions were signed out.");
        return RedirectToAction(nameof(Index));
    }
}
