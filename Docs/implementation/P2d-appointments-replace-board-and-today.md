# Slice P2d: Appointments replaces the Board and Today

Pilot item **P2** (slot booking), slice d. This slice **closes P2**.

**Claude implemented this slice at the owner's request.** The owner removes three old views, runs one command, does the manual check, and pushes.

| | |
|---|---|
| Branch | `feature/p2d-appointments-only` (from the working copy, after PR #43 is merged) |
| Behaviour change | **1.** **Appointment Board** leaves the menu. `Reservations/Board` (old links, bookmarks) **redirects to Appointments** for the same day and clinic. **2.** `Reservations/Today` **redirects to today's Appointments**. **3.** Appointments now shows each booking's **coverage badge** ("Covered" / "Not fully covered") and, for people who can see payments, a **Payment summary** link, which is what Today showed. **4.** **The Reservations list stays** in the menu as the place to **find** reservations (by patient, status and dates, including past visits). Its header now has **Appointments** and **New Reservation (full form)**; the full form is still needed for an administrator's price override. **5.** Unchanged: reservation details, edit, cancel, Move, the queue and the dashboard |
| Why the list stays | The roadmap planned to replace List, Board and Today. Board and Today are fully covered by Appointments, but the list is a search across any dates, which Appointments isn't. Keeping it avoids losing that. If you'd rather drop it too, it can go with the patient workspace (M1) |
| Database | None |
| Tests | Menu tests updated (no Appointment Board), and the day test now also checks the coverage data. Still about **368** |

## Files

| Action | Path |
|---|---|
| **Remove** (you, Step 1) | `Views/Reservations/Board.cshtml`, `Views/Reservations/_BoardAppointment.cshtml`, `Views/Reservations/Today.cshtml` |
| Edit | `Web/Controllers/ReservationsController.cs` (`Today` and `Board` redirect), `Web/Navigation/NavigationCatalogue.cs` (no Appointment Board) |
| Edit | `Application/Scheduling/AppointmentDay.cs`, `Services/AppointmentsService.cs` (patient id and coverage on bookings), `Views/Appointments/Index.cshtml` (coverage badge) |
| Edit | `Views/Reservations/Index.cshtml` (header links) |
| Edit | tests: `NavigationCatalogueTests.cs`, `AppointmentsServiceTests.cs` |

`ReservationService.GetAppointmentBoardAsync` and its DTO are now unused. They're left in place and can be removed in the repository-hygiene pass.

## Rollback

Revert the PR's merge commit. This brings back the three views.

---

## Your steps

After merging **PR #43**, from `D:\MyDevWork\clinic-MS`:

```powershell
git rm SafyaClinic\SafyaClinic.Web\Views\Reservations\Board.cshtml SafyaClinic\SafyaClinic.Web\Views\Reservations\_BoardAppointment.cshtml SafyaClinic\SafyaClinic.Web\Views\Reservations\Today.cshtml
.\.assessment\slice.ps1 -Branch feature/p2d-appointments-only `
  -Message 'P2d: Appointments replaces the Appointment Board and Today (redirects; coverage badge on bookings); Reservations list kept for search'
```

There's no migration, so there's no database approval step.

### Manual check (while the app runs)

1. **Menu (Admin, Reception, Doctor):** Front desk shows Patients · **Appointments** · Reservations · Queue · Follow-ups · schedule items. There's no Appointment Board.
2. **Old links:** open `/Reservations/Today` and `/Reservations/Board`. Both land on Appointments (Board keeps `?date=…&clinicId=…`).
3. **Coverage:** on Appointments, each booking shows "Covered" or "Not fully covered". As Reception there's also **Payment summary**, which opens the patient's payments.
4. **Reservations list:** search by a patient or a past date range. It still works, and the header shows **Appointments** and **New Reservation (full form)**.
5. **Dashboard and Queue** look as before.

When you've finished, press Enter in the script, then type `YES` to commit and push. Open the PR and send me its number and the CI result.

## Where we are after this

**P1 (schedules) and P2 (slot booking) are complete.** Next in the pilot:
- **P3, find and register patients:** global search that opens a patient in one step, and registration that blocks an existing primary phone with a link to that patient.
- **P4:** the check-in desk and doctor queue.
- **P5:** the consultation workspace.
- **P6:** payment coverage and write-offs.
- **P7:** acceptance.
