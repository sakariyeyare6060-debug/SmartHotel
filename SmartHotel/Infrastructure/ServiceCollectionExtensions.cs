using Microsoft.AspNetCore.Identity;
using SmartHotel.Repositories;
using SmartHotel.Services;

namespace SmartHotel.Infrastructure;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddSmartHotelServices(this IServiceCollection services)
    {
        // Repositories
        services.AddScoped(typeof(IRepository<>), typeof(Repository<>));
        services.AddScoped<IBookingRepository, BookingRepository>();

        services.AddHttpContextAccessor();

        // Business services
        services.AddScoped<INotificationService, NotificationService>();
        services.AddScoped<IHousekeepingService, HousekeepingService>();
        services.AddScoped<IMaintenanceService, MaintenanceService>();
        services.AddScoped<IRoomService, RoomService>();
        services.AddScoped<IGuestService, GuestService>();
        services.AddScoped<IBillingService, BillingService>();
        services.AddScoped<IBookingService, BookingService>();
        services.AddScoped<IStaffService, StaffService>();
        services.AddScoped<IDashboardService, DashboardService>();
        services.AddScoped<IReportService, ReportService>();

        services.AddScoped<IPasswordHasher<Models.Staff>, PasswordHasher<Models.Staff>>();
        return services;
    }
}
