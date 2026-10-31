using Microsoft.EntityFrameworkCore;
using SafyaClinic.Application.DependencyInjection;
using SafyaClinic.Domain.Interfaces.Repositories;
using SafyaClinic.Infrastructure.Data;
using SafyaClinic.Infrastructure.DependencyInjection;
using SafyaClinic.Infrastructure.Repositories;
using Microsoft.AspNetCore.Authentication.Cookies;
using SafyaClinic.Web.Services.Files;
using SafyaClinic.Application.Security;
using SafyaClinic.Web.Security;
using SafyaClinic.Web.Startup;
using SafyaClinic.Application.Common;


var builder = WebApplication.CreateBuilder(args);

// ── MVC ──────────────────────────────────────────────────────
builder.Services.AddControllersWithViews();

// ── Infrastructure (DbContext + UnitOfWork) ──────────────────
builder.Services.AddInfrastructure(builder.Configuration);

// ── Application services ─────────────────────────────────────
builder.Services.AddApplication(builder.Configuration);

// ── Generic repository (used directly in some places) ────────
builder.Services.AddScoped(typeof(IRepository<>), typeof(GenericRepository<>));

// ── Cookie authentication ─────────────────────────────────────
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath        = "/Auth/Login";
        options.LogoutPath       = "/Auth/Logout";
        options.AccessDeniedPath = "/Auth/AccessDenied";
        options.ExpireTimeSpan   = TimeSpan.FromHours(8);
        options.SlidingExpiration = true;
        options.Cookie.Name      = "SafyaClinic.Auth";
        options.Cookie.HttpOnly  = true;
        options.Cookie.SameSite  = SameSiteMode.Strict;
        options.Events.OnValidatePrincipal = SessionValidator.ValidateAsync;   // S6
    });

builder.Services.AddAuthorizationBuilder()
    // Payment access: financial reporting and collection have separate permissions.
    .AddPolicy("PaymentStaff", p => p.RequireRole("Admin", "Reception", "ReceptionAdmin"))
    .AddPolicy("PaymentReports", p => p.RequireRole("Admin", "Reception", "ReceptionAdmin", "Doctor"))
    .AddPolicy("AdminOnly",        p => p.RequireRole("Admin"))
    .AddPolicy("DoctorOrAdmin",    p => p.RequireRole("Admin", "Doctor"))
    .AddPolicy("ReceptionOrAdmin", p => p.RequireRole("Admin", "Reception"))
    .AddPolicy("NutritionTeam",    p => p.RequireRole("Admin", "Nutritionist"))
    .AddPolicy("ClinicalStaff",    p => p.RequireRole("Admin", "Doctor", "Nutritionist", "Reception"));

// ── Attachment storage (S3): outside wwwroot, never served statically ──
var storageRoot = Path.Combine(
    builder.Environment.ContentRootPath,
    builder.Configuration["FileStorage:BasePath"] ?? "App_Data/uploads");
var maxUploadBytes = builder.Configuration.GetValue<long?>("FileStorage:MaxFileSizeBytes") ?? 10 * 1024 * 1024;
builder.Services.AddSingleton<IFileStorage>(new LocalFileStorage(storageRoot, maxUploadBytes));


// ── Sign-in lockout (S5) ─────────────────────────────────────
builder.Services.AddSingleton(
    builder.Configuration.GetSection(LockoutSettings.SectionName).Get<LockoutSettings>() ?? new LockoutSettings());

// ── Clinic clock (F2): one time zone for "today" and every time shown ──
builder.Services.AddSingleton<IClinicClock>(new ClinicClock(ClinicClock.FindZone(
    builder.Configuration[ClinicClock.TimeZoneSettingKey] ?? ClinicClock.DefaultTimeZoneId)));
// ── Pipeline ─────────────────────────────────────────────────
var app = builder.Build();

// Database: migrate only in Development (opt-in); elsewhere require the reviewed script (S8).
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<SafyaDbContext>();
    var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("DatabaseStartup");
    await DatabaseStartup.PrepareAsync(db, app.Environment, app.Configuration, logger);
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();

// The old upload folder must never be served, even if files are left behind (S3).
app.Use(async (context, next) =>
{
    if (context.Request.Path.StartsWithSegments("/uploads"))
    {
        context.Response.StatusCode = StatusCodes.Status404NotFound;
        return;
    }
    await next();
});

app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

// A user whose password was reset must choose a new one before using the app (S5).
app.Use(async (context, next) =>
{
    if (context.User.HasClaim(AuthClaims.MustChangePassword, "true")
        && !context.Request.Path.StartsWithSegments("/Auth")
        && !context.Request.Path.StartsWithSegments("/Home/Error"))
    {
        context.Response.Redirect("/Auth/ChangePassword");
        return;
    }
    await next();
});
// Default route: Home/Index redirects authenticated → Dashboard, guests → Login
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
