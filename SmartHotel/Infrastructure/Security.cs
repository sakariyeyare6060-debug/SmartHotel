using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using SmartHotel.Data;

namespace SmartHotel.Infrastructure;

public static class Roles
{
    public const string Admin = nameof(UserRole.Admin);
    public const string Manager = nameof(UserRole.Manager);
    public const string Receptionist = nameof(UserRole.Receptionist);
    public const string Housekeeping = nameof(UserRole.Housekeeping);
    public const string Maintenance = nameof(UserRole.Maintenance);
}

public static class Policies
{
    public const string AdminOnly = "AdminOnly";
    public const string Management = "Management";
    public const string FrontDesk = "FrontDesk";
    public const string Housekeeping = "Housekeeping";
    public const string Maintenance = "Maintenance";

    public static readonly string[] ManagementRoles = [Roles.Admin, Roles.Manager];
    public static readonly string[] FrontDeskRoles = [Roles.Admin, Roles.Manager, Roles.Receptionist];
    public static readonly string[] HousekeepingRoles = [Roles.Admin, Roles.Manager, Roles.Receptionist, Roles.Housekeeping];
    public static readonly string[] MaintenanceRoles = [Roles.Admin, Roles.Manager, Roles.Maintenance];

    public static void Configure(AuthorizationOptions options)
    {
        options.AddPolicy(AdminOnly, p => p.RequireRole(Roles.Admin));
        options.AddPolicy(Management, p => p.RequireRole(ManagementRoles));
        options.AddPolicy(FrontDesk, p => p.RequireRole(FrontDeskRoles));
        options.AddPolicy(Housekeeping, p => p.RequireRole(HousekeepingRoles));
        options.AddPolicy(Maintenance, p => p.RequireRole(MaintenanceRoles));

        // Everything requires a signed-in user unless marked [AllowAnonymous].
        options.FallbackPolicy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build();
    }
}

public static class AppClaims
{
    public const string FullName = "sh:fullname";
    public const string SecurityStamp = "sh:stamp";
}

public static class ClaimsPrincipalExtensions
{
    public static int GetUserId(this ClaimsPrincipal user) =>
        int.TryParse(user.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : 0;

    public static string GetDisplayName(this ClaimsPrincipal user) =>
        user.FindFirstValue(AppClaims.FullName) ?? user.Identity?.Name ?? "User";

    public static string GetRole(this ClaimsPrincipal user) => user.FindFirstValue(ClaimTypes.Role) ?? string.Empty;

    public static bool IsAdmin(this ClaimsPrincipal user) => user.IsInRole(Roles.Admin);
    public static bool IsManagement(this ClaimsPrincipal user) => Policies.ManagementRoles.Any(user.IsInRole);
    public static bool IsFrontDesk(this ClaimsPrincipal user) => Policies.FrontDeskRoles.Any(user.IsInRole);
    public static bool IsHousekeepingStaff(this ClaimsPrincipal user) => Policies.HousekeepingRoles.Any(user.IsInRole);
    public static bool IsMaintenanceStaff(this ClaimsPrincipal user) => Policies.MaintenanceRoles.Any(user.IsInRole);

    public static ClaimsPrincipal CreatePrincipal(Staff staff)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, staff.Id.ToString()),
            new(ClaimTypes.Name, staff.Username),
            new(ClaimTypes.Email, staff.Email),
            new(ClaimTypes.Role, staff.Role.ToString()),
            new(AppClaims.FullName, staff.FullName),
            new(AppClaims.SecurityStamp, staff.SecurityStamp)
        };
        return new ClaimsPrincipal(new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme));
    }
}

/// <summary>
/// Re-validates the auth cookie on every request: a disabled account, a changed role
/// or a reset password (all rotate the security stamp) immediately ends old sessions.
/// </summary>
public static class StaffPrincipalValidator
{
    public static async Task ValidateAsync(CookieValidatePrincipalContext context)
    {
        var principal = context.Principal;
        var id = principal?.GetUserId() ?? 0;
        var stamp = principal?.FindFirstValue(AppClaims.SecurityStamp);

        var db = context.HttpContext.RequestServices.GetRequiredService<AppDbContext>();
        var staff = id == 0
            ? null
            : await db.Staff.AsNoTracking()
                .Where(s => s.Id == id)
                .Select(s => new { s.IsActive, s.SecurityStamp })
                .FirstOrDefaultAsync();

        if (staff is null || !staff.IsActive || staff.SecurityStamp != stamp)
        {
            context.RejectPrincipal();
            await context.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        }
    }
}
