# Slice M3a: the medical record forms

Group 4, item **M3** (clinical screens), slice a. Follows the approved plan (`M3-clinical-screens-plan.md`, approved 13 Nov 2026 with the recommended choices).

**Claude implemented this slice at the owner's request.** The owner runs one command, does the manual check, and pushes.

| | |
|---|---|
| Branch | `feature/m3a-record-forms` (from `main` after PR #67; your folder is on it) |
| Behaviour change | **1. New record and Edit record** use the shared form fields (label, field and message wired together for screen readers), the shared page heading, and are **in Arabic and English**. **2. Edit record now shows the patient header with allergies**, as New record already did. If a save is refused, the header stays when the form is shown again. **3. Two short hints:** *Chief complaint: in the patient's words*; *Treatment plan: medicines go on the record's Prescriptions tab*. **4. The category list** (Internal medicine, Nutritional) is translated. **5.** The inline width style is replaced by a shared class. **Unchanged:** the fields, their names, what's saved, who can create or edit, and locked records |
| Consultation workspace | Checked: no English text left in it (it was translated in P5b) |
| Database | None |
| Tests | No new unit tests (layout and text only). The browser test already creates a record as the doctor through this form, and checks it for accessibility |

## Files

| Action | Path |
|---|---|
| Edit | `Web/Views/MedicalRecords/Create.cshtml`, `Edit.cshtml` (rewritten), `Web/Controllers/MedicalRecordsController.cs` (patient on the Edit form, also when a save is refused), `wwwroot/css/site.css`, `Resources/SharedResource.ar.resx` (+13) |

## Rollback

Revert the PR's merge commit.

---

## Your steps

In PowerShell:

```powershell
.\.assessment\slice.ps1 -Branch feature/m3a-record-forms `
  -Message 'M3a: new and edit medical record forms on the shared fields, with the allergy header, in Arabic'
```

### Manual check (while the app runs)

1. **As a doctor**, start a consultation for a test patient with an allergy (or **New record** from the Records tab). The form shows the patient header with the allergy, the heading *New medical record*, and the sections Visit, Presenting complaint, Diagnosis and Plan, with the two hints.
2. Fill in a diagnosis and save. The record opens in the consultation workspace, as before.
3. **Edit** the record. The patient header with the allergy is shown. Change the treatment plan and save: the change is kept.
4. Clicking a field label puts the cursor in its field.
5. **Arabic:** both forms are in Arabic, laid out right to left. Typing Arabic or English into the fields works.
6. A **locked** record still can't be edited.

When you've finished, press Enter in the script, then type `YES` to commit and push. Open the PR, merge it, run `git switch main` and `git pull`, and send me its number and the CI result.

## Next: M3b, prescriptions

Labelled drug fields (compact rows, stacked on phones), the prescription page in the shared layout and Arabic, and the **bilingual printout** (A5 and A4).
