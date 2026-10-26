# Safya Clinic: update the existing MonsterASP site

Target: https://safyaclinic.runasp.net

The files have been prepared locally. Nothing has been published, and the hosted database has not been contacted or modified.

## What was verified

- Both existing Web Deploy profiles use Release, SkipExtraFilesOnServer=true, and EnableMsDeployAppOffline=true.
- Program.cs already calls db.Database.Migrate(), followed by DbSeeder.SeedAsync(db), before serving requests.
- There are 13 compiled migrations; the latest is 20260919210058_AddPatientFollowUpTracking.
- The current EF model matches the migration snapshot: no additional migration is required for the current code.
- Release publishing to a local preview folder succeeded. No appsettings*.json files or local patient uploads are included.
- Web Deploy skip patterns were checked against Windows paths. An actual remote Web Deploy preview has not been run.

## 1. Back up and confirm the destination

1. Rotate the database password and JWT key shared in chat. Update the existing server configuration privately. Do not put production secrets in the repository.
2. In the hosting control panel, take a database backup and download a copy. Also preserve a copy of the existing site files, appsettings.json, any appsettings.Production.json, and wwwroot/uploads.
3. Ensure you can restore that backup. Rolling back application DLLs alone does not undo database schema changes.
4. In Visual Studio, right-click SafyaClinic.Web > Publish. Choose the existing Web Deploy profile for this site. Two similar profiles exist: site83728-WebDeploy and site83728-WebDeploy1. Verify the actual destination before choosing Publish.
5. Keep Release configuration, the correct server runtime, and "Remove additional files at destination" UNCHECKED. The profiles already have SkipExtraFilesOnServer=true.

## 2. Check the live migration history first

Run this read-only query against the hosted SafyaClinic database using your SQL administration tool:

```sql
SELECT [MigrationId], [ProductVersion]
FROM [dbo].[__EFMigrationsHistory]
ORDER BY [MigrationId];
```

Compare the result with the migrations in SafyaClinic.Infrastructure/Migrations. The expected final ID after deployment is:

```text
20260919210058_AddPatientFollowUpTracking
```

Do not create or edit history rows manually. If the history table is absent but application tables already exist, stop and investigate; do not run the full initial migration over them.

Some older Up migrations remove columns:

- 20260622204648_fixingCollectedByColumnInPaymentEntity
- 20260810120000_MoveTreatmentTypeToReservation
- 20260904150116_FixingPrescription

If any of these are pending on the hosted database, review their data impact before continuing. A successful local build does not prove that old production data is safe to migrate.

## 3. Apply database updates during a maintenance window

Recommended controlled procedure:

1. Test the pending migrations on a restored copy of the hosted database where possible.
2. Review docs/DeploymentMigrations.sql. It is an idempotent script generated from the current compiled migrations; it checks migration history and applies missing migrations. It has NOT been executed. Existing schema changes made manually may still conflict with it.
3. Stop the website using the hosting controls and keep it stopped while applying schema updates and publishing. Also stop any other instance using this database. EF Core 8 startup migrations do not coordinate concurrent instances.
4. Execute the reviewed script against the backed-up hosted database using a tool that understands SQL Server GO batch separators, such as SSMS. The database user needs the required schema-change permissions. Verify the selected database before executing.
5. Rerun the migration-history query and check that the latest ID is present and that the script reported no errors.
6. Publish the application as described below, then start the site.

Existing automatic alternative: if you do not apply the SQL script separately, the application will attempt pending migrations at its next startup. It reads the connection string from the server's effective configuration. This requires schema-change permission and can delay startup or prevent the application from starting if a migration fails. Back up and review pending migrations first, and run only one instance during the update. A Publish Succeeded message alone does not verify that startup migration succeeded.

Do not enable another simultaneous database migration process in the publish wizard. Use one migration procedure. No new migration should be generated just because you are publishing.

## 4. Publish without replacing server settings

Deployment protections were added to SafyaClinic.Web/SafyaClinic.Web.csproj and apply to all existing profiles:

- appsettings*.json: excluded from publish output and protected by a Web Deploy skip rule.
- wwwroot/uploads: local files excluded and the server directory protected by a skip rule.
- Normal application code, compiled Razor views, CSS, scripts, and the new logo are still published.

In Visual Studio:

1. Save all source files and rebuild Release.
2. Open the correct existing publish profile and Preview the changes if available.
3. Confirm the preview contains no add/update/delete operation for appsettings.json, any appsettings environment file, or wwwroot/uploads. If it does, do not continue; check that this project and its updated project file are being used.
4. Publish. Keep the site stopped if you are following the manual database procedure above.
5. Start the site and open it in the browser.

Do not manually upload the local appsettings.json. Do not copy the entire local wwwroot/uploads folder over the server. If you use FTP or a folder-copy tool instead of Web Deploy, its own deletion/sync settings are not protected by Web Deploy skip rules; exclude these paths explicitly.

Preserving appsettings.json does not guarantee it is the effective source of every setting: existing appsettings.Production.json and environment variables can override it. Confirm the site runs in Production and no override points at the wrong database. The application currently requires no new server JSON keys for these updates.

For a brand-new site, configuration must be provisioned separately because the project now intentionally excludes appsettings files from publishing. Local development builds still use the local files.

## 5. Verify the deployed result

1. Confirm the migration-history query includes the latest migration.
2. Verify server settings and uploaded files still match the preserved copies (apart from your intentional secret rotation).
3. Sign out and back in so authentication reflects current role assignments.
4. Test patient search, reservation creation, check-in, consultation, completion, payment collection, and access to an existing uploaded document.
5. Test payment access using Admin, Reception, ReceptionAdmin, and Doctor accounts. ReceptionAdmin must exist as that exact role name and be assigned to the intended user through System Lookups > Roles/Users. Custom local roles and assignments are not copied by publishing. The existing seeder does not add new roles to an already populated Roles table.
6. Check the logo and static assets; refresh the browser cache if necessary.
7. Review startup logs if the site returns a startup error. Migrations run before normal request error handling. Do not repeatedly publish or manually alter migration-history rows to hide errors.

Publishing updates code and migration-backed schema/data only. It does not copy your local patients, payments, roles, or other database content. Payment maintenance/backfill controller actions are not automatically invoked by startup migrations; run only a separately reviewed action if actually needed for hosted data.

## Files changed or added for deployment preparation

- Edited: SafyaClinic.Web/SafyaClinic.Web.csproj
- Added: tests/DeploymentChecks/DeploymentChecks.csproj
- Added: tests/DeploymentChecks/Program.cs
- Added: docs/DeploymentMigrations.sql
- Added: docs/MonsterASP-Deployment-Guide.md

Program.cs and existing publish profile credentials were left unchanged. No production credentials were copied into these files.

## References

- Microsoft publish exclusions and Web Deploy skip rules: https://learn.microsoft.com/en-us/aspnet/core/host-and-deploy/visual-studio-publish-profiles?view=aspnetcore-8.0#exclude-files
- Microsoft migration deployment guidance: https://learn.microsoft.com/en-us/ef/core/managing-schemas/migrations/applying
