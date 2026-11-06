# Slice P2b: the appointments screen (book by clicking a free time)

Pilot item **P2** (slot booking), slice b. This slice puts D7 on screen: the front desk sees free times and books with one click.

**Claude implemented this slice at the owner's request.** The owner runs one command, does the manual check, and pushes.

| | |
|---|---|
| Branch | `feature/p2b-appointments` (from the working copy, after PR #41 is merged) |
| Behaviour change | **1. A new menu item, Front desk → Appointments** (for everyone who can see reservations), showing **one clinic's day**. A filter bar picks the date, clinic and visit; the visit's treatment sets its length. There are **previous day / today / next day** links. **2. One card per doctor** who can be booked in that clinic that day (by calendar, or by clinic hours without one), or who already has bookings there. Each card shows that day's hours, **the bookings** (time, patient linked to the reservation, status badge), and **the free times as buttons**, using exactly the calculation the server checks with. A booking in another clinic takes the doctor's time here too, and past times aren't offered. Doctors who aren't bookable there that day are listed by name underneath. **3. Clicking a free time** (for people who can book) opens a short form: doctor, clinic, date and time are fixed, so you only pick the **patient** (the same search as the reservation form, plus Register new patient), the **treatment** (it sets the price and the visit length), and an optional reason and notes. **Book** returns to the day with a toast. If someone took the time meanwhile, the P2a check refuses it and says why. **4.** A clinic without opening hours shows a note that its doctors can be booked at any time. **5.** English and Arabic. The existing **Reservations / Appointment Board / Today** screens stay unchanged until P2d replaces them |
| Price | Always the treatment's price (D4). An administrator can still override it on the full reservation form |
| Database | None |
| Tests | +6 (about **364**). They cover: doctors without a calendar are offered in clinic hours, with their bookings shown and booked times removed from the free times; a booking in another clinic blocks the time here; a calendar not in use means "not here"; cancelled bookings neither show nor hold a time; a closed day offers nobody; a past day offers no free times; the menu marks Appointments as current while booking |

## Files

| Action | Path |
|---|---|
| Add | `Application/Scheduling/AppointmentDay.cs`, `Interfaces/Services/IAppointmentsService.cs`, `Services/AppointmentsService.cs`; edit `ApplicationServiceExtensions.cs` |
| Add | `Web/Controllers/AppointmentsController.cs`, `Web/Models/AppointmentModels.cs`, `Views/Appointments/Index.cshtml`, `Views/Appointments/Book.cshtml` |
| Edit | `Web/Navigation/NavigationCatalogue.cs` (Appointments), `wwwroot/css/site.css`, `Resources/SharedResource.ar.resx` |
| Edit/Add | tests: `access-matrix.txt` (3 lines), `NavigationCatalogueTests.cs`; add `Application/AppointmentsServiceTests.cs` |

## Rollback

Revert the PR's merge commit.

---

## Your steps

After merging **PR #41**, from `D:\MyDevWork\clinic-MS`:

```powershell
.\.assessment\slice.ps1 -Branch feature/p2b-appointments `
  -Message 'P2b: appointments screen: a clinic''s day per doctor with bookings and free times; book by clicking a free time'
```

There's no migration, so there's no database approval step.

### Manual check (while the app runs)

1. **As Reception: Front desk → Appointments.** Today and the first clinic are shown. Each doctor who can be booked has a card with their hours, bookings and free-time buttons.
2. **Pick next Saturday** and a clinic with hours (e.g. 10:00–14:00). With a 30-minute visit the buttons run 10:00, 10:15 … 13:30.
3. **Click 10:30** for a doctor. The form shows doctor, clinic, date and 10:30. Search a patient, choose a treatment, and click **Book**. You're back on the day with a green toast. **10:30 now appears under Booked**, and 10:15–10:45 are gone from the free times.
4. **Same time twice:** open a second window on the same day and click the doctor's 11:00. In the first window, book 11:00 too. Then press **Book** in the second window. It's **refused** ("already has a booking …").
5. **Other clinic:** switch to a second clinic on that Saturday. The doctor's 10:30 is not free there either.
6. **Calendar:** give a doctor a calendar that isn't in use in this clinic. They move to "Not in this clinic on this day".
7. **Visit length:** choose a 60-minute treatment in the filter bar. The last free time becomes 13:00.
8. **As a Doctor:** you see the screen and can book (Doctors may manage reservations today). **As mm (Reception supervisor):** there's no Appointments item.
9. **Arabic:** المواعيد, with buttons and cards right to left. Times stay Western digits.
10. **Phone width:** doctor cards stack, and the buttons wrap.

When you've finished, press Enter in the script, then type `YES` to commit and push. Open the PR and send me its number and the CI result.

## Next

- **P2c, move a booking:**
  - a **Move** action on a booking (pick a new free time; history kept);
  - the P1c guard's "move or cancel first" links straight to it;
  - a week view.
- **P2d:** replace Reservations List / Appointment Board / Today with this screen (old links redirect).
