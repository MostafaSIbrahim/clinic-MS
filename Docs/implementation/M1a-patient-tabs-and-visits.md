# Slice M1a: patient workspace tabs, and the Visits tab

Group 4, item **M1** (patient workspace), slice a. Follows the approved plan (`M1-patient-workspace-plan.md`, approved 10 Nov 2026 with the recommended choices: each tab loads on its own; the first tab fits the person's work; the patient list opens this page).

**Claude implemented this slice at the owner's request.** The owner runs one command, does the manual check, and pushes.

| | |
|---|---|
| Branch | `feature/m1a-patient-tabs` (from an up-to-date `main`) |
| Behaviour change | **1. The patient page has tabs**, below the header (name, Book appointment, More actions): **Overview** and **Visits**. Each tab is its own address (`/Patients/Details/5?tab=visits`), so it can be bookmarked or sent. **2. Overview** is today's patient page content, unchanged: last and next visit, details, medical information and contacts. **3. Visits is new:** every reservation of the patient, newest first (upcoming ones at the top), 20 per page. Each row shows the date and time, doctor, visit type, status (and arrival status once checked in) and coverage. The row actions follow the same rules as Appointments and the dashboard: **Check in** (today, not yet arrived), **Collect** (balance due), **Move** (not yet arrived, today or later), **Open**. **4. The first tab fits the work:** people who check patients in (front desk, supervisors, admin) open a patient on **Visits**; others on **Overview**. A tab someone can't use isn't shown, and asking for it by address opens the default tab instead. **5.** The buttons for Medical Records, Analyses, Nutrition and Payments stay above the tabs for now; they become tabs in M1b and M1c |
| Not yet | The Overview text is still English only, as before; its Arabic comes with the remaining tabs (M1d). Records, Lab, Nutrition, Payments and Timeline are separate pages until M1b–M1d |
| Database | None |
| Tests | +10 (which tabs each group sees; the first tab for the front desk, doctors and others; a chosen tab wins; an unknown or forbidden tab falls back). The browser test now opens the patient on **Visits**, checks the booking is listed with the doctor, switches to **Overview**, and checks both tabs for accessibility at 1366, 1024 and 390 px |

## Files

| Action | Path |
|---|---|
| Add | `Web/Models/PatientTabs.cs` (tabs, default tab, page model), `Web/Views/Patients/_PatientOverview.cshtml` (moved from the patient page), `Web/Views/Patients/_PatientVisits.cshtml`, `tests/.../Web/PatientTabsTests.cs` |
| Edit | `Web/Views/Patients/Details.cshtml` (header + tabs), `Web/Controllers/PatientsController.cs` (`Details` loads the chosen tab), `Application/Services/ReservationService.cs` (the reservation list also carries arrival status and balance due), `wwwroot/css/site.css` (link tabs), `Resources/SharedResource.ar.resx` (+4) |
| Edit | `e2e/tests/pilot-flow.spec.js` |

## Rollback

Revert the PR's merge commit.

---

## Your steps

```powershell
git switch main
git pull
.\.assessment\slice.ps1 -Branch feature/m1a-patient-tabs `
  -Message 'M1a: patient workspace tabs (Overview, Visits); Visits lists every reservation with check-in, collect, move and open'
```

### Manual check (while the app runs)

Use a test patient with a few reservations: one today (not yet arrived), one finished with a balance, one in the future, and one cancelled.

1. **As Reception**, open the patient (from Patients or the top-bar search). It opens on **Visits**, and the Visits tab is underlined.
2. The list shows every reservation, newest first. The cancelled one shows **Cancelled** with no coverage badge and no actions except Open.
3. Today's not-yet-arrived row has **Check in**. Click it. You come back to the same tab and page, and the row shows **Waiting**.
4. The finished row with a balance has **Collect**. It opens the Collect form for that visit.
5. The future row has **Move**, and it opens the Move screen.
6. Click **Overview**: the page you know (last and next visit, details, contacts). Adding or removing a phone still works.
7. **As a doctor**, open the same patient. It opens on **Overview**. **Visits** is there, without Check in or Collect.
8. Copy the address of the Visits tab, open it in a new browser tab: the Visits tab opens.
9. **Arabic:** the tab names and the Visits table are in Arabic. At phone width the Visits table becomes cards.
10. A patient with more than 20 reservations shows page links under the list.

When you've finished, press Enter in the script, then type `YES` to commit and push. Open the PR and send me its number and the CI result.

## Next: M1b

**Records** and **Lab** become tabs: the patient header with allergies on clinical tabs, the old records and analyses pages redirect to the tabs, and their buttons above the tabs go.
