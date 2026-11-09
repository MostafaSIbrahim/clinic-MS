# D2b: rebuild the live site (safyaclinic.runasp.net)

Stage 3 of the approved deployment plan (`D2-fresh-deployment-plan.md`). It rebuilds the test site (site83728) from scratch: a new empty database, the full migration script, the server settings, a publish, and the checks. **This site becomes the clinic's live site. Don't enter real patients until the go-live gate in the plan has passed** (hosting answers, restore test, legal check).

| | |
|---|---|
| Before you start | **D2a is merged** and CI is green on `main` |
| You need | The MonsterASP control panel; **SSMS**; Visual Studio with the `site83728-WebDeploy` profile; a strong password for the first admin (at least 10 characters), chosen by you |
| Time | About 45 minutes |
| Data | The old test database is **deleted** (your choice on 9 Nov; it held no real data) |

Keep every password and connection string to yourself. Never send them to me, put them in git, or paste them into a chat.

---

## Step 1: the migration script (your PC)

```powershell
cd D:\MyDevWork\clinic-MS
git switch main
git pull
dotnet run --project SafyaClinic\tests\DeploymentChecks -- D:\MyDevWork\migrations-idempotent.sql
```

It must print `Model changes without a migration: False`. The file `D:\MyDevWork\migrations-idempotent.sql` builds the whole database from empty.

## Step 2: stop the site

Control panel → **site83728** → **Stop**.

## Step 3: replace the database

1. Control panel → **Databases** → the test site's database → **Delete**. Confirm it's the **test site's** database (site83728), not the demo's.
2. **Create** a new MS SQL database. Note its server, database name, user and password.

## Step 4: build the tables (SSMS)

1. SSMS → **Connect** with the new database's server, user and password.
2. Check the database name in the toolbar's drop-down is the **new** one.
3. Open `D:\MyDevWork\migrations-idempotent.sql` → **Execute**. It must finish without errors.
4. Check:

```sql
SELECT TOP 1 MigrationId FROM __EFMigrationsHistory ORDER BY MigrationId DESC;   -- ends with _WriteOffReasons
SELECT COUNT(*) AS Tables FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_TYPE = 'BASE TABLE';   -- about 40
```

## Step 5: clear old files (File Manager)

In site83728's root, delete:
- `wwwroot/uploads`, if it's still there;
- everything inside `App_Data` (old logs, uploads, keys). Keep the empty `App_Data` folder.

## Step 6: server settings (File Manager → `appsettings.json` in the site root → Edit)

Replace the whole file with this, filling in the `<…>` parts yourself:

```json
{
  "ConnectionStrings": {
    "SafyaClinicDb": "Server=<server>;Database=<database>;User Id=<user>;Password=<password>;TrustServerCertificate=True;MultipleActiveResultSets=False;"
  },
  "Seed": {
    "AdminPassword": "<the first admin's password: 10+ characters>"
  },
  "ClinicInfo": {
    "Name": "<clinic name>",
    "Tagline": "<one line, e.g. Internal Medicine & Nutrition Care>",
    "Address": "<street, area, city>",
    "Phone": "<phone as printed>",
    "Email": "<clinic email>"
  },
  "FileStorage": { "BasePath": "App_Data/uploads", "MaxFileSizeBytes": 10485760 },
  "Security": { "Lockout": { "MaxFailedAttempts": 5, "LockoutMinutes": 15 } },
  "Clinic": { "TimeZone": "Africa/Cairo" },
  "Logging": { "LogLevel": { "Default": "Warning", "Microsoft.AspNetCore": "Warning" } },
  "AllowedHosts": "*"
}
```

`ClinicInfo` prints on prescriptions and lab slips. Use the clinic's real details. If MonsterASP shows the connection string in a different form, use theirs and keep `TrustServerCertificate=True`.

## Step 7: publish (Visual Studio)

Right-click **SafyaClinic.Web** → **Publish** → profile **site83728-WebDeploy** → **Publish**. The server's `appsettings.json` isn't overwritten (publish rule), and `App_Data` is kept.

## Step 8: start the site

Control panel → site83728 → **Start**. The first request takes a few seconds: the app checks the database and seeds the administrator, permission groups, visit types and a clinic.

## Step 9: checks

| # | Check | Expected |
|---|---|---|
| 1 | `https://safyaclinic.runasp.net/health` | `Healthy` |
| 2 | Sign in with phone **0100** and your Step 6 password | The dashboard opens |
| 3 | `https://safyaclinic.runasp.net/System/Readiness` | **Problem** count 0. **Review** only for *First admin password setting* (removed in Step 10) |
| 4 | The address bar shows the padlock (HTTPS) | Yes. If not, stop and tell me (hosting question H4) |
| 5 | Settings → visit types | General Consultation 800, Follow-up Visit 200, … and nutrition Follow-up 0 |
| 6 | Language switch → العربية, then back | The layout flips to right-to-left and back |
| 7 | Register a test patient (name + phone), book them, check in, open My Queue as admin | Works. You'll delete this test patient before go-live |
| 8 | Upload a small PNG to the test patient's prescription or lab request, then delete it | Works: the site can write `App_Data` |
| 9 | File Manager → `App_Data` | `keys` (one `key-….xml`), `logs` (today's file), `uploads` |
| 10 | Control panel → **Restart** the site, then reload a page | You're **still signed in** (D2a) |

If check 8 or 9 fails (no write access to `App_Data`): control panel → site → **Permissions** (or File Manager → `App_Data` → permissions), and give the application write access. Otherwise ask MonsterASP support. Tell me if that isn't possible.

## Step 10: remove the seed password

1. **Users** → edit the admin account: your name and, if you want to stop using `0100`, your own phone number.
2. File Manager → `appsettings.json` → delete the whole `"Seed": { … },` block → Save.
3. Control panel → **Restart**. `/System/Readiness` now shows no Review for the seed password.

## Step 11: the hosting review (stage 2 of the plan)

From the control panel, MonsterASP's plan page, or their support, answer:

| # | Question | Your answer |
|---|---|---|
| H1 | Plan (free or paid), and its limits | |
| H2 | Automatic database backups: are there any, how often, kept how long, can you restore yourself? | |
| H3 | Can you download a backup file (.bak)? | |
| H4 | Valid HTTPS on `safyaclinic.runasp.net`? Own domain possible? | |
| H5 | Country of the servers and database | |
| H6 | Who can sign in to the panel; is two-factor sign-in available, and is it on? | |
| H7 | `App_Data` writable and kept (checks 8–10) | |

**Send me:** the Step 9 results (✓ or what you saw), a screenshot of the readiness page (it holds no secrets), and the H1–H7 answers. I'll then send the stage 4 set-up checklist, the restore test (stage 5) and the go-live decision.

## If something goes wrong

- **The script fails in Step 4:** don't continue. Send me the error message (not the connection details).
- **The site shows an error after Step 8:** File Manager → `App_Data/logs` → download today's `safya-….json` and send me the last few lines. Or tell me what `/health` says.
- **The site says migrations are missing:** Step 4 ran against a different database than the one in `appsettings.json`. Check the database name in both places.
