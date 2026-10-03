using SmartHotel.Repositories;

namespace SmartHotel.Services;

public interface IHousekeepingService
{
    Task<Dictionary<RoomStatus, int>> GetStatusCountsAsync();
    Task<List<Room>> GetRoomsAsync(RoomStatus? status, int? floor);
    Task<List<int>> GetFloorsAsync();
    Task<List<HousekeepingLog>> GetRecentLogsAsync(int count);
    Task<ServiceResult> UpdateStatusAsync(int roomId, RoomStatus status, string? note, string user);

    /// <summary>Changes a tracked room's status and records an audit entry (no save).</summary>
    void TrackStatusChange(Room room, RoomStatus status, string? note, string user);
}

public class HousekeepingService(
    IRepository<Room> rooms,
    IRepository<HousekeepingLog> logs,
    INotificationService notifications) : IHousekeepingService
{
    public async Task<Dictionary<RoomStatus, int>> GetStatusCountsAsync()
    {
        var counts = await rooms.QueryNoTracking()
            .GroupBy(r => r.Status)
            .Select(g => new { g.Key, Count = g.Count() })
            .ToListAsync();

        return Enum.GetValues<RoomStatus>()
            .ToDictionary(s => s, s => counts.FirstOrDefault(c => c.Key == s)?.Count ?? 0);
    }

    public Task<List<Room>> GetRoomsAsync(RoomStatus? status, int? floor)
    {
        var q = rooms.QueryNoTracking().Include(r => r.RoomType).AsQueryable();
        if (status.HasValue) q = q.Where(r => r.Status == status.Value);
        if (floor.HasValue) q = q.Where(r => r.Floor == floor.Value);
        return q.OrderBy(r => r.Floor).ThenBy(r => r.RoomNumber).ToListAsync();
    }

    public Task<List<int>> GetFloorsAsync() =>
        rooms.QueryNoTracking().Select(r => r.Floor).Distinct().OrderBy(f => f).ToListAsync();

    public Task<List<HousekeepingLog>> GetRecentLogsAsync(int count) =>
        logs.QueryNoTracking().Include(l => l.Room)
            .OrderByDescending(l => l.CreatedAt).Take(count).ToListAsync();

    public async Task<ServiceResult> UpdateStatusAsync(int roomId, RoomStatus status, string? note, string user)
    {
        var room = await rooms.GetByIdAsync(roomId);
        if (room is null) return ServiceResult.Fail("Room not found.");
        if (status == RoomStatus.Occupied)
            return ServiceResult.Fail("Rooms become Occupied automatically when a guest checks in.");
        if (room.Status == RoomStatus.Occupied)
            return ServiceResult.Fail($"Room {room.RoomNumber} is occupied. Check the guest out first.");
        if (room.Status == status) return ServiceResult.Ok();

        var previous = room.Status;
        TrackStatusChange(room, status, note, user);

        if (status == RoomStatus.Maintenance)
            notifications.Add(NotificationType.Maintenance, "Maintenance request",
                $"Maintenance request - Room {room.RoomNumber}{(string.IsNullOrWhiteSpace(note) ? "" : ": " + note.Trim())}",
                "/Housekeeping?status=Maintenance");
        else if (previous == RoomStatus.Maintenance)
            notifications.Add(NotificationType.Maintenance, "Maintenance completed",
                $"Room {room.RoomNumber} is back in service ({status.DisplayName()})", "/Housekeeping");
        else if (status == RoomStatus.Dirty)
            notifications.Add(NotificationType.Housekeeping, "Room needs cleaning",
                $"Room {room.RoomNumber} marked as Dirty", "/Housekeeping?status=Dirty");

        await rooms.SaveChangesAsync();
        return ServiceResult.Ok();
    }

    public void TrackStatusChange(Room room, RoomStatus status, string? note, string user)
    {
        if (room.Status == status) return;

        logs.Add(new HousekeepingLog
        {
            Room = room,
            RoomId = room.Id,
            FromStatus = room.Status,
            ToStatus = status,
            Note = string.IsNullOrWhiteSpace(note) ? null : note.Trim()[..Math.Min(note.Trim().Length, 250)],
            ChangedBy = user
        });
        room.Status = status;
    }
}
