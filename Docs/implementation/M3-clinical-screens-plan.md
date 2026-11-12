# M3 plan: clinical screens

Group 4, item **M3** of the roadmap: records and prescriptions (including the printout), lab screens **restyled only** (their rules stay, D9). The drug-search part of M3 was already done in P8b. This is the plan for review. **Nothing has been changed yet.**

## What I found

| Screen | Today |
|---|---|
| **Consultation workspace** (record page, P5b) | Already on the shared layout and in Arabic. A few leftovers: some buttons and messages are still English |
| **New record / Edit record** | Older layout, English only, an inline style. Six text areas with no hints. Edit doesn't show the patient header (allergies) |
| **New prescription** | Drug rows have **placeholders instead of labels** (Medication, Dosage, Frequency, Duration…), which screen readers and Arabic don't handle well. Partly Arabic (P8b, P9a) |
| **Prescription details** | Older layout; the add-drug row has the same placeholder problem; mostly English |
| **Printed prescription** | English only. Clinic details from `ClinicInfo` |
| **Lab: list, request, details, request slip** | Older layout, English only; status as plain words; two inline styles |

## Slices

| Slice | Content | Risk |
|---|---|---|
| **M3a: Record forms** | New record and Edit record: shared form fields, the patient header with allergies on both, Arabic, hints on the clinical fields. The consultation workspace's remaining English. Rules unchanged | Low |
| **M3b: Prescriptions** | New prescription and the add-drug row: each field has a **visible label** (compact row on desk and tablet, stacked on phones), and the allergy warning and drug search stay as they are. Prescription details in the shared layout and Arabic. **The printout** in the chosen language (decision 1), A5 and A4 preserved | Low–Medium. The browser test covers prescribing with the allergy warning |
| **M3c: Lab screens** | List, request, details and request slip: shared layout, status badges, Arabic. **Lab rules unchanged (D9):** same statuses, same who-can-do-what, same attachments | Low |

Each slice is the usual flow: I implement it, you run the script, check and push. **No database change** is expected.

## Decisions for you

1. **Language of the printed prescription.**
   - *Recommended:* **bilingual**: headings and labels in Arabic and English side by side, with drug names and instructions as the doctor wrote them. One printout works for every patient and pharmacy.
   - *Alternative:* the printout follows the language of the person printing.
   - *Alternative:* English only, as today.
2. **Drug rows on the prescription form.**
   - *Recommended:* compact rows with a small visible label above each field (Medication, Dosage, Frequency, Duration, Route, Instructions), which become stacked on a phone.
   - *Alternative:* one drug per card, every field stacked, which is easier to read but longer.
3. **Clinic details on printouts.**
   - *Recommended:* keep `ClinicInfo` from the server settings for now; the clinic profile screen comes with **M5** (settings).
   - *Alternative:* build the clinic profile screen now, inside M3b.

## Risks

| Risk | Handling |
|---|---|
| A doctor loses typed notes while the form changes | The fields, names and save rules stay the same; only labels and layout change. Tests cover create and edit |
| Prescribing gets slower | Same number of fields and the same keyboard flow; drug search and the allergy warning are untouched. The browser test times nothing new but checks the flow and its accessibility |
| The printout no longer fits A5 | Checked at A5 and A4 in the manual check; the print stylesheet is kept |

## Rollback

Each slice is reverted on its own by reverting its PR's merge commit.

## Next

Reply with your choices, or **"approve M3"** for the recommended ones. I'll then implement M3a.
