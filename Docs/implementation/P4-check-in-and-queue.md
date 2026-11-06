# Slice P4: check-in desk and doctor queue

Pilot item **P4**. Most of the roadmap item already existed. The queue shows doctors their own patients and the front desk everyone's, through the `Reservations.ViewQueue` / `ViewAllQueues` permissions. D6 (a doctor never changes another doctor's reservation status) is enforced by the server since S7/F4. This slice adds what was missing.

**Claude implemented this slice at the owner's request.** The owner runs one command, does the manual check, and pushes.

| | |
|---|---|
| Branch | `feature/p4-check-in-queue` (from the working copy, after PR #45 is merged) |
| Behaviour change | **1. Check in from Appointments.** On **today's** day, each booking that hasn't arrived yet has a **Check in** button (for people with *Check patients in*). It checks the patient in and returns to the same day. Once checked in, the booking shows its **queue status** instead (Waiting, In Consultation, Finished or Left). Cancelled, no-show and completed bookings get no button. **2. Open the queue** link on today's Appointments day, for the same clinic. **3. The queue updates itself every 30 seconds**, but only while the page is visible and nobody is typing in a field or has a dialog open, so nothing is lost mid-action. An **Auto-update on / paused** button pauses it, and the choice is remembered on that browser. Before, the queue only changed when you refreshed. **4.** The check-in return address must be on this site; otherwise it opens the reservation, as before. Checking in from the reservation page is unchanged |
| Database | None |
| Tests | +4 .NET (about **381**): check-in returns to the same Appointments day; without a local return address (missing, another site, `//host`) it opens the reservation; the day shows a checked-in booking's queue status. +2 JavaScript: auto-update runs only when on, visible and idle |

## Files

| Action | Path |
|---|---|
| Edit | `Application/Scheduling/AppointmentDay.cs`, `Services/AppointmentsService.cs` (queue status on bookings) |
| Edit | `Web/Controllers/ReservationsController.cs` (`CheckIn` returns to a local `returnUrl`), `Views/Appointments/Index.cshtml` (Check in, queue status, Open the queue), `Views/Reservations/Queue.cshtml` (auto-update) |
| Add | `wwwroot/js/auto-refresh.js`, `tests/auto-refresh.test.cjs`, `tests/.../Web/CheckInTests.cs` |
| Edit | `Resources/SharedResource.ar.resx`, `tests/.../Application/AppointmentsServiceTests.cs` |

## Rollback

Revert the PR's merge commit.

---

## Your steps

After merging **PR #45**, from `D:\MyDevWork\clinic-MS`:

```powershell
.\.assessment\slice.ps1 -Branch feature/p4-check-in-queue `
  -Message 'P4: check in from Appointments, queue status on bookings, self-updating queue (pausable)'
```

There's no migration, so there's no database approval step.

### Manual check (while the app runs)

1. **As Reception, Appointments → today.** A booking that hasn't arrived has **Check in**. Click it: you stay on the same day, with a green toast, and the booking now shows **Waiting**.
2. **Open the queue** (from the day). The patient is in the queue.
3. **Second window as the Doctor:** My Queue shows the patient. **Start consultation** there. Within 30 seconds the Reception queue window shows **In Consultation** without you refreshing.
4. **Pause:** click **Auto-update on**; it becomes **Auto-update paused**, and nothing reloads. Reload the page yourself and it stays paused. Click it again to resume.
5. **While typing:** click into the queue's filter field and wait more than 30 seconds. The page doesn't reload while you're in it.
6. **Doctor rule (D6):** as Doctor B, try to change Doctor A's reservation status. It's refused, as before.
7. **Another day:** Appointments for tomorrow has no Check in buttons.

When you've finished, press Enter in the script, then type `YES` to commit and push. Open the PR and send me its number and the CI result.

## Next: P5, the consultation workspace

- The patient header with the allergy warning on the consultation, record and prescription screens.
- Notes, prescriptions and lab orders on one screen.
- **Complete visit** requires a medical record.
- A tablet layout (1024).

P5 touches clinical data entry, so I'll show you the plan first.
