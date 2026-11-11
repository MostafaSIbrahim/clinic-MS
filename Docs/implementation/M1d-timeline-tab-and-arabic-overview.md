# Slice M1d: Timeline tab, and the Overview in Arabic (closes M1)

Group 4, item **M1** (patient workspace), slice d. This slice **closes M1**. Follows the approved plan (`M1-patient-workspace-plan.md`).

**Claude implemented this slice at the owner's request.** The owner deletes one old view, runs one command, does the manual check, and pushes.

| | |
|---|---|
| Branch | `feature/m1d-timeline-tab` (from `main` after PR #60; your folder is already on it) |
| Behaviour change | **1. Timeline tab:** the patient's reservations and medical records, newest first, 20 per page. Each entry has its date and time, kind, title, status and **Open**. It's a tab now, not a page that opened in a new browser tab, so the Timeline button in the header is gone. Shown to people who can view the timeline (doctors and admin). **2. The old address** `/Patients/Timeline/{id}` opens the tab. **3. The Overview tab and the header are in Arabic and English:** the last and next visit, general and medical information, contacts, the Add phone and Add address forms, the remove confirmations, and the header (back link, patient number, national ID, Book appointment, More actions menu). Phone numbers stay left-to-right in Arabic. **4.** The next appointment's status uses the shared status badge |
| M1 is complete | One patient page with seven tabs (Overview, Visits, Records, Lab, Nutrition, Payments, Timeline). Each person sees the tabs their permissions allow, and the first tab fits their work. All the old patient pages redirect to their tab |
| Still English | The Payments tab's text (with the billing screens, M2) |
| Database | None |
| Tests | +2 (doctors and admin see Timeline; a doctor can open it; Reception asking for it gets Visits). The browser test opens the Timeline tab as the doctor, checks the visit's record is listed, checks it for accessibility at the three widths, and checks that the old address lands on the tab |

## Files

| Action | Path |
|---|---|
| Add | `Web/Views/Patients/_PatientTimeline.cshtml` |
| Edit | `Web/Views/Patients/_PatientOverview.cshtml` (Arabic), `Web/Views/Patients/Details.cshtml` (header in Arabic, Timeline button removed, Timeline tab), `Web/Models/PatientTabs.cs`, `Web/Controllers/PatientsController.cs` (loads the timeline; the old page redirects), `wwwroot/css/site.css` (timeline list), `Resources/SharedResource.ar.resx` (+50) |
| Edit | tests: `Web/PatientTabsTests.cs`, `e2e/tests/pilot-flow.spec.js` |
| **Delete** (you, Step 1) | `Web/Views/Patients/Timeline.cshtml` |

## Rollback

Revert the PR's merge commit. This brings back the timeline page.

---

## Your steps

Your folder is on an up-to-date `main` with the M1d changes. In **PowerShell**:

```powershell
cd D:\MyDevWork\clinic-MS
Remove-Item SafyaClinic\SafyaClinic.Web\Views\Patients\Timeline.cshtml
.\.assessment\slice.ps1 -Branch feature/m1d-timeline-tab `
  -Message 'M1d: Timeline tab in the patient workspace; Overview and header in Arabic; closes M1'
```

### Manual check (while the app runs)

1. **As a doctor**, open a patient with a few visits and records → **Timeline**. The entries are newest first, each with its date, kind (Reservation or Medical record), title and status. **Open** goes to the reservation or record.
2. With more than 20 entries, page links appear under the list and keep you on the Timeline tab.
3. `/Patients/Timeline/<id>` opens the Timeline tab. The header has no separate Timeline button.
4. **As Reception**, there's no Timeline tab, and `?tab=timeline` opens Visits.
5. **Overview in English:** everything as before. Add a phone and an address, then remove them; the confirmation asks first.
6. **Switch to العربية**, Overview: the headings, labels, buttons and both forms are in Arabic, laid out right to left. Phone numbers read left to right. The More actions menu is in Arabic.
7. **Arabic, Timeline:** in Arabic, and at phone width the entries fit without sideways scrolling.

When you've finished, press Enter in the script, then type `YES` to commit and push. Open the PR, run `git switch main` and `git pull` after merging, and send me its number and the CI result.

## Next: M2, billing

The payment screens: collections, the payments report, and the financial dashboard with database totals and date-bounded queries. Their figures must not change, so I'll send the plan first, as for M1.
