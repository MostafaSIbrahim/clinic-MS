using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using SafyaClinic.Infrastructure.Data;

// Deployment check: build migration SQL from metadata only; never connect to a database or start the web app.
using var db = new SafyaDbContext(new DbContextOptionsBuilder<SafyaDbContext>()
    .UseSqlServer("Server=localhost;Database=OfflineMetadataOnly;Integrated Security=True;TrustServerCertificate=True").Options);
var migrations = db.Database.GetMigrations().ToArray();
Console.WriteLine($"Compiled migrations: {migrations.Length}");
Console.WriteLine($"Latest migration: {migrations.Last()}");
var pending = db.Database.HasPendingModelChanges();
Console.WriteLine($"Model changes without a migration: {pending}");
if (pending) Environment.ExitCode = 1;
if (args.Length == 1)
{
    File.WriteAllText(args[0], db.GetService<IMigrator>().GenerateScript(options: MigrationsSqlGenerationOptions.Idempotent));
    Console.WriteLine("Idempotent migration review script generated. No database connection made.");
}
