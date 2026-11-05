# Slice P1c: date changes, free times and the booked-time guard

Pilot item **P1**, slice c of 3. This slice **closes P1**. The design is in `P1-scheduling-design.md`.

**Claude implemented this slice at the owner's request.** The owner runs one command, approves the database change, does the manual check, and pushes.

| | |
|---|---|
| Branch | `feature/p1c-date-changes` (from the working copy, after PR #39 is merged) |
| Behaviour change | **1. Date changes** on the calendar page. A date can be a **day off**, or **different times that day** (working periods and breaks for an open calendar, slots for specific slots). A date change replaces that day's weekly pattern for that date only, and it can be removed again. The rules are the same as for a weekday: inside clinic hours, no overlaps, and no clash with the doctor's other clinics on that date. Only today or later dates are allowed. **2. Free times:** pick a date and a visit length to see the start times that booking will offer in P2. **3. The booked-time guard:** saving a weekly calendar, a date change, removing a date change, marking a calendar *not in use*, or **changing clinic opening hours** is **refused if it would leave a future booking outside the doctor's hours**. The message names each booking by **number, date and time** (no patient details), for example "#41 Sat 07 Nov 2026 18:00". Move or cancel those bookings first. Cancelled, no-show and completed bookings don't count. Bookings that were already outside the hours before the change aren't reported. **4.** English and Arabic. **Booking itself is still unchanged** until P2 |
| Rules (decided here) | *Not in use* means the doctor isn't bookable in that clinic. Only a doctor with **no** calendar falls back to the clinic's hours (S4). Free times start at each period's start and step by the calendar's step (15 minutes without a calendar). The whole visit must fit, must not overlap any of the doctor's bookings **in any clinic**, and must not be in the past |
| Database | ⚠ **One new table, `DoctorScheduleChanges`:** schedule, date, day off, kind, start and end. A check constraint requires either a day off with no times, or a period whose start is before its end. Indexed on schedule and date; removed with its calendar. Nothing existing changes. **Needs your approval** (the script asks) |
| Tests | +14 (about **346**). They cover the bookable periods (no calendar, working minus breaks clipped to clinic hours, not in use, a closed day, a date change for that date only), free starts (step and fit, the doctor's bookings in any clinic, past dates and times), the guard (only bookings that lose their time), and with the database: a first calendar that leaves out a booking is refused and names it; cancelled bookings don't block; a day off on a booked date is refused; replacing and removing a date change; date changes need a calendar, a future date and valid times; clinic hours that leave out a booking are refused |

## Files

| Action | Path |
|---|---|
| Edit | `Domain/Entities/Scheduling/DoctorSchedule.cs` (+ `DoctorScheduleChange`), `Infrastructure/Data/Configurations/DoctorScheduleConfiguration.cs`, `SafyaDbContext.cs`, `IUnitOfWork.cs`, `UnitOfWork.cs` |
| Add (script) | `Infrastructure/Migrations/<timestamp>_DoctorScheduleChanges.cs` + `.Designer.cs`; snapshot |
| Add | `Application/Scheduling/Availability.cs` (the pure calculation: periods, free starts, stranded bookings), `Application/Services/CalendarData.cs` (loading) |
| Edit | `Application/Services/DoctorScheduleService.cs` (guard; date changes; free starts), `ClinicHoursService.cs` (guard), `Interfaces/Services/IDoctorScheduleService.cs`, `Scheduling/DoctorSchedules.cs`, `Common/Auditing/Audit.cs` (`Schedule.DateChanged`) |
| Edit | `Web/Controllers/SchedulesController.cs` (`AddDate`, `RemoveDate`, free-times preview), `Web/Models/DoctorScheduleForm.cs` (`DateChangeForm`), `Views/Schedules/Edit.cshtml`, `wwwroot/js/schedule-form.js`, `wwwroot/css/site.css`, `Resources/SharedResource.ar.resx` |
| Edit/Add | tests: `access-matrix.txt`, `DoctorScheduleTests.cs`; add `Application/AvailabilityTests.cs` |

## Rollback

- **Code:** revert the PR's merge commit.
- **Database (dev):** `dotnet ef database update DoctorSchedules --project SafyaClinic\SafyaClinic.Infrastructure --startup-project SafyaClinic\SafyaClinic.Web` drops the table. Only the date changes entered are lost.

---

## Your steps

After merging **PR #39**, from `D:\MyDevWork\clinic-MS`:

```powershell
.\.assessment\slice.ps1 -Branch feature/p1c-date-changes -Migration DoctorScheduleChanges `
  -Expect 'CreateTable DoctorScheduleChanges','CreateIndex IX_DoctorScheduleChanges_ScheduleId_Date' `
  -Require 'CK_DoctorScheduleChanges_Period','ReferentialAction.Cascade' `
  -Message 'P1c: calendar date changes (day off, different times), free-time preview, guard against stranding future bookings'
```

**Typing `YES` at the approval prompt is your explicit approval** for the **dev** database: one new, empty table, with nothing existing changed.

### Manual check (while the app runs)

Use a clinic with Saturday hours (e.g. 10:00–14:00 and 17:00–21:00) and a doctor.

1. **Free times without a calendar:** open the doctor's calendar for that clinic (My schedule or Doctor schedules → Set calendar). Under **Free times**, pick next Saturday with 30 minutes. You get every 15 minutes from 10:00 to 13:30 and from 17:00 to 20:30.
2. **Book a test reservation** for that doctor next Saturday at **18:00**, in the usual way.
3. **Guard:** set the weekly calendar to Saturday **10:00–14:00 only** and save. It's **refused**, naming the booking "#… Sat … 18:00". Add **17:00–21:00** as a second working period; it saves.
4. **Free times again** for next Saturday: 18:00 and the times overlapping it (17:45, 18:15) are gone.
5. **Date change:** add a **day off** for next Saturday. It's **refused** because of the 18:00 booking. Cancel that reservation, try again, and the day off saves. Free times for that Saturday: none.
6. **Different times:** add the Saturday after with *Different times that day*: working 17:00–19:00. Free times that day are only 17:00–18:30. **Remove** the change, and the weekly times return.
7. **Clinic hours guard:** book again next-next Saturday at 18:00, then try to set the clinic's Saturday hours to 10:00–14:00 only. It's **refused**, naming the booking.
8. **Arabic:** the new sections read تغييرات بتواريخ محددة and الأوقات المتاحة, right to left.

When you've finished, press Enter in the script, then type `YES` to commit and push. Open the PR and send me its number and the CI result.

## Where we are after this

**P1 is complete.** Next is **P2, slot booking:**
- one appointments screen (day and week, per doctor or clinic);
- booking by clicking a free time from this calculation;
- the server re-checks inside the existing reservation lock, so double booking is impossible;
- price from the treatment type;
- the reschedule flow.

The guard's "move or cancel first" then gets a **Move** button.
