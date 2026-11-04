# Slice P1a: clinic opening hours

Pilot item **P1** (clinic hours and doctor schedules), slice a of 3. This is the first pilot slice. Its design and the 2026-11-04 decisions are in `P1-scheduling-design.md`.

**Claude implemented this slice at the owner's request.** The owner generates and reviews the migration, approves it, then builds, tests, checks and opens the PR.

| | |
|---|---|
| Branch | `feature/p1a-clinic-hours` (from the working copy, after PR #37 is merged) |
| Behaviour change | 1. **Clinic page → Opening hours:** a summary of the week, Saturday first, with **Edit opening hours**. 2. **Edit opening hours:** each weekday has **up to three periods** (from–to, 15-minute steps, clinic time). An empty day is **closed**. Overlapping periods, periods that close before they open, periods shorter than 15 minutes and half-filled rows are refused with a message, and nothing is saved. 3. Each change is **audited** (`Clinic.HoursChanged`, with the week before and after). 4. The screen and summary are in **English and Arabic**. **Booking is unchanged.** Hours start to matter in P2 |
| Who | People with **Manage clinics** (`Settings.Clinics`), today Administrators, as for the rest of the clinic page |
| Database | ⚠ **New table `ClinicOpeningHours`** (ClinicId, DayOfWeek, OpensAt, ClosesAt; check constraint *opens before closes*; index on clinic and day; removed with its clinic). Nothing existing changes. **Needs your approval** (Step 3) |
| Tests | +15 (about **311**). They cover: split shifts, touching vs overlapping periods, opens before closes, the 15-minute minimum, at most three periods a day, the Saturday-first week, the form (seven days with three rows each, empty rows ignored, half-filled rows flagged), and the service (saving replaces the whole week; invalid input changes nothing; changes are audited but unchanged saves aren't; unknown clinic; no hours means an empty week) |

## Files

| Action | Path |
|---|---|
| Add | `Domain/Entities/Settings/ClinicOpeningHours.cs`; edit `Clinic.cs` (`OpeningHours`) |
| Add | `Infrastructure/Data/Configurations/ClinicOpeningHoursConfiguration.cs`; edit `SafyaDbContext.cs`, `IUnitOfWork.cs`, `UnitOfWork.cs` |
| Add (you, Step 3) | `Infrastructure/Migrations/<timestamp>_ClinicOpeningHours.cs` + `.Designer.cs`; snapshot |
| Add | `Application/Scheduling/OpeningHours.cs` (`TimePeriod`, `OpeningPeriodDto`, `ClinicWeek`, `OpeningHoursRules`) |
| Add | `Application/Interfaces/Services/IClinicHoursService.cs`, `Application/Services/ClinicHoursService.cs`; edit `Common/Auditing/Audit.cs` (`ClinicHoursChanged`), `ApplicationServiceExtensions.cs` |
| Add | `Web/Models/ClinicHoursForm.cs`, `Web/Views/Clinics/Hours.cshtml` |
| Edit | `Web/Controllers/ClinicsController.cs` (`Hours` GET/POST; hours on `Details`), `Views/Clinics/Details.cshtml`, `wwwroot/css/site.css`, `Resources/SharedResource.ar.resx` |
| Edit | `tests/.../access-matrix.txt` (+ `Clinics.Hours GET/POST: Admin`); add `tests/.../Application/OpeningHoursTests.cs` |

## Rollback

- **Code:** revert the PR's merge commit.
- **Database (dev):**
  ```powershell
  dotnet ef database update DropRoles --project SafyaClinic\SafyaClinic.Infrastructure --startup-project SafyaClinic\SafyaClinic.Web
  ```
  This drops the table. Only the hours entered are lost.

---

## Your steps

Run these in PowerShell from `D:\MyDevWork\clinic-MS`.

### Step 1: Branch

Merge **PR #37** first. Then:

```powershell
git switch -c feature/p1a-clinic-hours
git status
```

### Step 2: Build and test

```powershell
dotnet build SafyaClinic\SafyaClinic.sln
dotnet test SafyaClinic\SafyaClinic.sln
```

Expected: about **311 passed**. The tests don't need the migration.

### Step 3: Migration (needs your approval)

**3a. Generate it:**

```powershell
Push-Location SafyaClinic\SafyaClinic.Web
dotnet ef migrations add ClinicOpeningHours --project ..\SafyaClinic.Infrastructure
Pop-Location
```

**3b. Review it.** `Up` must contain **only**:
- `CreateTable` `ClinicOpeningHours` with columns `Id`, `ClinicId`, `DayOfWeek` (int), `OpensAt` and `ClosesAt` (`time(0)`), a foreign key to `Clinics` with `onDelete: ReferentialAction.Cascade`, and the check constraint `CK_ClinicOpeningHours_Period`;
- `CreateIndex` `IX_ClinicOpeningHours_ClinicId_DayOfWeek`.

If it touches any other table, **stop and send me the file**.

**3c. Check the model and migrations agree:**

```powershell
dotnet run --project SafyaClinic\tests\DeploymentChecks -- D:\MyDevWork\migrations-idempotent.sql
```

It must print `Model changes without a migration: False`.

**3d. Back up, then start the app:**

```powershell
powershell -ExecutionPolicy Bypass -File .assessment\backup-db.ps1
dotnet run --project SafyaClinic\SafyaClinic.Web --launch-profile http
```

**Running 3d is your explicit approval** for this change to the **dev** database: one new, empty table, `ClinicOpeningHours`. Nothing existing changes.

### Step 4: Check

As **Admin**:

1. **Administration → Clinics →** open a clinic. A new **Opening hours** card says "No opening hours set yet."
2. **Edit opening hours.** Enter a split shift, e.g. **Saturday 10:00–14:00 and 17:00–21:00**, and a few other days. Leave **Friday** empty. Save. A green toast appears, and the card lists each day, with **Friday: Closed**.
3. **Try mistakes,** one at a time. Each shows a message and changes nothing:
   - Sunday 14:00–10:00 (closes before it opens);
   - Monday 10:00–15:00 and 14:00–18:00 (overlap);
   - Tuesday with only a "from" time.
4. **Arabic:** switch to العربية. The card is titled **مواعيد العمل** with Arabic day names, and the edit screen reads right to left.
5. **Phone width:** on the edit screen each day becomes a card with its three periods.
6. **Audit (optional, read-only):**
   ```sql
   SELECT TOP 3 Action, EntityId, Details FROM AuditLogs WHERE Action = 'Clinic.HoursChanged' ORDER BY Id DESC;
   ```
7. **Book a reservation** as before. It works exactly as it did, because hours aren't enforced until P2.

### Step 5: Commit, push, PR

```powershell
git add -A SafyaClinic Docs
git status
git commit -m "P1a: clinic opening hours per weekday (several periods a day), audited; English and Arabic"
git push -u origin feature/p1a-clinic-hours
```

Send me the PR number and the CI result.

## Next: P1b, doctor schedules

- For each doctor and clinic: open or specific-slots mode, a step, weekly working periods with breaks (open), or slots (specific).
- Overlaps across clinics refused; times kept inside clinic hours.
- New permissions `Schedule.ManageOwn` and `Schedule.ManageAll`.
- A "My schedule" screen for clinicians and "Doctor schedules" for the front desk.
