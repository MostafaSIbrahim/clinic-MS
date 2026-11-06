# Slice P2c: move a booking, and the week view

Pilot item **P2** (slot booking), slice c. This slice finishes D7's **reschedule flow**: a calendar change that would strand bookings now links straight to moving them.

**Claude implemented this slice at the owner's request.** The owner runs one command, does the manual check, and pushes.

| | |
|---|---|
| Branch | `feature/p2c-move-booking` (from the working copy, after PR #42 is merged) |
| Behaviour change | **1. Move a booking:** a **Move** link on each booking in the Appointments day and a **Move** button on the reservation page (for people who can manage reservations). It opens the booking's summary and **the same doctor's free times**. You can pick another **date** and **clinic**; the booking's own current time doesn't count as taken. Clicking a time asks for confirmation and moves it. The server re-checks the time (P2a rules, under the doctor's lock). Only bookings that are upcoming and **not yet checked in** can be moved. **History is kept:** each move is written to the audit log (`Reservation.Moved`, from and to). Price, patient, treatment, length and payments are unchanged. **2. The guard links to Move:** when saving a calendar, a date change or clinic hours is refused because of bookings, the page now lists **"Move booking #41"** buttons above the form. People who can't manage reservations, such as a Reception supervisor setting calendars, see the numbers with "Ask the front desk to move them". **3. Week view:** Appointments → **Week view** shows a clinic's week (Saturday first) as a table: per doctor and day, **"n booked · m free"**, each linking to that day. It has previous / this / next week links. **4.** English and Arabic |
| Database | None |
| Tests | +4 (about **368**). They cover: moving offers the doctor's free times without counting the booking itself; a booking moves to a free time in another clinic and the move is audited; it can't move onto another booking or outside the hours; cancelled or checked-in bookings can't be moved |

## Files

| Action | Path |
|---|---|
| Edit | `Application/Services/ReservationService.cs` (`MoveReservationAsync`; optional audit), `Interfaces/Services/IReservationService.cs`, `Common/Auditing/Audit.cs` (`Reservation.Moved`) |
| Edit | `Application/Scheduling/AppointmentDay.cs` (`MoveOptionsDto`), `Interfaces/Services/IAppointmentsService.cs`, `Services/AppointmentsService.cs` (`GetMoveAsync`) |
| Edit | `Web/Controllers/AppointmentsController.cs` (`Move` GET/POST, `Week`), `Web/Models/AppointmentModels.cs` |
| Add | `Views/Appointments/Move.cshtml`, `Views/Appointments/Week.cshtml`, `Views/Shared/_StrandedBookings.cshtml` |
| Edit | `Views/Appointments/Index.cshtml` (Move links, Week view), `Views/Reservations/Details.cshtml` (Move button), `Views/Schedules/Edit.cshtml`, `Views/Clinics/Hours.cshtml` (stranded list) |
| Edit | `Web/Controllers/BaseController.cs` (`NoteStrandedBookings`), `SchedulesController.cs`, `ClinicsController.cs`, `Resources/SharedResource.ar.resx` |
| Edit | tests: `access-matrix.txt` (+3), `Application/AppointmentsServiceTests.cs` (+4) |

## Rollback

Revert the PR's merge commit. Moves already made stay as they are, and they remain in the audit log.

---

## Your steps

After merging **PR #42**, from `D:\MyDevWork\clinic-MS`:

```powershell
.\.assessment\slice.ps1 -Branch feature/p2c-move-booking `
  -Message 'P2c: move a booking to another free time (audited), stranded-booking Move links, week view'
```

There's no migration, so there's no database approval step.

### Manual check (while the app runs)

1. **Move from the day:** on **Appointments**, next Saturday, book a patient with Dr X at 10:30. Click **Move** next to the booking. The page shows the patient, doctor and "Now: Sat … 10:30", plus Dr X's free times, without 10:30 itself.
2. **Click 11:30 → confirm.** You're back on the day: the booking is at 11:30 and 10:30 is free again.
3. **Other clinic / date:** Move again, pick another clinic or date, click **Show**, then pick a time. The booking moves there.
4. **Taken meanwhile:** open Move in two windows and move both to the same time. The second is refused with the reason.
5. **Not movable:** check a patient in (Today/queue), then open Move for that booking. "Only upcoming bookings that haven't been checked in can be moved", with no times offered.
6. **Guard → Move:** give the doctor a calendar that leaves out a booked time, or add a day off on a booked date. The refusal now lists **Move booking #…**. Click it, move the booking, then save the calendar again. It works.
7. **As mm:** setting such a calendar shows the numbers and "Ask the front desk to move them" (no Move buttons).
8. **Reservation page:** the **Move** button appears for upcoming bookings and opens the same screen.
9. **Week view:** Appointments → **Week view**. Rows are doctors, columns are Sat…Fri, and cells read "n booked · m free"; click one to open that day.
10. **Audit (optional, read-only):** `SELECT TOP 5 Action, EntityId, Details FROM AuditLogs WHERE Action = 'Reservation.Moved' ORDER BY Id DESC;`

When you've finished, press Enter in the script, then type `YES` to commit and push. Open the PR and send me its number and the CI result.

## Next: P2d (closes P2)

- Make **Appointments** the one place for the front desk.
- Retire the old **Reservations list / Appointment Board / Today** screens: their menu entries go, and the old links redirect to the matching Appointments view.
- Reservation details, Edit and the queue stay.
