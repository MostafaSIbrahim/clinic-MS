using Microsoft.EntityFrameworkCore;
using SafyaClinic.Infrastructure.Data;

namespace SafyaClinic.Web.Startup;

/// <summary>
/// Schema changes are a reviewed deployment step (S8). Only Development may apply migrations
/// automatically, and only when "Database:MigrateOnStartup" is true. Everywhere else the app
/// refuses to start while migrations are missing, so it never runs against an out-of-date schema.
/// </summary>
public static class DatabaseStartup
{
    public const string MigrateOnStartupKey = "Database:MigrateOnStartup";

    public static bool ShouldMigrate(IHostEnvironment environment, IConfiguration configuration) =>
        environment.IsDevelopment() && configuration.GetValue(MigrateOnStartupKey, false);

    public static async Task PrepareAsync(
        SafyaDbContext db, IHostEnvironment environment, IConfiguration configuration, ILogger logger)
    {
        if (ShouldMigrate(environment, configuration))
        {
            logger.LogInformation("Development: applying pending migrations ({Setting} is true).", MigrateOnStartupKey);
            await db.Database.MigrateAsync();
        }
        else
        {
            var pending = (await db.Database.GetPendingMigrationsAsync()).ToList();
            if (pending.Count > 0)
            {
                throw new InvalidOperationException(
                    $"The database is missing {pending.Count} migration(s): {string.Join(", ", pending)}. " +
                    "Apply the reviewed idempotent script (Docs/deployment/database-migrations.md), then start the application again.");
            }
        }

        await DbSeeder.SeedAsync(db, configuration);
    }
}