using SmartHotel.Repositories;
using SmartHotel.ViewModels;

namespace SmartHotel.Services;

public interface IRoomService
{
    Task<PagedResult<Room>> SearchAsync(string? search, int? roomTypeId, RoomStatus? status, int page, int pageSize = 10, bool ready = false);
    Task<Room?> GetAsync(int id);
    Task<List<Room>> GetAvailableRoomsAsync(DateTime checkIn, DateTime checkOut, int? excludeBookingId = null);
    Task<ServiceResult<Room>> CreateAsync(RoomFormViewModel model, string user);
    Task<ServiceResult> UpdateAsync(RoomFormViewModel model);
    Task<ServiceResult> DeleteAsync(int id);

    Task<List<RoomType>> GetRoomTypesAsync();
    Task<Dictionary<int, int>> GetRoomCountsByTypeAsync();
    Task<RoomType?> GetRoomTypeAsync(int id);
    Task<ServiceResult> SaveRoomTypeAsync(RoomTypeFormViewModel model);
    Task<ServiceResult> DeleteRoomTypeAsync(int id);
}

public class RoomService(IRepository<Room> rooms, IRepository<RoomType> roomTypes, IBookingRepository bookings) : IRoomService
{
    public Task<PagedResult<Room>> SearchAsync(string? search, int? roomTypeId, RoomStatus? status, int page, int pageSize = 10, bool ready = false)
    {
        var q = rooms.QueryNoTracking().Include(r => r.RoomType).AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim();
            q = q.Where(r => r.RoomNumber.Contains(s) || r.RoomType!.Name.Contains(s) ||
                             (r.Description != null && r.Description.Contains(s)));
        }
        if (roomTypeId.HasValue) q = q.Where(r => r.RoomTypeId == roomTypeId.Value);
        if (status.HasValue) q = q.Where(r => r.Status == status.Value);
        else if (ready) q = q.Where(r => r.Status == RoomStatus.Available || r.Status == RoomStatus.Clean);

