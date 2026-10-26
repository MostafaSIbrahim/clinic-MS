# First publish: Safya Clinic demo on MonsterASP

Demo URL: https://safyaclinic-demo.runasp.net
Production URL (leave untouched): https://safyaclinic.runasp.net

This guide uses your existing application and the newly downloaded DEMO publish profile. No application code, production profile, server, or database was changed while preparing it. The downloaded profile and the hosting account have not been inspected; verify their actual destination below.

## 1. Create an isolated demo database

1. Sign in to MonsterASP. Open Databases > Add database and create a NEW MSSQL database. A new website does not automatically prove that a separate database exists. [Host instructions](https://help.monsterasp.net/books/databases/page/create-database)
2. Record its database name, SQL server, database user, and password privately. Use a dedicated demo database user with no permissions to the production database.
3. Copy the connection string provided for this new database. Preserve the host's recommended encryption and connection options; do not build it by modifying the production password alone.
4. Compare database names: the demo database must NOT be the existing production database, db63156. The physical SQL server may be shared by the host; the database and its credentials must be separate.
5. Keep the new database empty. Do not restore production patient or payment records into it. Create fictional demonstration records later.

Check before proceeding: the demo database exists, is empty, and uses dedicated credentials. If your hosting plan cannot provide a separate database, resolve that first; do not reuse production.

## 2. Confirm the demo website and runtime

1. Open the website entry for safyaclinic-demo.runasp.net in the hosting control panel.
2. Confirm it is a separate website/site ID and physical directory from production.
3. Verify that its runtime supports this application's .NET 8 target and enable HTTPS using the hosting controls.
4. Activate WebDeploy for this DEMO site if necessary. You have already downloaded its profile; verify it came from this exact website entry.
5. Keep the demo website stopped while preparing its files if the host permits deploying to a stopped site. Otherwise finish its correct configuration before the first publish. Do not stop or restart production.

## 3. Prepare the demo's own appsettings.json

The project intentionally excludes appsettings*.json from publishing and protects server copies. Therefore the first deployment needs a MANUAL configuration upload. Do not remove these protections or change the project's local appsettings.json to production/demo credentials.

1. Copy docs/demo-appsettings.template.json to a private directory outside the repository.
2. Rename the private copy to appsettings.json.
3. Replace ConnectionStrings:SafyaClinicDb with the connection string from step 1. It must name the DEMO database and demo user.
4. Replace Jwt:Key with a NEW demo-only random secret. To generate one locally in PowerShell:

```powershell
$demoKeyBytes = New-Object byte[] 48
$demoKeyGenerator = [System.Security.Cryptography.RandomNumberGenerator]::Create()
$demoKeyGenerator.GetBytes($demoKeyBytes)
[Convert]::ToBase64String($demoKeyBytes)
$demoKeyGenerator.Dispose()
```

Copy the generated value privately into the demo JSON. Do not reuse the production key or publish the filled JSON in source control.

5. Keep Jwt:Issuer and Jwt:Audience as SafyaClinicDemo, and AllowedHosts as safyaclinic-demo.runasp.net.
6. Keep ClinicInfo:Name as Safya Clinic - DEMO so views that use this setting identify the environment. The shared logo will still say Safya Clinic; this setting does not add a global demo banner.
7. Keep FileStorage:BasePath as wwwroot/uploads. This is separate from production because the demo uses its own physical website directory.
8. Keep the JSON valid: no comments or trailing commas. If manually entering a password containing a backslash or quotation mark, JSON-escape it correctly. The template contains placeholders and cannot connect until filled in.
9. Using the DEMO site's file manager or its FTP/SFTP connection, upload appsettings.json into the application's root directory, alongside the location where SafyaClinic.Web.dll and web.config will be published. Do NOT put configuration under wwwroot.
10. Create wwwroot/uploads within the demo directory if absent, and ensure the application can write to it through the host's supported folder permissions.
11. Check for environment variables such as ConnectionStrings__SafyaClinicDb and any existing appsettings.Production.json. They must not override your demo connection with production settings. A fresh site normally has no application-specific overrides, but verify.

Use Production as the ASP.NET Core runtime environment even for this public demo. "Demo website" does not mean Development mode. This avoids displaying developer error details publicly.

## 4. Import a NEW Visual Studio publish profile

1. Open the solution in Visual Studio.
2. Right-click SafyaClinic.Web > Publish.
3. Choose New profile / Import profile and select the DEMO .publishSettings file downloaded from MonsterASP. Do not edit the production profile to point at the demo. [Host import instructions](https://help.monsterasp.net/books/deploy/page/how-to-deploy-net-core-web-application-using-visual-studio)
4. Give the imported profile a clearly distinct name, such as SafyaClinic-DEMO. Leave both existing production profiles unchanged.
5. Open the imported profile's settings and verify the deployment site/application name against the DEMO site's ID in MonsterASP. The WebDeploy server hostname may be shared, so it is not sufficient by itself to identify the website.
6. Confirm the destination/launch URL is https://safyaclinic-demo.runasp.net. Changing only this launch URL does NOT change where files deploy: the deployment site/application path must also be the demo's.
7. Use Release configuration. Match the hosting runtime/architecture; use framework-dependent deployment when the host provides the matching .NET 8 runtime.
8. Keep "Remove additional files at destination" unchecked. Enable taking the application offline during deployment if the profile provides this option.
9. If needed, edit ONLY the new .pubxml file under SafyaClinic.Web/Properties/PublishProfiles. Within its existing PropertyGroup, ensure these entries exist (update existing entries rather than duplicating them):

```xml
<SkipExtraFilesOnServer>true</SkipExtraFilesOnServer>
<EnableMsDeployAppOffline>true</EnableMsDeployAppOffline>
<EnvironmentName>Production</EnvironmentName>
```

Do not replace the imported server address, site ID, username, or other host-provided connection fields with guessed values.

10. Do not select a production connection under any database/dependency publishing options. This guide uses the application's existing startup migrations; no separate publish-time SQL execution is needed.
11. Treat the downloaded profile and any saved publishing credentials as private. Do not share them or commit passwords.

## 5. Final check before clicking Publish

Verify all four items together:

| Item | Required demo value |
| --- | --- |
| Selected Visual Studio profile | SafyaClinic-DEMO (new imported demo profile) |
| Deployment site/application ID | The ID shown for the DEMO website in MonsterASP |
| Server configuration database | The new empty DEMO database; never db63156 |
| Server configuration location | The DEMO website's physical root |

The project-level rules also apply to this new profile: settings and uploaded patient files are excluded/protected, while DLLs, compiled views, CSS, scripts, and the logo are published. If Visual Studio offers Preview, verify no operation overwrites or deletes appsettings*.json or wwwroot/uploads. Configuration must already have been manually installed for the first deployment.

Do not continue if any destination or database identity is unclear.

## 6. Publish and initialize the demo

1. Build the solution in Release.
2. Confirm SafyaClinic-DEMO is still selected, then click Publish.
3. Wait for a successful result. If you previously stopped the demo website, start only that website.
4. Open https://safyaclinic-demo.runasp.net. On startup Program.cs calls db.Database.Migrate(), then DbSeeder.SeedAsync(db).
5. For an empty hosted database, startup should create the application tables through all pending migrations, then create the seed administrator and lookup data. The host supplies the SQL database itself; the application is responsible for its tables.
6. Initialization may delay the first request. Only run one app instance while it is applying migrations. The database user needs the permissions required for schema changes. Publishing success alone does not prove database initialization succeeded.
7. If startup fails, inspect the DEMO logs and connection settings. Do not switch to the production connection to make it work. Do not run EnsureCreated or manually insert migration-history rows.

No separate Update-Database command is necessary with this startup flow. In particular, do not run one from Visual Studio against an unverified local/default connection. You also do not need to execute DeploymentMigrations.sql in addition to startup migrations for this first-install procedure.

## 7. Verify the database and finish demo setup

Connect your SQL tool specifically to the new demo database and run:

```sql
SELECT DB_NAME() AS CurrentDatabase;
SELECT [MigrationId], [ProductVersion]
FROM [dbo].[__EFMigrationsHistory]
ORDER BY [MigrationId];
```

For the code checked when this guide was created, expect 13 migration records, ending with 20260919210058_AddPatientFollowUpTracking. If new migrations are added later, the expected latest ID changes.

On a fresh empty database, the current DbSeeder creates this initial account:

- Email: admin@safya.com
- Initial password: Admin@123

Sign in immediately, then change the password at https://safyaclinic-demo.runasp.net/Auth/ChangePassword before sharing the demo URL. This is a bootstrap account, not a password to keep for a public demo. If the host offers access restrictions, keep them enabled during initialization until the password is changed. If Users already contains records, the seeder does not recreate this account.

First-install lookup check:

The existing SeedNutritionFollowUpTreatmentType migration inserts a nutrition Follow-up row before DbSeeder runs. SeedTreatmentTypesAsync currently returns when ANY treatment exists. Consequently a fresh database can have only that treatment, with the usual defaults missing. In the DEMO admin's System Lookups > Treatment Types, inspect the rows and add any missing types before testing reservations. Do not add duplicates:

| Category | Treatment name | Default fee in current seeder | Minutes |
| --- | --- | ---: | ---: |
| InternalMedicine | General Consultation | 800 | 30 |
| InternalMedicine | Follow-up Visit | 200 | 15 |
| InternalMedicine | Emergency Treatment | 1200 | 60 |
| Nutritional | Nutrition Consultation | 600 | 45 |
| Nutritional | Diet Plan Review | 150 | 20 |
| Nutritional | Body Composition Analysis | 250 | 15 |
| Nutritional | Follow-up | 0 | 15 |

These are the application's existing example defaults; use suitable demo values. This guide does not change the shared seeder or production data.

Next, through the demo application's lookups and user administration:

1. Create/configure demo clinics and doctors and their clinic assignments.
2. Add the exact ReceptionAdmin role if you need to demonstrate it; the current default role seeder does not create it.
3. Create separate demo Doctor, Reception, and ReceptionAdmin accounts and assign roles.
4. Create fictional patients and reservations. Do not import real patient documents or production credentials.

## 8. Prove the demo is isolated

1. Create a clearly named fictional patient, such as DEMO-ONLY-TEST, on the demo.
2. Search production for that exact name and confirm it does not exist there. Do not create or edit anything in production for this check.
3. Test check-in, consultation, completion, and a small fictional payment in the demo.
4. Test the payment role rules: Admin sees the full dashboard; reception sees unpaid sections and today's report; doctors see their own transactions for selected dates.
5. Upload a harmless sample file and verify it is stored under the demo's uploads directory.
6. Confirm the production site still works and its files/settings were not deployed to or changed. If the demo ever shows real production patients, stop the DEMO site immediately and inspect its effective database connection before doing anything else.

## 9. Subsequent demo updates

Select SafyaClinic-DEMO each time, review the target, and publish. Its manually installed settings and uploads will remain protected. Startup applies only migrations missing from the demo database. Future shared code changes reach production only if you separately publish them with a production profile.

Keep a backup of the demo database before later schema updates if you want to preserve its demonstration data. Never use a production database reset/delete procedure for the demo.

## Troubleshooting

- Missing connection string: settings are intentionally excluded from publish. Manually upload the filled appsettings.json into the DEMO app root.
- Login failed/database unavailable: check the demo credentials and host connection options; do not substitute production settings.
- HTTP 500.30: inspect demo startup logs; common causes include configuration, database permissions, or a failed migration.
- HTTP 500.31/runtime error: verify .NET 8 runtime and deployment architecture with the host.
- Missing treatment choices: complete the first-install lookup check above.
- Upload failure: verify the demo wwwroot/uploads directory and write permission.
- Wrong destination in preview: cancel and select the correct profile. Renaming a profile or changing its launch URL alone does not change its deployment target.

## Files supplied

- This guide: docs/MonsterASP-Demo-First-Publish.md
- Placeholder-only settings: docs/demo-appsettings.template.json

No production secrets are included. No site was published and no database was connected to or changed while preparing these files.
