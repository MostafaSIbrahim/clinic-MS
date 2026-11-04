# Slice F8a — Patient header with allergies, and the status badge

Roadmap item **F8** (component library), slice a of 3 (Group 2). Starts **F09** (allergies visible where clinical decisions are made).

**Implemented by Claude at the owner's request.** The owner builds, tests, checks and opens the PR.

| | |
|---|---|
| Branch | `chore/f8a-patient-header` (from the working copy, **after F7c is merged**; see Step 1) |
| Behaviour change | 1. **Patient header** at the top of **New/consultation record**, **Medical record details** and **a patient's records list**. It shows the name (linking to the patient), age and sex, primary phone and blood type, and, most importantly, **allergies in full**, in a red-tinted box with a warning icon and the word "Allergies". If none are recorded, it says "Allergies: none recorded", which is neutral and doesn't suggest the patient has none. Chronic diseases are shown below. 2. **Status badge component:** one style per status (booked, waiting, in consultation, completed, cancelled, no-show, paid, due, write-off), from the status tokens. Status is always written in words. It's used first for "Covered" / "Not fully covered" on reservations and the dashboard. The other screens adopt it as they're migrated |
| Database | None |
| Tests | +15 (about 241): header content (allergies in full, blank means none recorded, age, sex, primary phone) and the status-to-style mapping |

## Design

| Decision | Choice |
|---|---|
| Patient header | View component `PatientHeader` (`Web/Components/PatientHeader.cs`). Use it with `@await Component.InvokeAsync("PatientHeader", new { patientId = … })`. It loads the patient through the existing patient service and renders nothing if the patient isn't found |
| Allergy safety | The text is shown verbatim (trimmed), never shortened. Colour plus icon plus the word "Allergies" means colour is never the only signal. `dir="auto"` handles Arabic notes |
| Status badge | `<partial name="_StatusBadge" model="@status" />`, with the mapping in `StatusStyles`. Unknown words get a neutral style |
| Next screens | Prescriptions and the consultation workspace get the header in the pilot (P5), which reworks them anyway |

## Files

| Action | Path |
|---|---|
| Add | `SafyaClinic.Web/Components/PatientHeader.cs` (model + view component), `Components/StatusStyles.cs` |
| Add | `SafyaClinic.Web/Views/Shared/Components/PatientHeader/Default.cshtml` |
| Edit | `Views/Shared/_StatusBadge.cshtml` (rewritten; it wasn't used before), `Views/Shared/_PaymentStatusToggle.cshtml` |
| Edit | `Views/MedicalRecords/Create.cshtml`, `Details.cshtml`, `PatientRecords.cshtml` (header added) |
| Edit | `wwwroot/css/site.css`: `.app-status` and `.app-patient-header` (tokens only) |
| Add | `tests/SafyaClinic.Tests/Web/ComponentTests.cs` |

## Rollback

Revert the PR's merge commit.

---

## Your steps

Run these in PowerShell from `D:\MyDevWork\clinic-MS`.

### Step 1 — Finish F7c first, then branch

**F7c isn't in `main` yet.** PR #29 was F7b. Your F7c commit is pushed on `chore/f7c-shell-feedback`:
1. Open its PR on GitHub, wait for CI, and merge it.
2. Then, with the F8a changes still uncommitted in your working copy:

```powershell
git switch -c chore/f8a-patient-header
git status
```

`git status` should show 6 modified files and 3 new ones (the `Components` folder, the `PatientHeader` view and `ComponentTests.cs`).

### Step 2 — Build and test

```powershell
dotnet build SafyaClinic\SafyaClinic.sln
dotnet test SafyaClinic\SafyaClinic.sln
```

Expected: **226 + 15 = 241 passed** (tell me the number if it differs but nothing fails).

### Step 3 — Check

```powershell
dotnet run --project SafyaClinic\SafyaClinic.Web --launch-profile http
```

1. **As a Doctor or Admin**, open a patient with allergies recorded. If none has them, add some via **Edit medical** (e.g. "Penicillin"). Then open **Medical Records**: the header at the top shows the name, age and sex, phone and blood type, and **Allergies: Penicillin** in a red-tinted box.
2. **Open a medical record** and **start a new record** for the same patient. The same header is there.
3. **A patient without allergies** shows "Allergies: none recorded" in grey.
4. **Click the patient's name** in the header. It opens the patient page.
5. **Dashboard / Reservations → Today:** "Covered" is a green pill and "Not fully covered" a grey pill. Both are readable.

### Step 4 — Commit, push, PR

```powershell
git add -A SafyaClinic Docs
git status
git commit -m "F8a: patient header with allergies on clinical screens; status badge component"
git push -u origin chore/f8a-patient-header
```

Send me the PR number and the CI result.

## Next: F8b and F8c

- **F8b:** form field, select and date/time helpers with label, hint and error, so they can't be rendered without a label; money and date-time display; an icon button that requires a label.
- **F8c:**
  - a data table (Direction C style, with an empty state, pagination and stacked mode on phones);
  - a filter bar, card, KPI tile, tabs and modal;
  - a sample page that shows every component.
