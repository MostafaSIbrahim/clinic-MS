# Slice P9b: locked cancellations, and automatic No show

From the owner's manual test of 11 Nov 2026 (comments 2 and 3), with the owner's choices: **24 hours after the appointment time**; status **No show**; **reception supervisors, clinic managers and admin** may reopen; the lock applies to **Cancelled and No show**.

**Claude implemented this slice at the owner's request.** The owner runs one command, does the manual check, and pushes.

| | |
|---|---|
| Branch | `feature/p9b-reopen-and-no-show` (from `main` after PR #65; your folder is on it) |
| Behaviour change | **1. Cancelled and No-show reservations are locked.** Once a reservation is Cancelled or No show, its status can only be changed by people with the new permission **Reopen cancelled and no-show reservations** (`Reservations.Reopen`). This applies on every screen, and the server refuses it, so it's not just a hidden button. Out of the box that's **Reception supervisors**, **Clinic managers** and **Administrators**; you can change it on the Permission Groups screen. For everyone else, the reservation page shows the status disabled with *"Only a supervisor can change its status"*, and there's no **Cancel** button on a No-show reservation. **2. Automatic No show.** A **Pending** or **Confirmed** reservation not checked in by **24 hours after its appointment time** (clinic time) becomes **No show**. Each change is in the audit log as *Reservation.AutoNoShow*, with no user (the system). It runs **30 seconds after the app starts** and then **every hour**. On MonsterASP the app may sleep when nobody uses it; the first visit wakes it, and the check runs then. **3. Unchanged:** cancelling an open reservation, check-in, Move (already impossible for Cancelled and No show) |
| Database | None. The new permission is added to the catalogue at start-up and given to the supervisors' and clinic managers' groups, as the seeder does for any new permission |
| Tests | +9: who may change a Cancelled or No-show reservation (8 cases), and the automatic No show. That test runs as of Friday 6 Nov 2026 12:00 clinic time: visits 48 hours and 25 hours old become No show; a visit 23 hours old, one checked in, one cancelled and today's visit stay as they were. Each change is audited, and running it again changes nothing. The group permission table is updated |

## Files

| Action | Path |
|---|---|
| Add | `Web/Startup/NoShowSweeper.cs` (the hourly check), `tests/.../Application/NoShowAndReopenTests.cs` |
| Edit | `Application/Security/PermissionCatalogue.cs` (new permission, given to supervisors and clinic managers), `Application/Services/ReservationRules.cs` (the lock), `Application/Services/ReservationService.cs` (current status checked on every status change and cancel; `MarkMissedAsNoShowAsync`), `Interfaces/Services/IReservationService.cs`, `Common/Auditing/Audit.cs` |
| Edit | `Web/Program.cs` (starts the check), `Web/Views/Reservations/Details.cshtml` (status disabled when locked) |
| Edit | tests: `PermissionCatalogueTests.cs` |

## Rollback

Revert the PR's merge commit. Reservations already marked No show stay so. A supervisor can reopen any that should be different.

---

## Your steps

In PowerShell:

```powershell
.\.assessment\slice.ps1 -Branch feature/p9b-reopen-and-no-show `
  -Message 'P9b: cancelled and no-show reservations reopened only by supervisors; reservations not checked in become No show 24 hours after the appointment'
```

### Manual check (while the app runs)

**Before you start**, so step 4 has something to change: booking screens refuse past times, so book a **test patient** for later today, leave it Confirmed and not checked in, and move it two days back in your **local** database with SQL. It's your local test data; don't run this on the live site.

```sql
-- replace 123 with the test reservation's number (shown on its page as Reservation #123)
UPDATE Reservations SET ReservationDate = DATEADD(day, -2, CAST(GETDATE() AS date)) WHERE Id = 123;
```

1. **As Reception**, cancel a test reservation, then open it: the status drop-down and **Update** are disabled, with the message.
2. **As a supervisor**, open the same reservation: you can set it back to **Pending** or **Confirmed**.
3. **As Reception**, a reservation marked **No show** can't be changed. **As a supervisor**, it can.
4. **Automatic No show:** restart the app, wait about a minute, and refresh the reservation from *Before you start*: it shows **No show**. A reservation from today, or one checked in, is unchanged. Optionally, check the audit log for *Reservation.AutoNoShow*:
   ```sql
   SELECT TOP 10 OccurredAtUtc, Action, EntityId, Details FROM AuditLogs WHERE Action = 'Reservation.AutoNoShow' ORDER BY Id DESC;
   ```
5. **Permission Groups** → *Reception supervisors*: the new permission *Reopen cancelled and no-show reservations* is ticked. For *Reception* it isn't.

When you've finished, press Enter in the script, then type `YES` to commit and push. Open the PR, merge it, run `git switch main` and `git pull`, and send me its number and the CI result.

## Next

**M2d**, which closes M2: the payments report with the clinic clock, Arabic, totals by method and doctor, **Download as CSV**, and removal of the two old repair actions.