        return q.OrderBy(r => r.Floor).ThenBy(r => r.RoomNumber).ToPagedAsync(page, pageSize);
    }

    public Task<Room?> GetAsync(int id) =>
        rooms.QueryNoTracking().Include(r => r.RoomType).FirstOrDefaultAsync(r => r.Id == id);

    public Task<List<Room>> GetAvailableRoomsAsync(DateTime checkIn, DateTime checkOut, int? excludeBookingId = null)
    {
        var from = checkIn.Date;
        var to = checkOut.Date > from ? checkOut.Date : from.AddDays(1);

        var q = rooms.QueryNoTracking().Include(r => r.RoomType)
            .Where(r => r.Status != RoomStatus.Maintenance);

        // A stay starting today needs a room that is physically ready.
        if (from <= DateTime.Today)
            q = q.Where(r => r.Status == RoomStatus.Available || r.Status == RoomStatus.Clean);

        q = q.Where(r => !r.Bookings.Any(b =>
            (excludeBookingId == null || b.Id != excludeBookingId) &&
            b.Status != BookingStatus.Cancelled && b.Status != BookingStatus.CheckedOut &&
            b.CheckInDate < to && from < b.CheckOutDate));

        return q.OrderBy(r => r.Floor).ThenBy(r => r.RoomNumber).ToListAsync();
    }

    public async Task<ServiceResult<Room>> CreateAsync(RoomFormViewModel model, string user)
    {
        var number = model.RoomNumber.Trim().ToUpperInvariant();
        if (await rooms.QueryNoTracking().AnyAsync(r => r.RoomNumber == number))
            return ServiceResult<Room>.Fail($"Room {number} already exists.");
        if (!await roomTypes.QueryNoTracking().AnyAsync(t => t.Id == model.RoomTypeId))
            return ServiceResult<Room>.Fail("Selected room type does not exist.");
        if (model.Status == RoomStatus.Occupied)
            return ServiceResult<Room>.Fail("A new room cannot start as Occupied.");

        var room = new Room
        {
            RoomNumber = number,
            Floor = model.Floor,
            RoomTypeId = model.RoomTypeId!.Value,
            PricePerNight = model.PricePerNight,
            Status = model.Status,
            Description = model.Description?.Trim()
        };
        await rooms.AddAsync(room);
        await rooms.SaveChangesAsync();
        return ServiceResult<Room>.Ok(room);
    }

    public async Task<ServiceResult> UpdateAsync(RoomFormViewModel model)
    {
        var room = await rooms.GetByIdAsync(model.Id ?? 0);
        if (room is null) return ServiceResult.Fail("Room not found.");

        var number = model.RoomNumber.Trim().ToUpperInvariant();
        if (await rooms.QueryNoTracking().AnyAsync(r => r.RoomNumber == number && r.Id != room.Id))
            return ServiceResult.Fail($"Room {number} already exists.");
        if (!await roomTypes.QueryNoTracking().AnyAsync(t => t.Id == model.RoomTypeId))
            return ServiceResult.Fail("Selected room type does not exist.");

        room.RoomNumber = number;
        room.Floor = model.Floor;
        room.RoomTypeId = model.RoomTypeId!.Value;
        room.PricePerNight = model.PricePerNight;
        room.Description = model.Description?.Trim();
        await rooms.SaveChangesAsync();
        return ServiceResult.Ok();
    }

    public async Task<ServiceResult> DeleteAsync(int id)
    {
        var room = await rooms.GetByIdAsync(id);
        if (room is null) return ServiceResult.Fail("Room not found.");

        // Housekeeping logs and repair records go with the room (cascade).
        await bookings.RemoveWithBillingAsync(b => b.RoomId == id);
        rooms.Remove(room);
        await rooms.SaveChangesAsync();
        return ServiceResult.Ok();
    }

    public Task<List<RoomType>> GetRoomTypesAsync() =>
        roomTypes.QueryNoTracking().OrderBy(t => t.BasePrice).ThenBy(t => t.Name).ToListAsync();

    public async Task<Dictionary<int, int>> GetRoomCountsByTypeAsync() =>
        await rooms.QueryNoTracking().GroupBy(r => r.RoomTypeId)
            .Select(g => new { g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Count);

    public Task<RoomType?> GetRoomTypeAsync(int id) => roomTypes.QueryNoTracking().FirstOrDefaultAsync(t => t.Id == id);

    public async Task<ServiceResult> SaveRoomTypeAsync(RoomTypeFormViewModel model)
    {
        var name = model.Name.Trim();
        if (await roomTypes.QueryNoTracking().AnyAsync(t => t.Name == name && t.Id != (model.Id ?? 0)))
            return ServiceResult.Fail($"Room type '{name}' already exists.");

        RoomType? type;
        if (model.Id.HasValue)
        {
            type = await roomTypes.GetByIdAsync(model.Id.Value);
            if (type is null) return ServiceResult.Fail("Room type not found.");
        }
        else
        {
            type = new RoomType();
            roomTypes.Add(type);
        }

        type.Name = name;
        type.Description = model.Description?.Trim();
        type.Amenities = model.Amenities?.Trim();
        type.BasePrice = model.BasePrice;
        type.Capacity = model.Capacity;
        await roomTypes.SaveChangesAsync();
        return ServiceResult.Ok();
    }

    public async Task<ServiceResult> DeleteRoomTypeAsync(int id)
    {
        var type = await roomTypes.Query().Include(t => t.Rooms).FirstOrDefaultAsync(t => t.Id == id);
        if (type is null) return ServiceResult.Fail("Room type not found.");
        await bookings.RemoveWithBillingAsync(b => b.Room!.RoomTypeId == id);
        foreach (var room in type.Rooms) rooms.Remove(room);
        roomTypes.Remove(type);
        await roomTypes.SaveChangesAsync();
        return ServiceResult.Ok();
    }
}
