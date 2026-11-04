# Slice P1b: doctor schedules

Pilot item **P1**, slice b of 3. The design and decisions are in `P1-scheduling-design.md`.

**Claude implemented this slice at the owner's request.** The owner runs one command (`.assessment\slice.ps1`), approves the database change, does the manual check, and pushes.

| | |
|---|---|
| Branch | `feature/p1b-doctor-schedules` (from the working copy, after PR #38 is merged) |
| Behaviour change | **1. A calendar per doctor and clinic,** with: **mode** (*Open calendar*: bookable at any step in the working periods, except breaks; or *Specific slots*: bookable only in the slots set); **booking step** (5, 10, 15, 20, 30 or 60 minutes); **in use** on or off; and a **weekly pattern**, Saturday first. Open calendars have up to 3 working periods and 2 breaks a day; specific-slots calendars have up to 4 slots a day. The form shows the clinic's hours for each day. **2. Rules:** working periods and slots must be inside the clinic's opening hours, and nothing can be set on a day the clinic is closed. Breaks must be inside a working period. Slots must be at least one step long, and nothing may overlap. **A doctor can't be in two clinics at the same time**, though moving between them (touching times) is fine. A calendar in use needs at least one working period or slot. **3. Who:** clinicians see **Front desk → My schedule** and set their own. **Front desk → Doctor schedules** lists every clinician in every clinic and lets the holder set anyone's. Administrators and Reception supervisors have it by default; you can change this on the Permission Groups screen. **4. Audit:** each change is logged as `Schedule.Changed`, with the calendar before and after. **5.** English and Arabic. **Booking is still unchanged.** Calendars start limiting bookings in P2; a doctor without a calendar stays bookable in clinic hours (decision S4) |
| New permissions | `Schedule.ManageOwn` (Doctors, Nutritionists; Administrators) and `Schedule.ManageAll` (Reception supervisors; Administrators). **The seeder now grants a permission that is new to the catalogue to its built-in groups once.** Grants an admin removes later stay removed |
| Database | ⚠ **Two new tables:** `DoctorSchedules` (doctor, clinic, mode, step, in use; one per doctor and clinic; check step 5–60; removed with its clinic; a doctor with a calendar can't be deleted) and `DoctorScheduleTimes` (day, kind, start, end; check start before end; removed with its calendar). **At start-up the seeder adds the 2 permissions and their default grants.** Nothing existing changes. **Needs your approval** (the script asks) |
| Tests | +23 (about **334**). They cover the rules (split shift with a break, inside clinic hours, closed day, break inside working, overlaps, slot length, each mode's own kind, the offered steps, availability required when in use, the two-clinics overlap versus touching), the form (only the chosen mode is saved; clinic hours shown; half-filled rows), the service (the default calendar, create then replace with audit, an overlap with another clinic unless that calendar isn't in use, only clinicians, invalid input changes nothing), who may edit (own versus anyone's), the seeder's one-time grant, and the menu |

## Files

| Action | Path |
|---|---|
| Add | `Domain/Enums/ScheduleMode.cs` (`ScheduleMode`, `ScheduleTimeKind`), `Domain/Entities/Scheduling/DoctorSchedule.cs` |
| Add | `Infrastructure/Data/Configurations/DoctorScheduleConfiguration.cs`; edit `SafyaDbContext.cs`, `IUnitOfWork.cs`, `UnitOfWork.cs`, `DbSeeder.cs` (one-time grant of new permissions) |
| Add (script) | `Infrastructure/Migrations/<timestamp>_DoctorSchedules.cs` + `.Designer.cs`; snapshot |
| Add | `Application/Scheduling/DoctorSchedules.cs` (DTOs, `DoctorScheduleRules`), `Interfaces/Services/IDoctorScheduleService.cs`, `Services/DoctorScheduleService.cs` |
| Edit | `Application/Security/PermissionCatalogue.cs` (2 permissions, default grants), `Common/Auditing/Audit.cs`, `ApplicationServiceExtensions.cs` |
| Add | `Web/Controllers/SchedulesController.cs`, `Web/Models/DoctorScheduleForm.cs`, `Views/Schedules/Index.cshtml`, `Mine.cshtml`, `Edit.cshtml`, `_ScheduleList.cshtml`, `wwwroot/js/schedule-form.js` |
| Edit | `Web/Navigation/NavigationCatalogue.cs` (My schedule, Doctor schedules), `Resources/SharedResource.ar.resx` |
| Edit | tests: `access-matrix.txt`, `PermissionCatalogueTests.cs`, `NavigationCatalogueTests.cs`, `DbSeederTests.cs`; add `Application/DoctorScheduleTests.cs` |

## Rollback

- **Code:** revert the PR's merge commit.
- **Database (dev):** `dotnet ef database update ClinicOpeningHours --project SafyaClinic\SafyaClinic.Infrastructure --startup-project SafyaClinic\SafyaClinic.Web` drops both tables. The two permission rows and their grants stay, but they do nothing without this code; they can be removed on the Permission Groups screen.

---

## Your steps

After merging **PR #38**, from `D:\MyDevWork\clinic-MS`:

```powershell
.\.assessment\slice.ps1 -Branch feature/p1b-doctor-schedules -Migration DoctorSchedules `
  -Expect 'CreateTable DoctorSchedules','CreateTable DoctorScheduleTimes','CreateIndex IX_DoctorSchedules_ClinicId','CreateIndex IX_DoctorSchedules_DoctorId_ClinicId','CreateIndex IX_DoctorScheduleTimes_ScheduleId_DayOfWeek' `
  -Require 'CK_DoctorSchedules_Step','CK_DoctorScheduleTimes_Period','ReferentialAction.Restrict' `
  -Message 'P1b: doctor calendars per clinic (open or specific slots, step, breaks), no overlap across clinics; Schedule permissions'
```

**Typing `YES` at the approval prompt is your explicit approval** for the **dev** database: two new, empty tables, plus 2 new permission rows and their default grants, which the seeder adds at start-up.

### Manual check (while the app runs)

1. **Clinic hours first.** Give **two clinics** opening hours for Saturday, e.g. 10:00–21:00 (Clinics → clinic → Edit opening hours).
2. **As a Doctor:**
   - **Front desk → My schedule** lists your clinics as "Not set: bookable in clinic hours".
   - **Set calendar** for clinic A: Open calendar, 15 minutes, Saturday working 10:00–14:00 with a break 12:00–12:30. Save; the row now shows "Open calendar · 15-minute step".
   - **Clinic B,** Saturday 13:00–16:00. It's **refused**: it overlaps clinic A. **14:00–16:00 is accepted.**
   - **Try mistakes,** one at a time: a working period outside the clinic hours; a break outside working; Sunday when the clinic is closed. Each shows a message and nothing changes.
   - **Specific slots:** switch the mode; the working-periods section hides and the slots section shows. Add Saturday 10:00–11:00 and save.
   - **Another doctor's calendar,** by editing the doctorId in the address bar: **Access denied**.
3. **As mm (Reception supervisor):** **Front desk → Doctor schedules** lists every doctor in every clinic, and mm can edit any calendar.
4. **As Reception:** no schedule links in the menu.
5. **Permission Groups → Doctors:** "Set their own calendar" is ticked.
6. **Arabic:** the menu shows جدولي / جداول الأطباء, and the calendar form reads right to left.
7. **Booking** works exactly as before.

When you've finished, press Enter in the script, then type `YES` to commit and push. Open the PR and send me its number and the CI result.

## Next: P1c

- Date changes: a day off, extra or removed slots on a date.
- The free-slot function that P2 will use, with tests.
- The guard that blocks removing availability holding future bookings until each is moved.
