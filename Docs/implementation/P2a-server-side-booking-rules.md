# Slice P2a: bookings follow the calendars (server-side booking rules)

Pilot item **P2** (slot booking), slice a. This slice implements **D7**'s server rules: no double booking, no past times, and only inside the doctor's hours.

**Claude implemented this slice at the owner's request.** The owner runs one command, does the manual check, and pushes.

| | |
|---|---|
| Branch | `feature/p2a-booking-rules` (from the working copy, after PR #40 is merged) |
| Behaviour change | **Every new reservation, and every edit that changes the doctor, clinic, date, time or length,** is checked by the server: **1.** Not in the past (clinic time). **2.** Wholly inside the doctor's bookable hours in that clinic on that date: their calendar and date changes, or the clinic's hours for a doctor without a calendar. If not, the message lists the next free times that day (e.g. "Free times that day: 10:00, 10:15, …"). **3. No double booking:** it may not overlap any of the doctor's other bookings **in any clinic**. Cancelled, no-show and completed reservations don't count, and moving a reservation never clashes with itself. The message names the clashing booking by number and time. These checks run **inside a per-doctor lock**, so two people saving the same time at once can't both succeed. This applies to the Reservations form, editing, and booking a follow-up. **Edits that don't change the time** (notes, reason, treatment, price) are never blocked, even for reservations booked before calendars existed |
| Transition rule (new) | **A clinic that hasn't set any opening hours yet isn't restricted:** its doctors without a calendar can be booked at any time of day; only "not in the past" and "no double booking" apply. Your clinics have no hours yet, so booking keeps working until you set them. Once a clinic sets its hours, they take effect, and the P1c guard already refuses hours that would strand existing bookings |
| Database | None. No migration |
| Tests | +10 (about **358**). They cover the rules (a free time is accepted; past dates and times are refused; outside the hours is refused with free times offered; no double booking, but a reservation may keep its own time, and touching times are fine; visit length; a clinic without hours isn't restricted but a visit must end the same day) and the reservation service (book once, the same time again is refused, another doctor at the same time is fine, outside clinic hours is refused) |

## Design

| Decision | Choice |
|---|---|
| Where | `BookingRules.Problem` (pure, in `Application/Scheduling`) uses P1c's `Availability`. `ReservationService` calls it in `CreateReservationAsync` and, when the time changes, in `UpdateReservationAsync` |
| Lock | A new `IUnitOfWork.ExecuteDoctorBookingAsync(doctorId, …)` uses a SQL Server application lock per doctor, the same mechanism as the existing record, status and follow-up locks. **The lock helper now also works nested:** booking a follow-up (already under the follow-up lock) takes the doctor lock in the same transaction instead of failing. With the in-memory test database it simply runs the operation |
| Visit length | Still the form's value, which the form pre-fills from the treatment type. It must be 5–480 minutes and end the same day |
| Step alignment | Not required here. Any time inside the hours is accepted (e.g. 10:05), so the existing forms keep working. The appointments screen (P2b) offers step-aligned times |

## Files

| Action | Path |
|---|---|
| Add | `Application/Scheduling/BookingRules.cs` |
| Edit | `Application/Scheduling/Availability.cs` (clinic without hours = whole day) |
| Edit | `Application/Services/ReservationService.cs` (checks under the doctor lock on create and on time changes) |
| Edit | `Domain/Interfaces/Repositories/IUnitOfWork.cs`, `Infrastructure/Repositories/UnitOfWork.cs` (`ExecuteDoctorBookingAsync`; nested locks; in-memory bypass) |
| Add | `tests/.../Application/BookingRulesTests.cs` |

## Rollback

Revert the PR's merge commit.

---

## Your steps

After merging **PR #40**, from `D:\MyDevWork\clinic-MS`:

```powershell
.\.assessment\slice.ps1 -Branch feature/p2a-booking-rules `
  -Message 'P2a: bookings follow the calendars: no past times, inside the doctor''s hours, no double booking (per-doctor lock)'
```

There's no migration, so there's no database approval step. The script builds, tests, starts the app and waits for your check.

### Manual check (while the app runs)

1. **Without clinic hours** (any clinic you haven't set): book a reservation as usual. It works.
2. **Past time:** try today at a time that has already passed. Refused.
3. **Double booking:** book Dr X at 11:00 next Saturday, then book **another patient** with Dr X at 11:15 (30 minutes). Refused, naming "#… 11:00 to 11:30". **Dr Y at 11:15** works.
4. **Across clinics:** book Dr X in a **second clinic** at 11:15 the same day. Also refused.
5. **With clinic hours:** set a clinic's Saturday to 10:00–14:00, then book a doctor without a calendar there at 16:00. Refused, with "Free times that day: 10:00, …". **10:00 works.**
6. **With a calendar:** give the doctor a calendar (e.g. 10:00–12:00), then book 12:30. Refused.
7. **Editing:** open an existing reservation and change only the **notes**. It saves, even if its time is outside today's rules. Change its **time** to a clashing one. Refused.
8. **Follow-ups:** book a follow-up from the follow-up list at a free time. It works, so the nested lock is fine.
9. **Two at once (optional):** in two browser windows, prepare the same doctor and time, then save both quickly. Only one is saved; the other gets the double-booking message.

When you've finished, press Enter in the script, then type `YES` to commit and push. Open the PR and send me its number and the CI result.

## Next: P2b, the appointments screen

- One screen for a day: per clinic, the doctors in columns.
- Their bookings, and their **free times as buttons**.
- Clicking a free time opens a short booking form with the patient picker. The time, doctor and clinic are already filled in, and the price comes from the treatment.
