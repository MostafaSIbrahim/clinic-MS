# Slice P2e: booking forms refuse past dates and times

A follow-up to **P2**, from your note of 9 Nov: a new reservation can't be for a date before today, or for a time earlier than now.

**Claude implemented this slice at the owner's request.** The owner runs one command, does the manual check, and pushes.

## Where the rule already applied

The **server** has refused past bookings since P2a (`BookingRules`). This applies on every path that creates or moves a reservation: Appointments, the full reservation form, Edit when the date or time changes, Move, and follow-up booking. It also applies to open schedules (a doctor without a calendar, or a clinic without hours). This slice adds a test for open schedules. Appointments never offers past free times.

The gap was on the **forms that take a typed date and time**: **New Reservation (full form)**, **Edit reservation** and **Book follow-up**. They let you pick yesterday, or 09:00 when it's already 12:50, and only refused after you submitted.

**If you did manage to save a past reservation**, tell me the screen and the reservation number. That would be a separate fault, and I'll trace it.

| | |
|---|---|
| Branch | `feature/p2e-no-past-bookings` (from the working copy, after PR #51 is merged) |
| Behaviour change | On those three forms, the **date picker starts at today** (clinic time), so earlier days can't be chosen. For **today**, a time earlier than now is refused at once with "Choose a current or future time; past times can't be booked.", and the form won't submit. "Now" keeps moving while the page is open. **Edit:** an existing booking's unchanged date and time stay allowed, so you can still correct the notes of a past visit. A new date or time must be current or future. The server check is unchanged and still decides |
| Database | None |
| Tests | +1 .NET (open schedules still refuse the past) and +4 JavaScript (past date, earlier time today, future dates, "now" moving on) |

## Files

| Action | Path |
|---|---|
| Add | `wwwroot/js/booking-time-guard.js`, `tests/booking-time-guard.test.cjs` |
| Edit | `Views/Reservations/Create.cshtml`, `Views/Reservations/Edit.cshtml`, `Views/FollowUps/Book.cshtml` |
| Edit | `tests/.../Application/BookingRulesTests.cs` |

## Rollback

Revert the PR's merge commit.

---

## Your steps

After merging **PR #51**, from `D:\MyDevWork\clinic-MS`:

```powershell
.\.assessment\slice.ps1 -Branch feature/p2e-no-past-bookings `
  -Message 'P2e: booking forms refuse past dates and earlier-than-now times (server rule unchanged)'
```

There's no migration, so there's no database approval step.

### Manual check (while the app runs)

1. **Reservations → New Reservation (full form):** the date picker greys out days before today. Type yesterday's date anyway and click Save: the date field shows the message, and nothing is sent.
2. Choose **today** and a time an hour ago. The time field shows the message. Change it to a later time and save: it's booked (provided it's inside the doctor's hours and free).
3. **Edit** a past visit (e.g. last week's) and change only the notes. It saves. Change its date to yesterday: refused before submitting.
4. **Follow-ups → Book:** same as step 1.
5. **Server (optional):** with the browser's JavaScript turned off, repeat step 2. The server refuses with the same message, as before.

When you've finished, press Enter in the script, then type `YES` to commit and push. Open the PR and send me its number and the CI result.
