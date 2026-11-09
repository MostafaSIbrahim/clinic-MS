# Slice P4b: "Not checked in yet" on the queue, and no Move after check-in

A follow-up to **P4**, from your manual test on 8 Nov.

**Claude implemented this slice at the owner's request.** The owner runs one command, does the manual check, and pushes.

## What you saw, and why

| You saw | Why it happens |
|---|---|
| A confirmed booking for today **didn't appear in the doctor's queue** | The queue shows only patients who have **arrived**, i.e. been checked in (Waiting or In consultation). A confirmed booking that hasn't been checked in wasn't shown anywhere on the queue page |
| After the check-in, **the status was still Confirmed** for the receptionist | A booking has **two** statuses. The **reservation status** (Pending, Confirmed, Completed, Cancelled, No-show) stays **Confirmed** for the whole visit, until it's Completed. Check-in changes the **queue status** instead: Not checked in → **Waiting** → In consultation → Finished. On the reservation page this is the **Patient Queue** card; on Appointments it's the badge next to the booking |
| **To check:** how did the doctor check the patient in? | The built-in **Doctors** group doesn't have **Check patients in**. The button is shown only to the front desk and Admin. If the doctor's account was in Doctors only, there was no Check In button to press. If the patient still doesn't appear as **Waiting** after the front desk checks them in, tell me which page and button were used, and I'll trace it |

## What this slice changes

| | |
|---|---|
| Branch | `feature/p4b-queue-arrivals` (from the working copy, after the P6a PR is merged) |
| Behaviour change | **1. "Not checked in yet" on the queue page.** Below the queue, a new section lists **today's Pending or Confirmed bookings that haven't been checked in**, by time. It uses the same clinic and doctor filters. A doctor sees **only their own**, so they know who's expected before the patient arrives. People who can check patients in (front desk, Admin) get a **Check In** button there, which returns to the queue. The patient then moves up into the queue as **Waiting**. It refreshes with the queue every 30 seconds. **2. Move is hidden once the patient is checked in.** On **Appointments** and on the **reservation page**, **Move** appears only for bookings that haven't been checked in (and aren't completed, cancelled or no-show). Before, the link showed and the Move page then refused. The server's rule is unchanged and still refuses |
| Not changed | The two statuses themselves, and who can check in (Doctors still can't, unless you add **Check patients in** to their group in Permission groups) |
| Known edge | The calendar guard's "Move booking #…" buttons (after a refused calendar change) still list every booking that's in the way. For one that's already checked in, the Move page says it can't be moved |
| Database | None |
| Tests | +1 (the P6a total plus one): today's not-checked-in bookings are listed by time and by doctor, leaving out those already in the queue, cancelled bookings and other days |

## Files

| Action | Path |
|---|---|
| Edit | `Application/Services/ReservationService.cs`, `Interfaces/Services/IReservationService.cs` (`GetExpectedArrivalsAsync`) |
| Edit | `Web/Controllers/ReservationsController.cs`, `Web/Models/DoctorQueueViewModel.cs`, `Views/Reservations/Queue.cshtml` (Not checked in yet) |
| Edit | `Views/Appointments/Index.cshtml`, `Views/Reservations/Details.cshtml` (Move only before check-in) |
| Edit | `tests/.../Application/AppointmentsServiceTests.cs` |

## Rollback

Revert the PR's merge commit.

---

## Your steps

After the **P6a** PR is merged, from `D:\MyDevWork\clinic-MS`:

```powershell
.\.assessment\slice.ps1 -Branch feature/p4b-queue-arrivals `
  -Message 'P4b: queue lists today''s bookings not checked in yet; Move hidden once a patient is checked in'
```

There's no migration, so there's no database approval step.

### Manual check (while the app runs)

1. **Reception:** book a test patient for **today** with Dr X and confirm it.
2. **Doctor X → My Queue:** the queue is empty, and **Not checked in yet** lists the patient with the time. There's no Check In button for the doctor.
3. **Reception → Queue:** the same patient is under **Not checked in yet**, with **Check In**. Click it: you stay on the queue, and the patient is now in Dr X's queue as **Waiting**.
4. **Doctor's window:** within 30 seconds, without refreshing, the patient shows as **Waiting** with **Start Consultation**.
5. **Statuses:** open the reservation. **Status** still reads **Confirmed**, and the **Patient Queue** card reads **Waiting** with the check-in time.
6. **Move:** before check-in, Appointments and the reservation page show **Move**. After check-in (and during or after the consultation), neither shows **Move**.

When you've finished, press Enter in the script, then type `YES` to commit and push. Open the PR and send me its number and the CI result.
