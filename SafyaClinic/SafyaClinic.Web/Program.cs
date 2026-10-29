using Microsoft.EntityFrameworkCore;
using SafyaClinic.Application.DependencyInjection;
using SafyaClinic.Domain.Interfaces.Repositories;
using SafyaClinic.Infrastructure.Data;
using SafyaClinic.Infrastructure.DependencyInjection;
using SafyaClinic.Infrastructure.Repositories;
using Microsoft.AspNetCore.Authentication.Cookies;


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


// ── Pipeline ─────────────────────────────────────────────────
var app = builder.Build();

// Run pending EF Core migrations on startup
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<SafyaDbContext>();
    db.Database.Migrate();
    await DbSeeder.SeedAsync(db, app.Configuration);


}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
// Default route: Home/Index redirects authenticated → Dashboard, guests → Login
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
