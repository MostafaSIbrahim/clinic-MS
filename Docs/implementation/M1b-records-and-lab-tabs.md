# Slice M1b: Records and Lab tabs

Group 4, item **M1** (patient workspace), slice b. Follows the approved plan (`M1-patient-workspace-plan.md`).

**Claude implemented this slice at the owner's request.** The owner deletes two old views, runs one command, does the manual check, and pushes.

| | |
|---|---|
| Branch | `feature/m1b-records-lab-tabs` (from an up-to-date `main`, after PR #58 is merged) |
| Behaviour change | **1. Records tab:** the patient's medical records, newest first: date, doctor, category, diagnosis, locked. **New record** for people who can write records. **2. Lab tab:** the patient's lab requests, with type (and **Urgent**), status, request and result dates, and the record they belong to. **Print request form** when something is requested, and **Request analysis** for people who can request. Lab rules are unchanged (D9). **3. The patient header with allergies** shows above both clinical tabs. **4. Doctors open a patient on Records.** The front desk still opens on Visits; others on Overview. **5. The old pages redirect:** `/MedicalRecords/PatientRecords?patientId=…` and `/Analysis/PatientAnalyses?patientId=…` open the matching tab. The **Medical Records** and **Analyses** buttons above the tabs are gone, and the links on the reservation page, an analysis page and the request slip go to the tabs. **6.** Lab statuses (*Requested*, *In progress*) get their own badges. Both tabs are in Arabic and stack on phones |
| CI | **The browser test no longer depends on the time of day.** CI runs the test app with a clinic time zone where it's about 10:00, so today always has free times. The live site is unaffected (it uses `Clinic:TimeZone`, default Africa/Cairo) |
| Database | None |
| Tests | +4 (doctors and admin see Records and Lab; doctors start on Records; a tab the person can't see falls back; tab names in any case). The browser test now opens the patient as the doctor: Records is the current tab, shows the diagnosis and the allergy header; Lab opens; the old records address lands on the tab. Both tabs are checked for accessibility at 1366, 1024 and 390 px |

## Files

| Action | Path |
|---|---|
| Add | `Web/Views/Patients/_PatientRecords.cshtml`, `Web/Views/Patients/_PatientLab.cshtml` |
| Edit | `Web/Models/PatientTabs.cs`, `Web/Controllers/PatientsController.cs` (loads the tab), `MedicalRecordsController.cs` and `AnalysisController.cs` (old pages redirect), `Web/Views/Patients/Details.cshtml` (header on clinical tabs, two buttons removed), `Web/Components/StatusStyles.cs` (lab statuses) |
| Edit | `Web/Views/Reservations/Details.cshtml`, `Web/Views/Analysis/Details.cshtml`, `Web/Views/Analysis/PrintRequestSlip.cshtml` (links to the tabs), `Resources/SharedResource.ar.resx` (+19) |
| Edit | tests: `Web/PatientTabsTests.cs`, `e2e/tests/pilot-flow.spec.js`; `.github/workflows/ci.yml` (clinic time zone in the browser test) |
| **Delete** (you, Step 1) | `Web/Views/MedicalRecords/PatientRecords.cshtml`, `Web/Views/Analysis/PatientAnalyses.cshtml` (no action uses them any more) |

## Rollback

Revert the PR's merge commit. This brings back the two views.

---

## Your steps

**Step 1.** In **PowerShell**, after merging PR #58:

```powershell
cd D:\MyDevWork\clinic-MS
git switch main
git pull
Remove-Item SafyaClinic\SafyaClinic.Web\Views\MedicalRecords\PatientRecords.cshtml
Remove-Item SafyaClinic\SafyaClinic.Web\Views\Analysis\PatientAnalyses.cshtml
```

If `git switch main` refuses because of local changes, use the same commands as before: `git stash push --include-untracked -m m1b`, `git reset --hard`, `git switch main`, `git pull`, `git stash pop`. Answer **n** to the folder questions. Then do the two `Remove-Item` lines.

**Step 2.**

```powershell
.\.assessment\slice.ps1 -Branch feature/m1b-records-lab-tabs `
  -Message 'M1b: Records and Lab tabs in the patient workspace; old pages redirect; browser test independent of the time of day'
```

### Manual check (while the app runs)

Use a test patient with an allergy, a record and a lab request.

1. **As a doctor**, open the patient. It opens on **Records**, with the allergy header above it. The records are listed newest first; **Open** opens the consultation workspace.
2. **New record** opens the new-record form for this patient.
3. **Lab** shows the lab requests with their status badges. **Request analysis** opens the request form. With a request still open, **Print request form** opens the slip in a new tab, and the slip's back link returns to the Lab tab.
4. Open an analysis. Its back button returns to the patient's Lab tab.
5. Type `/MedicalRecords/PatientRecords?patientId=<id>` in the address bar: the Records tab opens. The same with `/Analysis/PatientAnalyses?patientId=<id>` opens Lab.
6. **As Reception**, the patient opens on Visits, and there's no Records or Lab tab. Typing `?tab=records` opens Visits.
7. On a reservation page, **Patient medical records** opens the Records tab.
8. **Arabic:** both tabs are in Arabic. At phone width the tables become cards.

When you've finished, press Enter in the script, then type `YES` to commit and push. Open the PR and send me its number and the CI result. The **e2e** job should now pass at any hour.

## Next: M1c

**Payments** and **Nutrition** become tabs. Collect and write off return to the Payments tab, and the old payment summary and enrollments pages redirect.
