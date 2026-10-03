using System.Diagnostics;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartHotel.ViewModels;

namespace SmartHotel.Controllers;

public class HomeController : AppController
{
    public IActionResult Index() =>
        User.IsFrontDesk() ? RedirectToAction("Index", "Dashboard")
        : User.IsHousekeepingStaff() ? RedirectToAction("Index", "Housekeeping")
        : RedirectToAction("Index", "Maintenance");

    [AllowAnonymous, IgnoreAntiforgeryToken]
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error() =>
        View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });

    [AllowAnonymous, IgnoreAntiforgeryToken]
    [Route("/error/{code:int}")]
    public IActionResult StatusPage(int code) =>
        View("StatusPage", new ErrorViewModel { StatusCode = code, RequestId = HttpContext.TraceIdentifier });
}
