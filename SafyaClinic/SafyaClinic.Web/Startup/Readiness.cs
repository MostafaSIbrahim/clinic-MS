using Microsoft.EntityFrameworkCore;
using SafyaClinic.Application.Common;
using SafyaClinic.Application.Security;
using SafyaClinic.Infrastructure.Data;

namespace SafyaClinic.Web.Startup;

public enum ReadinessLevel { Ok, Warning, Problem }

public sealed record ReadinessItem(string Name, ReadinessLevel Level, string Detail);

/// <summary>D2a: what the readiness page looks at, gathered from the running site.</summary>
public sealed record ReadinessFacts(
    string Environment, bool IsHttps, string? LastMigration, int PendingMigrations,
    bool KeysFolderWritable, int KeyFiles, bool UploadsWritable, bool TodaysLogExists,
    bool SeedPasswordConfigured, string? ClinicName, string? ClinicAddress, string? ClinicPhone, string? ClinicEmail,
    int PricedVisitTypes, int ActiveAdmins, string TimeZone, DateTime ClinicNow);

/// <summary>
/// D2a: "is this site ready for real patients?", for Admin at /System/Readiness. Pure rules over
/// <see cref="ReadinessFacts"/>, so they can be tested; <see cref="GatherAsync"/> reads the facts.
/// </summary>
public static class Readiness
{
    // The placeholder clinic details shipped in appsettings.json.
    private static readonly string[] ExampleValues = { "123 Corniche St", "+20 2 1234 5678", "safyaclinic.example" };

    public static IReadOnlyList<ReadinessItem> Evaluate(ReadinessFacts f)
    {
        var items = new List<ReadinessItem>
        {
            f.Environment == "Production"
                ? new("Environment", ReadinessLevel.Ok, "Production")
                : new("Environment", ReadinessLevel.Warning, $"{f.Environment}. A hosted site should run as Production (development settings show error details)."),

            f.IsHttps
                ? new("Encrypted connection (HTTPS)", ReadinessLevel.Ok, "This page was served over HTTPS.")
                : new("Encrypted connection (HTTPS)", ReadinessLevel.Problem, "This page was served without HTTPS. Patient data must only travel encrypted."),

            f.PendingMigrations == 0
                ? new("Database", ReadinessLevel.Ok, $"Up to date (last migration {f.LastMigration ?? "none"}).")
                : new("Database", ReadinessLevel.Problem, $"{f.PendingMigrations} migration(s) not applied."),

            f.KeysFolderWritable
                ? new("Sign-in keys", ReadinessLevel.Ok, $"Kept in App_Data/keys ({f.KeyFiles} key file(s)); restarts don't sign people out.")
                : new("Sign-in keys", ReadinessLevel.Problem, "App_Data/keys can't be written. Give the site write access to App_Data."),

            f.UploadsWritable
                ? new("Attachments folder", ReadinessLevel.Ok, "App_Data/uploads can be written.")
                : new("Attachments folder", ReadinessLevel.Problem, "App_Data/uploads can't be written, so lab results and prescription files can't be saved."),

            f.TodaysLogExists
                ? new("Logs", ReadinessLevel.Ok, "Today's log file exists in App_Data/logs.")
                : new("Logs", ReadinessLevel.Warning, "No log file for today yet in App_Data/logs."),

            f.SeedPasswordConfigured
                ? new("First admin password setting", ReadinessLevel.Warning, "Seed:AdminPassword is still in the server settings. Remove it once the first admin has signed in.")
                : new("First admin password setting", ReadinessLevel.Ok, "Not present."),
        };

        var clinic = new[] { f.ClinicName, f.ClinicAddress, f.ClinicPhone, f.ClinicEmail };
        items.Add(clinic.Any(v => string.IsNullOrWhiteSpace(v)) || clinic.Any(v => ExampleValues.Any(e => v!.Contains(e, StringComparison.OrdinalIgnoreCase)))
            ? new("Clinic details", ReadinessLevel.Warning, "ClinicInfo still has missing or example values; they print on prescriptions and lab slips.")
            : new("Clinic details", ReadinessLevel.Ok, $"{f.ClinicName}, {f.ClinicAddress}"));

        items.Add(f.PricedVisitTypes > 0
            ? new("Visit types", ReadinessLevel.Ok, $"{f.PricedVisitTypes} active priced visit type(s). Check the prices under Settings.")
            : new("Visit types", ReadinessLevel.Problem, "No active visit type has a price, so no visit would ever have a balance to collect."));

        items.Add(f.ActiveAdmins switch
        {
            0 => new("Administrators", ReadinessLevel.Problem, "No active administrator."),
            1 => new("Administrators", ReadinessLevel.Ok, "1 active administrator."),
            <= 3 => new("Administrators", ReadinessLevel.Ok, $"{f.ActiveAdmins} active administrators."),
            _ => new("Administrators", ReadinessLevel.Warning, $"{f.ActiveAdmins} active administrators. Keep full access to as few people as possible."),
        });

        items.Add(new("Clinic time", ReadinessLevel.Ok, $"{f.ClinicNow:ddd dd MMM yyyy HH:mm} ({f.TimeZone}). Check it matches the clock on the wall."));
        return items;
    }

    public static async Task<ReadinessFacts> GatherAsync(
        SafyaDbContext db, IWebHostEnvironment env, IConfiguration config, IClinicClock clock, bool isHttps)
    {
        var applied = (await db.Database.GetAppliedMigrationsAsync()).ToList();
        var pending = (await db.Database.GetPendingMigrationsAsync()).Count();
        var appData = Path.Combine(env.ContentRootPath, "App_Data");
        var keys = Path.Combine(appData, "keys");
        var uploads = Path.IsPathRooted(config["FileStorage:BasePath"] ?? "")
            ? config["FileStorage:BasePath"]!
            : Path.Combine(env.ContentRootPath, config["FileStorage:BasePath"] ?? "App_Data/uploads");
        var todaysLog = Path.Combine(appData, "logs", $"safya-{DateTime.Now:yyyyMMdd}.json");

        var priced = await db.TreatmentTypes.CountAsync(t => t.IsActive && t.DefaultCost > 0);
        var admins = await db.UserGroups
            .Where(ug => ug.Group.SystemKey == SystemGroups.AdminRole && ug.User.IsActive)
            .Select(ug => ug.UserId).Distinct().CountAsync();

        return new ReadinessFacts(
            env.EnvironmentName, isHttps, applied.LastOrDefault(), pending,
            CanWrite(keys), Directory.Exists(keys) ? Directory.GetFiles(keys, "*.xml").Length : 0,
            CanWrite(uploads), File.Exists(todaysLog),
            !string.IsNullOrWhiteSpace(config["Seed:AdminPassword"]),
            config["ClinicInfo:Name"], config["ClinicInfo:Address"], config["ClinicInfo:Phone"], config["ClinicInfo:Email"],
            priced, admins, clock.TimeZone.Id, clock.Now);
    }

    /// <summary>Writes and deletes a small probe file.</summary>
    private static bool CanWrite(string folder)
    {
        try
        {
            Directory.CreateDirectory(folder);
            var probe = Path.Combine(folder, $".probe-{Guid.NewGuid():N}");
            File.WriteAllText(probe, "ok");
            File.Delete(probe);
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }
}
