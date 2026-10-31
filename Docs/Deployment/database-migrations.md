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