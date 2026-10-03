using SmartHotel.Repositories;
using SmartHotel.ViewModels;

namespace SmartHotel.Services;

public interface IMaintenanceService
{
    Task<List<MaintenanceRoom>> GetRoomsAsync();
    Task<List<MaintenanceRecord>> GetRecentRepairsAsync(int count);
    Task<(int Count, decimal Cost)> GetMonthTotalsAsync();

    /// <summary>Records the repair and hands the room to housekeeping for cleaning.</summary>
    Task<ServiceResult<MaintenanceRecord>> ResolveAsync(RepairFormViewModel model, string user);
}

public class MaintenanceService(
    IRepository<Room> rooms,
    IRepository<HousekeepingLog> logs,
    IRepository<MaintenanceRecord> records,
    IHousekeepingService housekeeping,
    INotificationService notifications) : IMaintenanceService
{
    public async Task<List<MaintenanceRoom>> GetRoomsAsync()
    {
        var list = await rooms.QueryNoTracking().Include(r => r.RoomType)
            .Where(r => r.Status == RoomStatus.Maintenance)
            .OrderBy(r => r.Floor).ThenBy(r => r.RoomNumber)
            .ToListAsync();

        var ids = list.Select(r => r.Id).ToList();
        var reports = await logs.QueryNoTracking()
            .Where(l => ids.Contains(l.RoomId) && l.ToStatus == RoomStatus.Maintenance)
            .ToListAsync();

        return list.Select(r =>
        {
            var log = reports.Where(l => l.RoomId == r.Id).MaxBy(l => l.CreatedAt);
            return new MaintenanceRoom(r, log?.Note, log?.ChangedBy, log?.CreatedAt);
        }).ToList();
    }

    public Task<List<MaintenanceRecord>> GetRecentRepairsAsync(int count) =>
        records.QueryNoTracking().Include(m => m.Room)
            .OrderByDescending(m => m.CreatedAt).Take(count).ToListAsync();

    public async Task<(int Count, decimal Cost)> GetMonthTotalsAsync()
    {
        var monthStart = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
        var month = records.QueryNoTracking().Where(m => m.CreatedAt >= monthStart);
        return (await month.CountAsync(), await month.SumAsync(m => (decimal?)m.Cost) ?? 0);
    }

    public async Task<ServiceResult<MaintenanceRecord>> ResolveAsync(RepairFormViewModel model, string user)
    {
        var room = await rooms.GetByIdAsync(model.RoomId);
        if (room is null) return ServiceResult<MaintenanceRecord>.Fail("Room not found.");
        if (room.Status != RoomStatus.Maintenance)
            return ServiceResult<MaintenanceRecord>.Fail($"Room {room.RoomNumber} is no longer under maintenance.");

        var report = await logs.QueryNoTracking()
            .Where(l => l.RoomId == room.Id && l.ToStatus == RoomStatus.Maintenance)
            .OrderByDescending(l => l.CreatedAt).FirstOrDefaultAsync();

        var faultType = model.FaultType!.Value;
        var record = new MaintenanceRecord
        {
            Room = room,
            RoomId = room.Id,
            FaultType = faultType,
            FaultDescription = model.FaultDescription.Trim(),
            WorkDone = string.IsNullOrWhiteSpace(model.WorkDone) ? null : model.WorkDone.Trim(),
            Cost = Math.Round(model.Cost, 2),
            ReportedIssue = report?.Note,
            ReportedAt = report?.CreatedAt,
            ResolvedBy = user
        };
        records.Add(record);

        // A repaired room is cleaned before it goes back on sale.
        housekeeping.TrackStatusChange(room, RoomStatus.Dirty, $"Repaired ({faultType.DisplayName()})", user);
        notifications.Add(NotificationType.Maintenance, "Repair completed",
            $"Room {room.RoomNumber} repaired - {faultType.DisplayName()}, cost {Ui.Money(record.Cost)}. Needs cleaning.",
            "/Housekeeping?status=Dirty");

        await records.SaveChangesAsync();
        return ServiceResult<MaintenanceRecord>.Ok(record);
    }
}
