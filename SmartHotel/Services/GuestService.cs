using SmartHotel.Repositories;
using SmartHotel.ViewModels;

namespace SmartHotel.Services;

public interface IGuestService
{
    Task<PagedResult<Guest>> SearchAsync(string? search, int page, int pageSize = 10);
    Task<List<Guest>> GetAllForSelectAsync();
    Task<Guest?> GetAsync(int id);
    Task<GuestDetailsViewModel?> GetDetailsAsync(int id);
    Task<ServiceResult<Guest>> CreateAsync(GuestFormViewModel model);
    Task<ServiceResult> UpdateAsync(GuestFormViewModel model);
    Task<ServiceResult> DeleteAsync(int id);
}

public class GuestService(IRepository<Guest> guests, IBookingRepository bookings) : IGuestService
{
    public Task<PagedResult<Guest>> SearchAsync(string? search, int page, int pageSize = 10)
    {
        var q = guests.QueryNoTracking();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim();
            q = q.Where(g => g.FirstName.Contains(s) || g.LastName.Contains(s) ||
                             (g.FirstName + " " + g.LastName).Contains(s) ||
                             g.Phone.Contains(s) ||
                             (g.Email != null && g.Email.Contains(s)) ||
                             (g.IdNumber != null && g.IdNumber.Contains(s)));
        }
        return q.OrderBy(g => g.Id).ToPagedAsync(page, pageSize);
    }

    public Task<List<Guest>> GetAllForSelectAsync() =>
        guests.QueryNoTracking().OrderBy(g => g.FirstName).ThenBy(g => g.LastName).ToListAsync();

    public Task<Guest?> GetAsync(int id) => guests.QueryNoTracking().FirstOrDefaultAsync(g => g.Id == id);

    public async Task<GuestDetailsViewModel?> GetDetailsAsync(int id)
    {
        var guest = await GetAsync(id);
        if (guest is null) return null;

        var history = await bookings.QueryWithDetails().AsNoTracking()
            .Where(b => b.GuestId == id)
            .OrderByDescending(b => b.CheckInDate)
            .ToListAsync();

        return new GuestDetailsViewModel { Guest = guest, Bookings = history };
    }

    public async Task<ServiceResult<Guest>> CreateAsync(GuestFormViewModel model)
    {
        var duplicate = await FindDuplicateAsync(model, null);
        if (duplicate is not null) return ServiceResult<Guest>.Fail(duplicate);

        var guest = new Guest();
        Map(model, guest);
        await guests.AddAsync(guest);
        await guests.SaveChangesAsync();
        return ServiceResult<Guest>.Ok(guest);
    }

    public async Task<ServiceResult> UpdateAsync(GuestFormViewModel model)
    {
        var guest = await guests.GetByIdAsync(model.Id ?? 0);
        if (guest is null) return ServiceResult.Fail("Guest not found.");

        var duplicate = await FindDuplicateAsync(model, guest.Id);
        if (duplicate is not null) return ServiceResult.Fail(duplicate);

        Map(model, guest);
        await guests.SaveChangesAsync();
        return ServiceResult.Ok();
    }

    public async Task<ServiceResult> DeleteAsync(int id)
    {
        var guest = await guests.GetByIdAsync(id);
        if (guest is null) return ServiceResult.Fail("Guest not found.");
        await bookings.RemoveWithBillingAsync(b => b.GuestId == id);
        guests.Remove(guest);
        await guests.SaveChangesAsync();
        return ServiceResult.Ok();
    }

    private async Task<string?> FindDuplicateAsync(GuestFormViewModel m, int? excludeId)
    {
        var email = m.Email?.Trim();
        if (!string.IsNullOrEmpty(email) &&
            await guests.QueryNoTracking().AnyAsync(g => g.Email == email && g.Id != (excludeId ?? 0)))
            return $"A guest with email {email} already exists.";

        var idNumber = m.IdNumber?.Trim();
        if (!string.IsNullOrEmpty(idNumber) &&
            await guests.QueryNoTracking().AnyAsync(g => g.IdNumber == idNumber && g.Id != (excludeId ?? 0)))
            return $"A guest with ID number {idNumber} already exists.";

        return null;
    }

    private static void Map(GuestFormViewModel m, Guest g)
    {
        g.FirstName = m.FirstName.Trim();
        g.LastName = m.LastName.Trim();
        g.Email = string.IsNullOrWhiteSpace(m.Email) ? null : m.Email.Trim().ToLowerInvariant();
        g.Phone = m.Phone.Trim();
        g.IdType = m.IdType?.Trim();
        g.IdNumber = string.IsNullOrWhiteSpace(m.IdNumber) ? null : m.IdNumber.Trim();
        g.Nationality = m.Nationality?.Trim();
        g.Address = m.Address?.Trim();
        g.DateOfBirth = m.DateOfBirth?.Date;
        g.Notes = m.Notes?.Trim();
    }
}
