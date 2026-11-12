# Slice M3b: prescriptions

Group 4, item **M3** (clinical screens), slice b. Follows the approved plan (`M3-clinical-screens-plan.md`: bilingual printout; compact labelled drug rows; clinic details stay in the server settings until M5).

**Claude implemented this slice at the owner's request.** The owner runs one command, does the manual check, and pushes.

| | |
|---|---|
| Branch | `feature/m3b-prescriptions` (from `main` after PR #68; your folder is on it) |
| Behaviour change | **1. Every drug field has a visible label**: Medication, Dosage, Frequency, Duration, Route, Instructions. This is on the new-prescription form and the add-drug row, instead of grey placeholder text that disappeared when you typed. On desk and tablet the fields stay in a compact row; on a phone they stack. Each drug is announced as "Drug 1", "Drug 2"… to screen readers. **2. New prescription** uses the shared page heading and the shared field for the notes (hint: *Printed under the drugs*), and is in Arabic. **3. The prescription page:** shared layout and Arabic; the drug table becomes cards on phones; Instructions is its own column; the patient, doctor and attachments panels are translated. **Print** says it opens in a new tab. **4. Deleting an attachment** now asks first, and its button has a name: it was a bare "×". **5. The printout is bilingual**, whatever language the person printing uses: each heading in English with Arabic under it (Patient, Age, Gender, Diagnosis, Medication, Dosage, Frequency, Duration, Route, Notes, licence number, prescription number, the validity line). Routes print in both languages. Drug names, doses and instructions print as the doctor wrote them. Paper sizes (A4, A5, Letter) and the three designs are kept; the print controls are translated |
| Unchanged | Field names sent to the server, the drug search (P8b), the route list (P9a), the allergy warning and *Prescribe anyway* (P5a), who can prescribe, and the clinic details on the printout (`ClinicInfo`) |
| Database | None |
| Tests | No new unit tests (layout and text). The browser test prescribes through the new form, including the allergy warning and *Prescribe anyway*, and checks the form for accessibility at the three widths |

## Files

| Action | Path |
|---|---|
| Edit | `Web/Views/MedicalRecords/CreatePrescription.cshtml`, `PrescriptionDetails.cshtml` (rewritten), `PrintPrescription.cshtml` (bilingual headings), `Resources/SharedResource.ar.resx` (+29) |

## Rollback

Revert the PR's merge commit.

---

## Your steps

In PowerShell:

```powershell
.\.assessment\slice.ps1 -Branch feature/m3b-prescriptions `
  -Message 'M3b: labelled drug fields, prescription page in the shared layout and Arabic, bilingual printout'
```

### Manual check (while the app runs)

Use a test patient with an allergy (e.g. Penicillin) and an open record.

1. **New prescription**: each field of the drug row has its label above it. Type `aug` in Medication: suggestions appear as before. Choose one.
2. **Add drug** adds a second labelled row; the bin removes it.
3. Type a drug matching the allergy (e.g. `Penicillin V`): the allergy warning appears, saving is refused until you tick *Prescribe anyway*, then it saves, as before.
4. The **prescription page**: drugs with an Instructions column, the patient, doctor and attachments panels. **Add another drug** with labelled fields works, including the allergy warning.
5. Upload a small PNG, then delete it: it asks first.
6. **Print**: the headings show English with Arabic under them. Routes are in both languages, and drug names are as typed. Switch the paper to **A5**: it still fits. Try **A4** and the three designs.
7. Switch to **Arabic** and print again: the printout looks the same (bilingual). The form and the prescription page are in Arabic.
8. At **phone width**, the drug fields stack and the drug table becomes cards.

When you've finished, press Enter in the script, then type `YES` to commit and push. Open the PR, merge it, run `git switch main` and `git pull`, and send me its number and the CI result.

## Next: M3c, which closes M3

The lab screens: list, request, details and request slip, in the shared layout with status badges and Arabic. The lab rules don't change.
