using Microsoft.AspNetCore.Identity;
using SmartHotel.Repositories;
using SmartHotel.ViewModels;

namespace SmartHotel.Services;

public interface IStaffService
{
    Task<PagedResult<Staff>> SearchAsync(string? search, UserRole? role, int page, int pageSize = 10);
    Task<Staff?> GetAsync(int id);
    Task<ServiceResult<Staff>> CreateAsync(StaffFormViewModel model, bool actorIsAdmin);
    Task<ServiceResult> UpdateAsync(StaffFormViewModel model, int actorId, bool actorIsAdmin);
    Task<ServiceResult> DeleteAsync(int id, int actorId, bool actorIsAdmin);
    Task<ServiceResult> ResetPasswordAsync(int id, string newPassword, bool actorIsAdmin);
    Task<ServiceResult<Staff>> ChangePasswordAsync(int id, string currentPassword, string newPassword);
    Task<ServiceResult<Staff>> AuthenticateAsync(string usernameOrEmail, string password);
}

public class StaffService(IRepository<Staff> staff, IPasswordHasher<Staff> hasher) : IStaffService
{
    private const int MaxFailedAttempts = 5;
    private static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(15);

    public Task<PagedResult<Staff>> SearchAsync(string? search, UserRole? role, int page, int pageSize = 10)
    {
        var q = staff.QueryNoTracking();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim();
            q = q.Where(x => x.FullName.Contains(s) || x.Username.Contains(s) || x.Email.Contains(s) ||
                             (x.Phone != null && x.Phone.Contains(s)));
        }
        if (role.HasValue) q = q.Where(x => x.Role == role.Value);
        return q.OrderBy(x => x.FullName).ToPagedAsync(page, pageSize);
    }

    public Task<Staff?> GetAsync(int id) => staff.QueryNoTracking().FirstOrDefaultAsync(x => x.Id == id);

    public async Task<ServiceResult<Staff>> CreateAsync(StaffFormViewModel model, bool actorIsAdmin)
    {
        if (!actorIsAdmin && model.Role == UserRole.Admin)
            return ServiceResult<Staff>.Fail("Only administrators can create administrator accounts.");

        var duplicate = await FindDuplicateAsync(model.Username, model.Email, null);
        if (duplicate is not null) return ServiceResult<Staff>.Fail(duplicate);

        var member = new Staff();
        Map(model, member);
        member.PasswordHash = hasher.HashPassword(member, model.Password!);
        await staff.AddAsync(member);
        await staff.SaveChangesAsync();
        return ServiceResult<Staff>.Ok(member);
    }

    public async Task<ServiceResult> UpdateAsync(StaffFormViewModel model, int actorId, bool actorIsAdmin)
    {
        var member = await staff.GetByIdAsync(model.Id ?? 0);
        if (member is null) return ServiceResult.Fail("Staff member not found.");
        if (!actorIsAdmin && (member.Role == UserRole.Admin || model.Role == UserRole.Admin))
            return ServiceResult.Fail("Only administrators can manage administrator accounts.");
        if (member.Id == actorId && (!model.IsActive || model.Role != member.Role))
            return ServiceResult.Fail("You cannot deactivate your own account or change your own role.");
        if (member.Role == UserRole.Admin && (model.Role != UserRole.Admin || !model.IsActive) && await IsLastActiveAdminAsync(member.Id))
            return ServiceResult.Fail("At least one active administrator is required.");

        var duplicate = await FindDuplicateAsync(model.Username, model.Email, member.Id);
        if (duplicate is not null) return ServiceResult.Fail(duplicate);

        var securityChanged = member.Role != model.Role || member.IsActive != model.IsActive ||
                              !string.Equals(member.Username, model.Username.Trim(), StringComparison.OrdinalIgnoreCase);
        Map(model, member);
        if (!string.IsNullOrWhiteSpace(model.Password))
        {
            member.PasswordHash = hasher.HashPassword(member, model.Password);
            securityChanged = true;
        }
        if (securityChanged) member.SecurityStamp = Guid.NewGuid().ToString("N");

        await staff.SaveChangesAsync();
        return ServiceResult.Ok();
    }

    public async Task<ServiceResult> DeleteAsync(int id, int actorId, bool actorIsAdmin)
    {
        var member = await staff.GetByIdAsync(id);
        if (member is null) return ServiceResult.Fail("Staff member not found.");
        if (member.Id == actorId) return ServiceResult.Fail("You cannot delete your own account.");
        if (!actorIsAdmin && member.Role == UserRole.Admin)
            return ServiceResult.Fail("Only administrators can delete administrator accounts.");
        if (member.Role == UserRole.Admin && await IsLastActiveAdminAsync(member.Id))
            return ServiceResult.Fail("At least one active administrator is required.");

        staff.Remove(member);
        await staff.SaveChangesAsync();
        return ServiceResult.Ok();
    }

    public async Task<ServiceResult> ResetPasswordAsync(int id, string newPassword, bool actorIsAdmin)
    {
        var member = await staff.GetByIdAsync(id);
        if (member is null) return ServiceResult.Fail("Staff member not found.");
        if (!actorIsAdmin && member.Role == UserRole.Admin)
            return ServiceResult.Fail("Only administrators can reset an administrator's password.");

        member.PasswordHash = hasher.HashPassword(member, newPassword);
        member.SecurityStamp = Guid.NewGuid().ToString("N");
        member.FailedLoginAttempts = 0;
        member.LockoutEnd = null;
        await staff.SaveChangesAsync();
        return ServiceResult.Ok();
    }

    public async Task<ServiceResult<Staff>> ChangePasswordAsync(int id, string currentPassword, string newPassword)
    {
        var member = await staff.GetByIdAsync(id);
        if (member is null) return ServiceResult<Staff>.Fail("Account not found.");
        if (hasher.VerifyHashedPassword(member, member.PasswordHash, currentPassword) == PasswordVerificationResult.Failed)
            return ServiceResult<Staff>.Fail("Current password is incorrect.");

        member.PasswordHash = hasher.HashPassword(member, newPassword);
        member.SecurityStamp = Guid.NewGuid().ToString("N");
        await staff.SaveChangesAsync();
        return ServiceResult<Staff>.Ok(member);
    }

    public async Task<ServiceResult<Staff>> AuthenticateAsync(string usernameOrEmail, string password)
    {
        const string invalid = "Invalid username or password.";
        var login = usernameOrEmail.Trim().ToLowerInvariant();
        var member = await staff.Query().FirstOrDefaultAsync(x => x.Username == login || x.Email == login);
        if (member is null)
        {
            // Hash anyway so response time does not reveal whether the account exists.
            hasher.HashPassword(new Staff(), password);
            return ServiceResult<Staff>.Fail(invalid);
        }

        if (member.LockoutEnd > DateTime.Now)
        {
            var minutes = Math.Ceiling((member.LockoutEnd.Value - DateTime.Now).TotalMinutes);
            return ServiceResult<Staff>.Fail($"Account locked after too many failed attempts. Try again in {minutes} minute(s).");
        }

        var result = hasher.VerifyHashedPassword(member, member.PasswordHash, password);
        if (result == PasswordVerificationResult.Failed)
        {
            member.FailedLoginAttempts++;
            if (member.FailedLoginAttempts >= MaxFailedAttempts)
            {
                member.LockoutEnd = DateTime.Now.Add(LockoutDuration);
                member.FailedLoginAttempts = 0;
            }
            await staff.SaveChangesAsync();
            return ServiceResult<Staff>.Fail(invalid);
        }

        if (!member.IsActive) return ServiceResult<Staff>.Fail("Your account is disabled. Contact an administrator.");

        if (result == PasswordVerificationResult.SuccessRehashNeeded)
            member.PasswordHash = hasher.HashPassword(member, password);
        member.FailedLoginAttempts = 0;
        member.LockoutEnd = null;
        member.LastLoginAt = DateTime.Now;
        await staff.SaveChangesAsync();
        return ServiceResult<Staff>.Ok(member);
    }

    private async Task<bool> IsLastActiveAdminAsync(int id) =>
        !await staff.QueryNoTracking().AnyAsync(x => x.Role == UserRole.Admin && x.IsActive && x.Id != id);

    private async Task<string?> FindDuplicateAsync(string username, string email, int? excludeId)
    {
        var u = username.Trim().ToLowerInvariant();
        var e = email.Trim().ToLowerInvariant();
        if (await staff.QueryNoTracking().AnyAsync(x => x.Username == u && x.Id != (excludeId ?? 0)))
            return $"Username '{u}' is already taken.";
        if (await staff.QueryNoTracking().AnyAsync(x => x.Email == e && x.Id != (excludeId ?? 0)))
            return $"Email '{e}' is already used by another account.";
        return null;
    }

    private static void Map(StaffFormViewModel m, Staff s)
    {
        s.FullName = m.FullName.Trim();
        s.Email = m.Email.Trim().ToLowerInvariant();
        s.Phone = m.Phone?.Trim();
        s.Username = m.Username.Trim().ToLowerInvariant();
        s.Role = m.Role;
        s.IsActive = m.IsActive;
        s.HireDate = m.HireDate.Date;
    }
}
