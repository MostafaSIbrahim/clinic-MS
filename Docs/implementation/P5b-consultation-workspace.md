# Slice P5b: the consultation workspace

Pilot item **P5** (consultation workspace), slice b. This slice **closes P5**. It follows your decisions: **one page with tabs**, and the Lab tab **links out to the request page and returns**.

**Claude implemented this slice at the owner's request.** The owner runs one command, does the manual check, and pushes.

| | |
|---|---|
| Branch | `feature/p5b-consultation-workspace` (from the working copy, after PR #47 is merged) |
| Behaviour change | **1. One page with four tabs.** The medical record page (where **Start consultation** leads) is now the visit workspace, with the tabs **Notes · Treatments (n) · Prescriptions (n) · Lab (n)**. Before, it was one long page of cards. The counts show what each tab holds without opening it. **2. The visit bar stays visible** at the top while you scroll, on every tab. It shows the record number, Locked/Editable, the date, **Complete visit**, **Back to queue** and **More actions** (Back to patient, View reservation, Lock record). **Complete visit** follows the same rule as before: it appears for the visit's own record when you may act on that consultation. **It still requires a medical record**, because the page *is* the record. **3. Coming back to the same tab.** Adding or removing a treatment returns to **Treatments**. Creating a prescription, marking one printed, or **Back to Record**/**Cancel** on a prescription returns to **Prescriptions**. The address keeps the open tab (`?tab=lab`), so reloading the page keeps the tab. **4. Lab links out and returns.** **Request lab tests** opens the existing request page. After requesting, the request slip has a green **Back to the visit** button that opens the **Lab** tab. **Cancel** on the request page and **View Medical Record** on a lab test also return to the Lab tab. **5. Tablet layout (1024 px and below):** the tabs share the width as large touch targets (48 px tall), the notes stack label-over-text, and buttons are at least 44 px. On a phone, the visit bar stops sticking and its buttons go full width. **6. English and Arabic** for the whole workspace |
| Unchanged | The record's content and rules: editing notes (**Edit notes**, now on the Notes tab), locking, treatments, prescriptions with the P5a allergy check, attachments, printing, and the completion and payment hand-off. The request, slip and lab-result pages themselves aren't changed beyond the return links |
| Removed | The "Clinical notes · Treatments · Prescriptions · Analyses" jump buttons; the tabs replace them. An unused medicine-picker script on the record page (it looked for a field that no longer exists) |
| Database | None |
| Tests | +9 .NET (about **405**): a requested tab opens (any case); a missing or unknown tab opens Notes; the four tabs are in order. +1 JavaScript: the tab's name is read correctly to keep it in the address |

## Files

| Action | Path |
|---|---|
| Add | `Web/Models/VisitTabs.cs`, `tests/.../Web/VisitTabsTests.cs` |
| Rewrite | `Views/MedicalRecords/Details.cshtml` (the workspace) |
| Edit | `Web/Controllers/MedicalRecordsController.cs` (`Details` takes `tab`; actions return to their tab) |
| Edit | `Views/Analysis/PrintRequestSlip.cshtml` (Back to the visit), `Views/Analysis/Request.cshtml`, `Views/Analysis/Details.cshtml`, `Views/MedicalRecords/CreatePrescription.cshtml`, `Views/MedicalRecords/PrescriptionDetails.cshtml` (links return to the tab) |
| Edit | `wwwroot/js/app-tabs.js` (`data-tab-param` keeps the tab in the address), `tests/app-tabs.test.cjs`, `wwwroot/css/site.css` (visit bar, tablet), `Resources/SharedResource.ar.resx` |

No permissions or pages were added. `Details` gained an optional `tab` parameter, so the access matrix is unchanged.

## Rollback

Revert the PR's merge commit.

---

## Your steps

After merging **PR #47**, from `D:\MyDevWork\clinic-MS`:

```powershell
.\.assessment\slice.ps1 -Branch feature/p5b-consultation-workspace `
  -Message 'P5b: consultation workspace with tabs (Notes, Treatments, Prescriptions, Lab), sticky visit bar, return to tab, tablet layout'
```

There's no migration, so there's no database approval step.

### Manual check (while the app runs)

Use a **test patient** who is checked in today with a doctor.

1. **As that Doctor: My Queue → Start consultation → create the record.** The record opens on **Notes** with the visit bar on top: the number, **Editable**, the date, **Complete visit**, **Back to queue**, **More actions**.
2. **Treatments tab:** add a treatment. You're back on **Treatments**, and the tab now reads **Treatments (1)**. Remove it: still on Treatments.
3. **Prescriptions tab → New prescription →** save one. You return to **Prescriptions (1)**. Open it, then **Back to Record**: Prescriptions again.
4. **Lab tab → Request lab tests →** pick one, then request it. On the slip, click **Back to the visit**: the **Lab (1)** tab shows the test. Open it, then **View Medical Record**: the Lab tab again.
5. **Reload** the page while on Lab. It stays on Lab, and the address shows `?tab=lab`.
6. **Keyboard:** click a tab, then use the arrow keys, Home and End. In Arabic the arrows are mirrored.
7. **Scroll** a long notes page. The visit bar stays at the top with **Complete visit**.
8. **Tablet:** in the browser's developer tools, choose a 1024 × 768 device (or use a real tablet). The four tabs fill the width, the notes read label-over-text, and nothing scrolls sideways.
9. **Complete visit** from any tab, and confirm. It works as before, including the payment hand-off for Reception.
10. **Arabic:** the tabs read ملاحظات · العلاجات · الوصفات · التحاليل, and the button reads إنهاء الزيارة.
11. **Locked record (optional):** after **Lock record**, the page shows **Locked**, with no Edit, Add or Remove.

When you've finished, press Enter in the script, then type `YES` to commit and push. Open the PR and send me its number and the CI result.

## Where we are after this

**P5 is complete.** Next in the pilot:
- **P6, payment coverage:** payments cover each visit, with zero and written-off payments, a `Payments.WriteOff` permission, and a Clinic Admin group. This touches money, so I'll show you the plan first.
- **P7:** acceptance.
