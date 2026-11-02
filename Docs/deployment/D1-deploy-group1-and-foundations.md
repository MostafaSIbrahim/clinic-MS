# D1 — Deploy Group 1 and the foundations to the hosted sites

> **Status (2026-11-02): replaced by a fresh deployment later.** The owner confirmed that neither hosted database holds real data. Development continues locally. Both hosted sites stay online on the old code: the owner confirmed they hold no uploaded files and no sensitive data. Before the pilot (Group 3), both sites will be rebuilt from scratch: empty databases created by the full migration script, the first administrator seeded from `Seed:AdminPassword`, and no old uploads. That deployment gets its own guide. The step-by-step upgrade below is kept as the reference for the first upgrade **with** real data, which must follow it (backups, checks, migration script, file move).

First deployment since slice S2. Brings the **test site** (`safyaclinic.runasp.net`, site83728) and then the **demo site** (`safyaclinic-demo.runasp.net`, site89078) up to `main` after PR #18 (S3–S8, F2, F3, F5, F10).

| | |
|---|---|
| Why now | The hosted sites still serve patient attachments **without sign-in** (F01) and lack every security fix since S2 |
| Database | Five new migrations: `AddPasswordLockout`, `AddSecurityStamp`, `ReshapeAuditLogs`, `SchemaIntegrity`, `UniquePrimaryPhone`. **You** apply them with the reviewed idempotent script (S8), which skips anything already applied. The app no longer migrates itself, and it refuses to start while migrations are pending |
| Files | Attachments move from `wwwroot/uploads` to `App_Data/uploads` (S3) |
| Users notice | Everyone is signed out once (S6); passwords keep working and are upgraded at the next sign-in (S5); amounts show `EGP` (F2) |
| Time | ~1 hour for the test site; ~30 minutes for the demo site |
| Order | **Test site first.** Demo only after every test-site check passes |

You need: the MonsterASP control panel (File Manager, database backup, start/stop), SQL access to each site's database with **SSMS** (connection details are in the panel's database section), and Visual Studio with the existing publish profiles.

---

## Part A — Prepare (once)

