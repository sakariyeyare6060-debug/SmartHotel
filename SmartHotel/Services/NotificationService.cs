using SmartHotel.Repositories;

namespace SmartHotel.Services;

public interface INotificationService
{
    /// <summary>Queues a notification; it is persisted with the caller's next SaveChanges.</summary>
    void Add(NotificationType type, string title, string message, string? link = null);
    Task CreateAsync(NotificationType type, string title, string message, string? link = null);
    Task<int> GetUnreadCountAsync();
    Task<List<Notification>> GetLatestAsync(int count);
    Task<PagedResult<Notification>> GetPagedAsync(bool unreadOnly, int page, int pageSize = 15);
    Task<Notification?> MarkReadAsync(int id);
    Task MarkAllReadAsync();
    Task DeleteAsync(int id);
}

public class NotificationService(IRepository<Notification> notifications, IHttpContextAccessor http) : INotificationService
{
    /// <summary>Maintenance staff only see maintenance notifications; everyone else sees all of them.</summary>
    private IQueryable<Notification> Visible(IQueryable<Notification> q)
    {
        var user = http.HttpContext?.User;
        return user is not null && user.IsInRole(Roles.Maintenance)
            ? q.Where(n => n.Type == NotificationType.Maintenance)
            : q;
    }

    public void Add(NotificationType type, string title, string message, string? link = null) =>
        notifications.Add(new Notification
        {
            Type = type,
            Title = Truncate(title, 150),
            Message = Truncate(message, 500),
            Link = link
        });

    public async Task CreateAsync(NotificationType type, string title, string message, string? link = null)
    {
        Add(type, title, message, link);
        await notifications.SaveChangesAsync();
    }

    public Task<int> GetUnreadCountAsync() => Visible(notifications.QueryNoTracking()).CountAsync(n => !n.IsRead);

    public Task<List<Notification>> GetLatestAsync(int count) =>
        Visible(notifications.QueryNoTracking()).OrderByDescending(n => n.CreatedAt).Take(count).ToListAsync();

    public Task<PagedResult<Notification>> GetPagedAsync(bool unreadOnly, int page, int pageSize = 15)
    {
        var q = Visible(notifications.QueryNoTracking());
        if (unreadOnly) q = q.Where(n => !n.IsRead);
        return q.OrderByDescending(n => n.CreatedAt).ToPagedAsync(page, pageSize);
    }

    public async Task<Notification?> MarkReadAsync(int id)
    {
        var n = await Visible(notifications.Query()).FirstOrDefaultAsync(x => x.Id == id);
        if (n is { IsRead: false })
        {
            n.IsRead = true;
            await notifications.SaveChangesAsync();
        }
        return n;
    }

    public async Task MarkAllReadAsync()
    {
        var unread = await Visible(notifications.Query()).Where(n => !n.IsRead).ToListAsync();
        unread.ForEach(n => n.IsRead = true);
        await notifications.SaveChangesAsync();
    }

    public async Task DeleteAsync(int id)
    {
        var n = await Visible(notifications.Query()).FirstOrDefaultAsync(x => x.Id == id);
        if (n is null) return;
        notifications.Remove(n);
        await notifications.SaveChangesAsync();
    }

    private static string Truncate(string value, int max) => value.Length <= max ? value : value[..max];
}
