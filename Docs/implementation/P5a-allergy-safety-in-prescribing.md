# Slice P5a: allergy safety when prescribing

Pilot item **P5** (consultation workspace), slice a. This slice implements the decision you made: when a medicine matches a recorded allergy, **warn and require confirmation**.

**Claude implemented this slice at the owner's request.** The owner runs one command, does the manual check, and pushes.

| | |
|---|---|
| Branch | `feature/p5a-allergy-check` (from the working copy, after PR #46 is merged) |
| Behaviour change | **1. Patient header on prescriptions.** **New Prescription** and **Prescription #…** now show the patient header (allergies, chronic diseases), as the record pages already do. **2. Allergy warning while typing.** When a medicine name matches the patient's recorded allergies, a red **Allergy warning** appears in the form and lists each match ("Penicillin V 500mg matches the recorded allergy: penicillin"). The form can't be saved until **Prescribe anyway** is ticked. **3. The server enforces it.** Without the tick, the server refuses the prescription or the added drug and shows the form again with everything still filled in. Before, the New Prescription form came back empty after any refusal; it now keeps the drugs entered. **4. Overrides are recorded** in the audit log as `Prescription.AllergyOverride`, with each medicine and the allergy it matched. **5.** The **Add Another Drug** form on a prescription now offers the medicine list while typing, as New Prescription does. That script was already on the page but had nothing to attach to. **6.** English and Arabic |
| How matching works | The recorded allergies are free text. They're split on commas, semicolons, the Arabic comma, slashes, new lines and "and" / "و". Entries such as "none", "NKDA" and "لا يوجد" are ignored, as are words shorter than 3 letters. A medicine matches when its name contains an allergy ("Penicillin V" ↔ "penicillin"), or an allergy contains the medicine's first word, if that word is 4 letters or more ("Augmentin 1g" ↔ "Augmentin tablets"). Upper and lower case don't matter |
| Limitation (say this to the doctors) | **This compares names only. It doesn't know drug classes:** an allergy to *penicillin* won't flag *amoxicillin*. It supports the doctor's judgement; it doesn't replace it. A class-aware check needs a drug database and is out of scope for the pilot |
| Database | None |
| Tests | +15 .NET (about **396**): how allergy text is split; "none" entries and short words ignored; matching ignores case and works both ways; refusal without confirmation (nothing saved, nothing audited); **Prescribe anyway** saves and audits; unrelated medicines aren't audited; a new prescription is refused, then saved when confirmed. +5 JavaScript: the browser follows the same rules |

## Files

| Action | Path |
|---|---|
| Add | `Application/Clinical/AllergyCheck.cs` (the rule) |
| Edit | `Application/Services/PatientRecordService.cs` (enforced on New Prescription and Add drug; patient allergies on record and prescription details), `DTOs/MedicalRecord/MedicalRecordDtos.cs` (`ConfirmAllergyOverride`, `PatientId`, `PatientAllergies`), `Common/Auditing/Audit.cs` (`Prescription.AllergyOverride`) |
| Edit | `Web/Controllers/MedicalRecordsController.cs` (form shown again with the matches; header patient) |
| Add | `Views/Shared/_AllergyConfirm.cshtml`, `wwwroot/js/allergy-check.js`, `tests/allergy-check.test.cjs`, `tests/.../Application/AllergyCheckTests.cs` |
| Edit | `Views/MedicalRecords/CreatePrescription.cshtml`, `Views/MedicalRecords/PrescriptionDetails.cshtml`, `Views/Shared/Components/PatientHeader/Default.cshtml` (`data-patient-allergies`), `Resources/SharedResource.ar.resx` |

No pages or permissions were added, so the access matrix is unchanged.

## Rollback

Revert the PR's merge commit. Overrides already recorded stay in the audit log.

---

## Your steps

After merging **PR #46**, from `D:\MyDevWork\clinic-MS`:

```powershell
.\.assessment\slice.ps1 -Branch feature/p5a-allergy-check `
  -Message 'P5a: allergy warning with Prescribe anyway (audited), patient header on prescriptions'
```

There's no migration, so there's no database approval step.

### Manual check (while the app runs)

Use a **test patient**. Set their allergies to `Penicillin, sulfa` on the patient's Edit page.

1. **As the Doctor, open a record of that patient → New Prescription.** The patient header shows the allergies.
2. Type `Penicillin V 500mg` as the first drug. The red **Allergy warning** appears and names it. Click **Save Prescription** without ticking: the page doesn't submit and the **Prescribe anyway** box is focused.
3. **Add Drug** → `Paracetamol`, then change the first drug to `Amoxicillin`. The warning disappears. Change it back to `Penicillin V`, tick **Prescribe anyway** and save. The prescription is created.
4. **Prescription #… → Add Another Drug:** type `Sulfamethoxazole`. The warning appears. Tick the box and add it. It's added.
5. **No JavaScript path (optional):** in the browser's developer tools, disable JavaScript and repeat step 2. The server refuses: the page comes back with **both drugs still filled in**, the warning and the box. Tick it and save. It works.
6. **Patient without allergies** (or "none"): no warning appears and prescribing works as before.
7. **Arabic:** the warning reads تحذير حساسية, and the box reads وصف الدواء رغم ذلك.
8. **Audit (optional, read-only):** `SELECT TOP 5 Action, EntityType, EntityId, Details FROM AuditLogs WHERE Action = 'Prescription.AllergyOverride' ORDER BY Id DESC;`

When you've finished, press Enter in the script, then type `YES` to commit and push. Open the PR and send me its number and the CI result.

## Next: P5b, the consultation workspace

- One page with tabs: **Notes · Treatments · Prescription · Lab**.
- **Complete visit** is always visible and needs a medical record.
- The **Lab** tab links to the existing request page and returns to the workspace.
- A tablet layout (1024 px).