**A1.** `main` is up to date and CI is green on it (PR #18 merged).

**A2.** Get the migration script:

```powershell
cd D:\MyDevWork\clinic-MS
git switch main
git pull
dotnet run --project SafyaClinic\tests\DeploymentChecks -- D:\MyDevWork\migrations-idempotent.sql
```

It must print `Model changes without a migration: False`. (The same file is in the latest `main` CI run → Artifacts → test-results.)

---

## Part B — Test site (site83728)

### B1 — Read-only checks on the test database (SSMS, connected to the test database)

```sql
-- 1. Where is this database?
SELECT MigrationId FROM __EFMigrationsHistory ORDER BY MigrationId;

-- 2. Audit table must be empty (F3b-1 reshapes it)
SELECT COUNT(*) AS AuditLogRows FROM AuditLogs;

-- 3. Shadow columns (F5a) — each must be 0 (skip a line if the column no longer exists)
SELECT COUNT(*) FROM MedicalAnalyses WHERE AnalysisTypeId1 IS NOT NULL AND AnalysisTypeId1 <> AnalysisTypeId;
SELECT COUNT(*) FROM WeeklyAdministeredItems WHERE PackageItemId1 IS NOT NULL AND PackageItemId1 <> PackageItemId;
SELECT COUNT(*) FROM Prescriptions WHERE PatientRecordId IS NOT NULL AND PatientRecordId <> RecordId;

-- 4. Longest content types (F5a) — must be <= 100
SELECT MAX(LEN(ContentType)) FROM AnalysisAttachments;
SELECT MAX(LEN(ContentType)) FROM PrescriptionAttachments;

-- 5. Duplicate phones (F5b) — the same report as in the F5b guide
WITH n AS (
  SELECT pp.Id, pp.PatientId, pp.IsPrimary, pp.PhoneNumber,
         REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(LTRIM(RTRIM(pp.PhoneNumber)),' ',''),'-',''),'(',''),')',''),'.',''),'+','') AS N
  FROM PatientPhones pp),
m AS (
  SELECT Id, PatientId, IsPrimary, PhoneNumber,
         CASE WHEN N LIKE '0020%' THEN '0' + SUBSTRING(N, 5, 50)
              WHEN N LIKE '20%' AND LEN(N) >= 11 THEN '0' + SUBSTRING(N, 3, 50)
              ELSE N END AS Normalized
  FROM n)
SELECT m.Normalized, m.PatientId, p.FirstName + ' ' + p.LastName AS Patient, m.IsPrimary, m.PhoneNumber AS Stored, m.Id AS PhoneId
FROM m JOIN Patients p ON p.Id = m.PatientId
WHERE m.Normalized IN (SELECT Normalized FROM m GROUP BY Normalized HAVING COUNT(DISTINCT PatientId) > 1)
   OR LEN(m.Normalized) < 7
ORDER BY m.Normalized, m.PatientId;
```

**Send me the results of 1, 2 and 5** (and anything non-zero in 3, or over 100 in 4). If 2 is not 0, or 5 lists rows, I will send exact statements before you continue. If 1 shows `20260725134425_PatientSourceAndPaymentOptions`, I will include its removal (S8).

### B2 — Back up

1. Control panel → database → **create a backup** of the test database; wait until it finishes.
2. File Manager → download the folder `wwwroot/uploads` (if it exists) to `D:\MyDevWork\deploy-backup\test\uploads`.

### B3 — Stop the site

Control panel → site83728 → **Stop**.

### B4 — Apply the migrations

SSMS → connected to the **test** database (check the database name in the title bar) → open `D:\MyDevWork\migrations-idempotent.sql` → **Execute**. It must finish with no errors. Re-run query 1 of B1: the last row is `20261102183431_UniquePrimaryPhone`.

If it stops with an error: do not continue. Restore is not needed for a failed migration (each runs in a transaction), but send me the message.

### B5 — Publish

Visual Studio → right-click `SafyaClinic.Web` → **Publish** → profile **site83728-WebDeploy** → Publish. (Server `appsettings.json` and uploads are never overwritten — publish rules from earlier slices.)

### B6 — Move the attachments (S3)

File Manager, in the site root:

1. Create `App_Data/uploads/analysis` and `App_Data/uploads/prescriptions`.
2. Upload the files from your downloaded backup (`…\uploads\analysis\*` → `App_Data/uploads/analysis`, same for `prescriptions`).
3. Delete the folder `wwwroot/uploads` on the server.

### B7 — Start and check

Control panel → **Start**. Then:

| # | Check | Expected |
|---|---|---|
| 1 | `https://safyaclinic.runasp.net/health` | `Healthy` |
| 2 | Sign in as admin | Works (you are signed out first — normal) |
| 3 | Open an existing analysis/prescription attachment | Preview and download work |
| 4 | Private window, not signed in: the old link `https://safyaclinic.runasp.net/uploads/analysis/<a file name>` | 404 |
| 5 | Upload a small PNG to a test analysis, then delete it | Works → the site can write to `App_Data` |
| 6 | File Manager → `App_Data/logs` | Today's `safya-YYYYMMDD.json` exists |
| 7 | Dashboard | Amounts show `EGP`; "today" is today |
| 8 | Users → reset a test user's password; sign in as them | Forced to change password |
| 9 | SSMS: `SELECT TOP 10 Action, UserId, OccurredAtUtc FROM AuditLogs ORDER BY Id DESC` | Your sign-in and the reset appear |

If check 5 fails (no write permission to `App_Data`): give the application pool write access to `App_Data` in the control panel (permissions), or ask MonsterASP support. Tell me if that is not possible.

**Send me the results.** Then the demo site.

---

## Part C — Demo site (site89078)

Same as Part B with these differences:

- B1–B4 against the **demo** database; profile **site89078-WebDeploy**; URLs `https://safyaclinic-demo.runasp.net/…`.
- The demo admin's existing password keeps working. Existing passwords are not forced to the new 10-character rule; they are only re-hashed at the next sign-in.
- ⚠ **Lockout on a public demo:** anyone typing the demo admin's password wrong 5 times locks it for 15 minutes. If that becomes a nuisance, tell me — we can raise the limit for the demo through the server's settings (`Security:Lockout:MaxFailedAttempts`).

---

## Afterwards

- Keep the database backups and the downloaded `uploads` folders for a week, then delete the local `uploads` copies (patient files).
- From now on, follow `Docs/deployment/database-migrations.md` for any release with new migrations, and `Docs/deployment/logs-and-health.md` for logs and monitoring.
