using System.Globalization;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using SmartHotel.Data;

var builder = WebApplication.CreateBuilder(args);

// ---------- Configuration ----------
builder.Services.Configure<HotelSettings>(builder.Configuration.GetSection("Hotel"));

// ---------- Database ----------
var useInMemory = builder.Configuration.GetValue<bool>("Database:UseInMemory");
builder.Services.AddDbContext<AppDbContext>(options =>
{
    if (useInMemory)
    {
        options.UseInMemoryDatabase("SmartHotelDemo");
    }
    else
    {
        var connection = builder.Configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Connection string 'DefaultConnection' is missing.");
        options.UseSqlServer(connection, sql => sql.EnableRetryOnFailure(3));
    }
});

// ---------- Application services ----------
builder.Services.AddSmartHotelServices();

// ---------- Authentication & authorization ----------
static bool IsApiRequest(HttpRequest request) => request.Path.StartsWithSegments("/api");

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Account/Login";
        options.LogoutPath = "/Account/Logout";
        options.AccessDeniedPath = "/Account/AccessDenied";
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
        options.SlidingExpiration = true;
        options.Cookie.Name = "SmartHotel.Auth";
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.Cookie.SecurePolicy = builder.Environment.IsDevelopment()
            ? CookieSecurePolicy.SameAsRequest
            : CookieSecurePolicy.Always;
        options.Events = new CookieAuthenticationEvents
        {
            OnValidatePrincipal = StaffPrincipalValidator.ValidateAsync,
            OnRedirectToLogin = ctx =>
            {
                if (IsApiRequest(ctx.Request)) ctx.Response.StatusCode = StatusCodes.Status401Unauthorized;
                else ctx.Response.Redirect(ctx.RedirectUri);
                return Task.CompletedTask;
            },
            OnRedirectToAccessDenied = ctx =>
            {
                if (IsApiRequest(ctx.Request)) ctx.Response.StatusCode = StatusCodes.Status403Forbidden;
                else ctx.Response.Redirect(ctx.RedirectUri);
                return Task.CompletedTask;
            }
        };
    });

builder.Services.AddAuthorization(Policies.Configure);

builder.Services.AddAntiforgery(options => options.HeaderName = "X-CSRF-TOKEN");

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("login", ctx => RateLimitPartition.GetFixedWindowLimiter(
        ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 10, Window = TimeSpan.FromMinutes(1) }));
});

// ---------- MVC + API ----------
builder.Services.AddControllersWithViews(options =>
        options.Filters.Add(new AutoValidateAntiforgeryTokenAttribute()))
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
        options.JsonSerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;
    });

var app = builder.Build();

// ---------- Culture (consistent number/date formats) ----------
var culture = new CultureInfo("en-US");
CultureInfo.DefaultThreadCurrentCulture = culture;
CultureInfo.DefaultThreadCurrentUICulture = culture;
Ui.Currency = app.Services.GetRequiredService<IOptions<HotelSettings>>().Value.Currency;

// ---------- Database migration & seed ----------
await InitializeDatabaseAsync(app);

// ---------- HTTP pipeline ----------
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseWhen(ctx => !IsApiRequest(ctx.Request),
    branch => branch.UseStatusCodePagesWithReExecute("/error/{0}"));

app.UseHttpsRedirection();

app.Use(async (context, next) =>
{
    var headers = context.Response.Headers;
    headers["X-Content-Type-Options"] = "nosniff";
    headers["X-Frame-Options"] = "DENY";
    headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
    headers["Permissions-Policy"] = "camera=(), microphone=(), geolocation=()";
    await next();
});

app.UseStaticFiles();
app.UseRequestLocalization(new RequestLocalizationOptions
{
    DefaultRequestCulture = new RequestCulture(culture),
    SupportedCultures = [culture],
    SupportedUICultures = [culture]
});
app.UseRouting();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapControllerRoute(name: "default", pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();

static async Task InitializeDatabaseAsync(WebApplication app)
{
    using var scope = app.Services.CreateScope();
    var services = scope.ServiceProvider;
    var logger = services.GetRequiredService<ILoggerFactory>().CreateLogger("DatabaseInit");
    var config = services.GetRequiredService<IConfiguration>();
    var db = services.GetRequiredService<AppDbContext>();

    try
    {
        if (db.Database.IsRelational())
        {
            if (config.GetValue("Database:MigrateOnStartup", true))
            {
                logger.LogInformation("Applying database migrations...");
                await db.Database.MigrateAsync();
            }
        }
        else
        {
            await db.Database.EnsureCreatedAsync();
        }

        await DbSeeder.SeedAsync(db,
            services.GetRequiredService<IPasswordHasher<Staff>>(),
            services.GetRequiredService<IOptions<HotelSettings>>().Value,
            config.GetValue("Database:SeedSampleData", true),
            logger);
    }
    catch (Exception ex)
    {
        logger.LogCritical(ex, "Database initialisation failed. Check the 'DefaultConnection' connection string in appsettings.json.");
        throw;
    }
}
