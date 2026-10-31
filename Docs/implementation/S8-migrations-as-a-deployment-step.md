# Slice S8 — Migrations as a deployment step

Roadmap item **S8** (Group 1, last slice). Fixes **F19**.

| | |
|---|---|
| Branch | `chore/s8-migrations` (from an up-to-date `main`, after PR #10 is merged) |
| Behaviour change | 1. **Outside Development the app no longer changes the database schema when it starts.** If the database is missing migrations, it refuses to start, with a message naming them. 2. In Development nothing changes for you: `appsettings.Development.json` turns automatic migration on. 3. **CI checks every PR**: the build fails if the model changed without a migration, and CI produces the idempotent SQL script as a downloadable artifact. 4. Deploying a version with new migrations has one extra step: **run the reviewed script, then publish** (`Docs/deployment/database-migrations.md`). 5. The stale committed `SafyaClinic/docs/DeploymentMigrations.sql` is removed (CI produces a fresh one) |
| Database | No schema change. ⚠ Optional, with your approval: delete the orphan history row `20260725134425_PatientSourceAndPaymentOptions` from dev (Step 7) |
| Size | S |

## Why

`Program.cs` runs `Database.Migrate()` on every start in every environment. A bad migration would be applied the moment a hosted site restarts, without review, by whichever instance starts first. The history table on dev also has a row for a migration that exists nowhere in code or git.

## Design

| Decision | Choice |
|---|---|
| Who applies migrations | Development: the app, when `Database:MigrateOnStartup` is `true` (set in `appsettings.Development.json`). Everywhere else: a person, by running the reviewed idempotent script before publishing |
| Safety net | Outside that case the app checks for pending migrations at start-up and **stops with a clear error** rather than running on an out-of-date schema |
| Seeder | Still runs at start-up in every environment (it only adds missing reference data and is idempotent) — after the migration check |
| Script source | The existing `tests/DeploymentChecks` tool (offline: no database connection). CI runs it on every PR and uploads `migrations-idempotent.sql` with the test results |
| Orphan history row | Schema already matches the code (verified in Phase 1), so the row is only bookkeeping. Removing it makes history = compiled migrations. Dev now; test/demo during their next deployment, only if present |

## Affected files

| Action | Path |
|---|---|
| Add | `SafyaClinic/SafyaClinic.Web/Startup/DatabaseStartup.cs` |
| Edit | `SafyaClinic/SafyaClinic.Web/Program.cs` |
| Edit | `SafyaClinic/SafyaClinic.Web/appsettings.Development.json` |
| Edit | `.github/workflows/ci.yml` |
| Delete | `SafyaClinic/docs/DeploymentMigrations.sql` |
| Edit | `SafyaClinic/docs/MonsterASP-Deployment-Guide.md` (points to the new procedure) |
| Add | `Docs/deployment/database-migrations.md` |
| Add | `SafyaClinic/tests/SafyaClinic.Tests/Web/DatabaseStartupTests.cs` |
| Add | `Docs/implementation/S8-migrations-as-a-deployment-step.md` |

## Risks

| Risk | Mitigation |
|---|---|
| Someone publishes without running the script | The site refuses to start and names the missing migrations — no half-applied state; run the script, restart |
| Running locally as Production (as in S4's manual test) needs an up-to-date dev DB | It is up to date because Development keeps auto-migrating |
| The hosted SQL tool does not understand `GO` | The procedure says to use SSMS / `sqlcmd`; check with a no-op run first |

## Rollback

Revert the PR's merge commit (start-up migration returns). The orphan-row delete is not undone by a revert; if ever needed, the row can be re-inserted from the backup taken in Step 7.

---

## Steps

PowerShell, from `D:\MyDevWork\clinic-MS`.

### Step 0 — Branch

```powershell
git switch main
git pull
git switch -c chore/s8-migrations
```

### Step 1 — Start-up logic

Create `SafyaClinic/SafyaClinic.Web/Startup/DatabaseStartup.cs`:

```csharp
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
```

### Step 2 — Use it (`Program.cs`)

**2a.** Add with the `using` lines:

```csharp
using SafyaClinic.Web.Startup;
```

**2b.** Replace the start-up block:

```csharp
// Run pending EF Core migrations on startup
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<SafyaDbContext>();
    db.Database.Migrate();
    await DbSeeder.SeedAsync(db, app.Configuration);


}
```

with:

```csharp
// Database: migrate only in Development (opt-in); elsewhere require the reviewed script (S8).
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<SafyaDbContext>();
    var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("DatabaseStartup");
    await DatabaseStartup.PrepareAsync(db, app.Environment, app.Configuration, logger);
}
```

(If your comment line above the block differs slightly, replace from `using (var scope` to the closing `}` of that block.)

### Step 3 — Keep auto-migration for you (Development)

Replace the whole `SafyaClinic/SafyaClinic.Web/appsettings.Development.json` with:

```json
{
  "EnableSensitiveDataLogging": true,
  "Database": {
    "MigrateOnStartup": true
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.EntityFrameworkCore.Database.Command": "Information"
    }
  }
}
```

### Step 4 — CI checks migrations and produces the script

In `.github/workflows/ci.yml`, directly **before** the step `- name: Vulnerable packages`, add:

```yaml
      - name: Migrations match the model (and produce the review script)
        run: |
          mkdir -p TestResults
          dotnet run --project SafyaClinic/tests/DeploymentChecks --configuration Release -- TestResults/migrations-idempotent.sql
```

The existing *Upload test results* step already uploads `TestResults/`, so the script appears in the run's **test-results** artifact. The step fails the build if the model changed without a migration.

### Step 5 — Deployment procedure and clean-up

**5a.** Remove the stale script:

```powershell
git rm SafyaClinic/docs/DeploymentMigrations.sql
```

**5b.** Create `Docs/deployment/database-migrations.md` with this content:

````markdown
# Database migrations — deployment procedure

Since slice S8 the application does **not** change the database schema when it starts (except in Development, where `Database:MigrateOnStartup` is true). Outside Development it refuses to start if migrations are missing and names them in the error.

## When you need this

Only when the version you are about to publish contains new files in `SafyaClinic/SafyaClinic.Infrastructure/Migrations/`. Otherwise publish as usual.

## 1. Get the script

Either download it from the PR's CI run (**Actions → the run → Artifacts → test-results → `migrations-idempotent.sql`**), or generate it locally (no database connection is made):

```powershell
dotnet run --project SafyaClinic\tests\DeploymentChecks -- migrations-idempotent.sql
```

The script is **idempotent**: each migration runs only if its ID is not yet in `__EFMigrationsHistory`.

## 2. Check the target database (read-only)

```sql
SELECT [MigrationId], [ProductVersion] FROM [dbo].[__EFMigrationsHistory] ORDER BY [MigrationId];
```

Compare with the migration files in the code. If the table is missing but application tables exist — stop and ask.

## 3. Back up and stop

1. Back up the database from the MonsterASP control panel.
2. Stop the website (hosting controls) so nothing uses the database during the change.

## 4. Run the script

Use a tool that understands `GO` batch separators: **SSMS** or `sqlcmd` (`sqlcmd -S <server> -d <database> -U <user> -i migrations-idempotent.sql -b`). Check the selected database first. The script must finish without errors; rerun the query from step 2 — the newest migration ID must be present.

## 5. Publish and start

Publish as usual, start the site, sign in, open a few pages. If the site does not start and the error lists missing migrations, step 4 did not complete — fix that, do not edit history rows.

## Orphan history row (one-time)

If step 2 shows `20260725134425_PatientSourceAndPaymentOptions`, it belongs to a migration that no longer exists in the code (its changes are already in the schema). After the backup:

```sql
DELETE FROM [dbo].[__EFMigrationsHistory] WHERE [MigrationId] = N'20260725134425_PatientSourceAndPaymentOptions';
```
````

**5c.** `SafyaClinic/docs/MonsterASP-Deployment-Guide.md` — add this paragraph directly under its first heading line (`# Safya Clinic: update the existing MonsterASP site`):

```markdown
> **Since slice S8 (2026-10-30):** the application no longer applies migrations at start-up outside Development, and `docs/DeploymentMigrations.sql` has been removed. Follow `Docs/deployment/database-migrations.md` for any release that contains new migrations. Statements below about automatic start-up migration are out of date.
```

### Step 6 — Tests

Create `SafyaClinic/tests/SafyaClinic.Tests/Web/DatabaseStartupTests.cs`:

```csharp
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using SafyaClinic.Web.Startup;
using Xunit;

namespace SafyaClinic.Tests.Web;

/// <summary>S8 / F19: only Development, and only when enabled, migrates at start-up.</summary>
public class DatabaseStartupTests
{
    private sealed class Env : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Production;
        public string ApplicationName { get; set; } = "SafyaClinic.Web";
        public string ContentRootPath { get; set; } = "";
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }

    private static IConfiguration Config(string? migrateOnStartup) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { [DatabaseStartup.MigrateOnStartupKey] = migrateOnStartup })
            .Build();

    [Fact]
    public void Development_with_the_setting_migrates()
    {
        Assert.True(DatabaseStartup.ShouldMigrate(new Env { EnvironmentName = Environments.Development }, Config("true")));
    }

    [Fact]
    public void Development_without_the_setting_does_not_migrate()
    {
        Assert.False(DatabaseStartup.ShouldMigrate(new Env { EnvironmentName = Environments.Development }, Config(null)));
    }

    [Theory]
    [InlineData("Production")]
    [InlineData("Staging")]
    public void Other_environments_never_migrate_even_with_the_setting(string environment)
    {
        Assert.False(DatabaseStartup.ShouldMigrate(new Env { EnvironmentName = environment }, Config("true")));
    }
}
```

**Check:**

```powershell
dotnet build SafyaClinic\SafyaClinic.sln
dotnet test SafyaClinic\SafyaClinic.sln
dotnet run --project SafyaClinic\tests\DeploymentChecks -- $env:TEMP\migrations-idempotent.sql
```

Expected: **Passed: 98** (94 + 4); the last command prints `Compiled migrations: 15`, `Latest migration: 20261030120510_AddSecurityStamp`, `Model changes without a migration: False` and writes the script (no database connection).

### Step 7 — Orphan history row on dev (needs your approval)

**7a.** Read-only check — compare history with the code:

```powershell
$cfg = Get-Content SafyaClinic\SafyaClinic.Web\appsettings.json -Raw | ConvertFrom-Json
$conn = New-Object System.Data.SqlClient.SqlConnection $cfg.ConnectionStrings.SafyaClinicDb
$conn.Open(); $cmd = $conn.CreateCommand()
$cmd.CommandText = "SELECT MigrationId, ProductVersion FROM __EFMigrationsHistory ORDER BY MigrationId"
$r = $cmd.ExecuteReader(); $t = New-Object System.Data.DataTable; $t.Load($r); $t | Format-Table -AutoSize; $conn.Close()
```

Expected: the 15 compiled migrations **plus** `20260725134425_PatientSourceAndPaymentOptions` (16 rows).

**7b.** If that is what you see: back up, then delete only that row (it runs inside a transaction and rolls back unless exactly one row is affected):

```powershell
powershell -ExecutionPolicy Bypass -File .assessment\backup-db.ps1
$conn = New-Object System.Data.SqlClient.SqlConnection $cfg.ConnectionStrings.SafyaClinicDb
$conn.Open(); $tx = $conn.BeginTransaction(); $cmd = $conn.CreateCommand(); $cmd.Transaction = $tx
$cmd.CommandText = "DELETE FROM __EFMigrationsHistory WHERE MigrationId = N'20260725134425_PatientSourceAndPaymentOptions'"
$n = $cmd.ExecuteNonQuery()
if ($n -eq 1) { $tx.Commit(); "Deleted 1 row." } else { $tx.Rollback(); "Expected 1 row, got $n - rolled back." }
$conn.Close()
```

Running 7b is your explicit approval for this one-row delete on the **dev** database. Rerun 7a: 15 rows, matching the code. (Skip 7b if the row is not there.)

### Step 8 — Manual check

1. **Development unchanged:** `dotnet run --project SafyaClinic\SafyaClinic.Web --launch-profile http` → starts as usual; the log shows *"Development: applying pending migrations…"*.
2. **Production does not migrate:** run as Production (as in S4):
   ```powershell
   $env:ASPNETCORE_ENVIRONMENT = "Production"
   dotnet run --project SafyaClinic\SafyaClinic.Web --no-launch-profile --urls http://localhost:5070
   ```
   It starts (your dev database is up to date) and the log does **not** say it applied migrations. Stop it with Ctrl+C, then `Remove-Item Env:ASPNETCORE_ENVIRONMENT`. (The refusal path for a missing migration is covered by the next deployment; we do not break your dev database to demonstrate it.)
3. After pushing, open the PR's CI run → Artifacts → **test-results** → `migrations-idempotent.sql` is there.

### Step 9 — Commit, push, PR

```powershell
git add .github/workflows/ci.yml SafyaClinic/SafyaClinic.Web SafyaClinic/docs SafyaClinic/tests/SafyaClinic.Tests Docs
git reset SafyaClinic/SafyaClinic.Web/SafyaClinic.Web.csproj.user
git status
git commit -m "S8: migrations are a reviewed deployment step; Development-only auto-migrate; CI checks model and publishes idempotent script"
git push -u origin chore/s8-migrations
```

Open the PR and send me the number and CI result. Do not merge before review.

---

## Hosted servers (next deployment — S3 to S8 together)

The test and demo sites have not received S5/S6 yet, so the next deployment needs their migrations. Follow `Docs/deployment/database-migrations.md` on the **test site first**:

1. Script from CI (contains `AddPasswordLockout` and `AddSecurityStamp`; idempotent).
2. Read-only history check; delete the orphan row if present.
3. Backup, stop, run the script, publish, start.
4. Then the S3 file move (`App_Data/uploads`) and the checks from S3–S7.

Repeat on the demo site only after the test site works.

## Acceptance criteria

- [ ] Outside Development the app does not apply migrations and refuses to start when any are missing.
- [ ] Development still migrates automatically (setting in `appsettings.Development.json`).
- [ ] CI fails on model changes without a migration and publishes `migrations-idempotent.sql`.
- [ ] `Docs/deployment/database-migrations.md` describes the deploy step; the stale SQL file is gone.
- [ ] Dev history matches the compiled migrations (orphan row removed with approval).
- [ ] 98 tests pass locally and in CI.
