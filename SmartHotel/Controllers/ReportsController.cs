using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartHotel.Services;
using SmartHotel.ViewModels;

namespace SmartHotel.Controllers;

[Authorize(Policy = Policies.Management)]
public class ReportsController(IReportService reports) : AppController
{
    public async Task<IActionResult> Index([FromQuery] ReportFilter filter) => View(await reports.GenerateAsync(filter));

    public async Task<IActionResult> Export([FromQuery] ReportFilter filter, string kind = "bookings")
    {
        var (fileName, content) = await reports.ExportCsvAsync(filter, kind);
        return File(content, "text/csv", fileName);
    }
}
