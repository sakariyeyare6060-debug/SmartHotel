using Microsoft.AspNetCore.Mvc;
using SmartHotel.Services;
using SmartHotel.ViewModels;

namespace SmartHotel.Controllers;

public class NotificationsController(INotificationService notifications) : AppController
{
    public async Task<IActionResult> Index(bool unread = false, int page = 1) =>
        View(new NotificationListViewModel
        {
            UnreadOnly = unread,
            Notifications = await notifications.GetPagedAsync(unread, page),
            UnreadCount = await notifications.GetUnreadCountAsync()
        });

    [HttpPost]
    public async Task<IActionResult> Open(int id)
    {
        var n = await notifications.MarkReadAsync(id);
        // Maintenance staff work from their own page, not Housekeeping.
        if (n?.Type == NotificationType.Maintenance && !User.IsHousekeepingStaff()) return RedirectToAction("Index", "Maintenance");
        return n?.Link is { } link && Url.IsLocalUrl(link) ? Redirect(link) : RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public async Task<IActionResult> MarkAllRead()
    {
        await notifications.MarkAllReadAsync();
        Success("All notifications marked as read.");
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public async Task<IActionResult> Delete(int id)
    {
        await notifications.DeleteAsync(id);
        return RedirectToAction(nameof(Index));
    }
}
