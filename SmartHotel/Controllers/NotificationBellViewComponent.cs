using Microsoft.AspNetCore.Mvc;
using SmartHotel.Services;
using SmartHotel.ViewModels;

namespace SmartHotel.Controllers;

public class NotificationBellViewComponent(INotificationService notifications) : ViewComponent
{
    public async Task<IViewComponentResult> InvokeAsync() =>
        View(new NotificationBellViewModel
        {
            UnreadCount = await notifications.GetUnreadCountAsync(),
            Latest = await notifications.GetLatestAsync(5)
        });
}
