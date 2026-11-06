# Slice P3: find and register patients

Pilot item **P3**. This slice implements D8 at the front desk: one patient per primary phone, found in one step.

**Claude implemented this slice at the owner's request.** The owner runs one command, does the manual check, and pushes.

| | |
|---|---|
| Branch | `feature/p3-find-patients` (from the working copy, after PR #44 is merged) |
| Behaviour change | **1. Search finds people by any words:** "mona adel", "adel 0100", or a phone number with spaces, dashes or +20 ("0100 123 4567", "+20 100 123 4567"). **Every word must match** the first name, last name, national ID or a phone. Name matching ignores upper/lower case. Before, "Mona Adel" found nothing because the whole text had to match one field. **2. One match opens the patient straight away.** This applies from the top-bar search and the Patients page: typing a phone number goes directly to that patient. Two or more matches still show the list. **3. Registering an existing primary phone** is still refused (F5b), and now shows **"This phone belongs to an existing patient"** with **Open patient #…**. In the booking pop-up the button is **Use this patient**, which fills that patient into the booking form and closes the pop-up, as if they had just been registered. **4. Book from the patient's page:** the patient page's main button is now **Book appointment**. It opens Appointments with "Booking for {patient}: choose a free time"; clicking a time opens the booking form with the patient already filled in. That makes a known patient bookable in three interactions after search: **open patient → Book appointment → click a time → Book**. "Book for someone else" clears it. The full reservation form stays available from the Reservations page |
| Database | None |
| Tests | +9 (about **377**). They cover: every word must match, and phones match in any format (five cases); one word finds everyone it matches and unknown words find nobody; the existing patient is read from the duplicate-phone message; one match opens the patient; several matches show the list |

## Files

| Action | Path |
|---|---|
| Edit | `Application/Services/PatientService.cs` (multi-word, case-insensitive search; a spaced phone is one number) |
| Add | `Web/Models/DuplicatePatient.cs` |
| Edit | `Web/Controllers/PatientsController.cs` (one match → patient; existing patient on a duplicate phone; `UseExisting` for the booking pop-up), `Views/Patients/Create.cshtml`, `Views/Patients/Details.cshtml` (Book appointment) |
| Edit | `Web/Controllers/AppointmentsController.cs`, `Web/Models/AppointmentModels.cs`, `Views/Appointments/Index.cshtml` (booking for a patient), `Resources/SharedResource.ar.resx` |
| Edit/Add | tests: `access-matrix.txt` (+ `Patients.UseExisting GET`); add `Application/PatientSearchTests.cs` |

## Rollback

Revert the PR's merge commit.

---

## Your steps

After merging **PR #44**, from `D:\MyDevWork\clinic-MS`:

```powershell
.\.assessment\slice.ps1 -Branch feature/p3-find-patients `
  -Message 'P3: find patients by any words or phone format, one match opens the patient, existing-phone link, book from the patient page'
```

There's no migration, so there's no database approval step.

### Manual check (while the app runs)

1. **Top bar:** type a patient's full phone with spaces (e.g. `0100 123 4567`) and press Enter. **That patient's page opens directly.**
2. **Two words:** search "first-name last-name" (any case). The patient is found. A common first name alone shows the list.
3. **Duplicate phone:** Patients → Register, using an existing patient's primary phone. You see the refusal, "This phone belongs to an existing patient" and **Open patient #…**, which opens them.
4. **In the booking pop-up:** Appointments → click a free time → **Register a new patient** → enter an existing phone → **Use this patient**. The pop-up closes and the booking form now shows that patient.
5. **Book from the patient:** open a patient → **Book appointment**. Appointments says "Booking for …"; click a free time and the patient is already filled in. Choose a treatment and Book.
6. **Book for someone else** removes the preselected patient.
7. **Arabic:** the "Booking for …" line reads الحجز لـ …

When you've finished, press Enter in the script, then type `YES` to commit and push. Open the PR and send me its number and the CI result.

## Next: P4, check-in desk and doctor queue

- Check patients in from **Appointments**.
- The queue shows doctors their own patients and the front desk everyone's.
- Status changes follow D6 (a doctor never changes another doctor's reservation status).
- Optional automatic refresh.
