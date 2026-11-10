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

This step runs the migration script against the **new, empty** database to create all the tables. It takes 1–2 minutes.

### 4a. Find the connection details in the control panel

Control panel → **Databases** → the database you created in Step 3. Note four things:

| What | Looks like | In SSMS it goes in |
|---|---|---|
| Server (host) | e.g. `db12345.public.databaseasp.net`. Some panels add a port, e.g. `,1433` | Server name |
| Database name | e.g. `db12345` | Connect to database |
| User (login) | e.g. `db12345` | Login |
| Password | the one you set or were given | Password |

### 4b. Connect from SSMS

1. Open **SQL Server Management Studio**. The **Connect to Server** window opens. If it doesn't: **File → Connect Object Explorer…**.
2. Fill in:
   - **Server type:** Database Engine
   - **Server name:** the server from 4a, exactly as shown, including any `,port`
   - **Authentication:** **SQL Server Authentication** (not Windows)
   - **Login** and **Password:** from 4a. You can tick *Remember password* only if this is your own PC.
3. Encryption, which depends on your SSMS version:
   - **SSMS 20 or later:** set **Encryption** to *Mandatory* and tick **Trust server certificate**.
   - **Older SSMS:** click **Options >>** → **Connection Properties** → tick **Encrypt connection** and **Trust server certificate**.
4. Still under **Options >> → Connection Properties**, in **Connect to database**, type the **database name** from 4a. On shared hosting you may not be allowed to list or open other databases.
5. Click **Connect**. In **Object Explorer** on the left, you see the server, and under *Databases* your database.

If it fails:
- *Login failed*: re-check the user and password.
- *The certificate chain was issued by an authority that is not trusted*: tick **Trust server certificate**.
- *Cannot connect* or *network-related error*: check the server name and port. Some hosts also require **remote access** to be switched on for the database in the panel; look for "Remote access" or "Allow remote connections".

### 4c. Open the script against the right database

1. In Object Explorer, right-click **your database** → **New Query**. A query window opens, connected to it.
2. Check the **database drop-down** in the toolbar (top left, next to *Execute*). It must show the **new** database's name, not `master`. If it shows `master`, choose your database there.
3. **File → Open → File…** → `D:\MyDevWork\migrations-idempotent.sql`. If SSMS asks which connection to use, choose the same server and database. Check the drop-down again: still the new database.
4. **Ignore red underlines and the Error List at this point.** As soon as the file opens, SSMS's IntelliSense checks it against the database as it is now, which is empty. It underlines many column names and may list about 24 "errors" such as *Column name 'ClinicId' does not exist in the target table or view*. These are not real: the script creates those columns itself, earlier in the run. Only the **Messages** tab after you press Execute (4d) counts.

### 4d. Run it

1. Press **F5**, or click **Execute**.
2. Wait. The status bar shows *Executing query…* and then *Query executed successfully*.
3. In the **Messages** tab at the bottom you'll see many lines. The last ones say *Commands completed successfully* (or rows affected). There must be **no red error lines**.

A real error in the Messages tab looks like `Msg 1911, Level 16, State 1, Line 345` followed by the message. If any appears: **stop here** and don't run it again. Copy the red message (it holds no passwords) and send it to me. Each migration in the script runs in its own transaction, so a failed one leaves nothing half-applied.

### 4e. Check the result

Open a **New Query** on the same database, paste the following, and press **F5**:

```sql
SELECT TOP 1 MigrationId FROM __EFMigrationsHistory ORDER BY MigrationId DESC;
SELECT COUNT(*) AS Tables FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_TYPE = 'BASE TABLE';
SELECT COUNT(*) AS Migrations FROM __EFMigrationsHistory;
```

| Result | Expected |
|---|---|
| First result | A value ending in `_WriteOffReasons` (the newest migration) |
| Tables | Around 40 |
| Migrations | The number of migration files, around 30. Any number is fine as long as the first result is right |

The tables are empty apart from a few seed rows; the app adds the administrator, permission groups, visit types and a clinic when it first starts (Step 8). Close SSMS, or keep it open for later.

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

1. Leave the `0100` account as it is for now. There's no screen to rename it; D2d step 4.1 replaces it with your own admin account.
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
