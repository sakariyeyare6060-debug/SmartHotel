using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using SmartHotel.Services;
using SmartHotel.ViewModels;

namespace SmartHotel.Controllers;

public class AccountController(IStaffService staff, ILogger<AccountController> logger) : AppController
{
    [HttpGet, AllowAnonymous]
    public IActionResult Login(string? returnUrl = null)
    {
        if (User.Identity?.IsAuthenticated == true) return RedirectToLocal(returnUrl);
        return View(new LoginViewModel { ReturnUrl = returnUrl });
    }

    [HttpPost, AllowAnonymous, EnableRateLimiting("login")]
    public async Task<IActionResult> Login(LoginViewModel model)
    {
        if (!ModelState.IsValid) return View(model);

        var result = await staff.AuthenticateAsync(model.Username, model.Password);
        if (!result.Succeeded)
        {
            logger.LogWarning("Failed login attempt for {Username} from {Ip}", model.Username, HttpContext.Connection.RemoteIpAddress);
            ModelState.AddModelError(string.Empty, result.Error!);
            model.Password = string.Empty;
            return View(model);
        }

        await SignInAsync(result.Value!, model.RememberMe);
        logger.LogInformation("{Username} signed in", result.Value!.Username);
        return RedirectToLocal(model.ReturnUrl);
    }

    [HttpPost]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToAction(nameof(Login));
    }

    [HttpGet, AllowAnonymous]
    public IActionResult AccessDenied() => View();

    [HttpGet]
    public IActionResult ChangePassword() => View(new ChangePasswordViewModel());

    [HttpPost]
    public async Task<IActionResult> ChangePassword(ChangePasswordViewModel model)
    {
        if (!ModelState.IsValid) return View(model);

        var result = await staff.ChangePasswordAsync(User.GetUserId(), model.CurrentPassword, model.NewPassword);
        if (!result.Succeeded)
        {
            ModelState.AddModelError(nameof(model.CurrentPassword), result.Error!);
            return View(new ChangePasswordViewModel());
        }

        // The security stamp changed, so refresh this session's cookie.
        await SignInAsync(result.Value!, persistent: false);
        Success("Your password has been changed.");
        return RedirectToAction("Index", "Home");
    }

    private Task SignInAsync(Staff member, bool persistent) =>
        HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme,
            ClaimsPrincipalExtensions.CreatePrincipal(member),
            new AuthenticationProperties { IsPersistent = persistent, AllowRefresh = true });

    private IActionResult RedirectToLocal(string? returnUrl) =>
        !string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl)
            ? Redirect(returnUrl)
            : RedirectToAction("Index", "Home");
}
