# Slice M1c: Payments and Nutrition tabs

Group 4, item **M1** (patient workspace), slice c. Follows the approved plan (`M1-patient-workspace-plan.md`).

**Claude implemented this slice at the owner's request.** The owner deletes two old views, runs one command, does the manual check, and pushes.

| | |
|---|---|
| Branch | `feature/m1c-payments-nutrition-tabs` (from `main` after PR #59; your folder is already on it) |
| Behaviour change | **1. Payments tab:** the patient's payment summary, moved unchanged: totals (charged, paid, written off, collectible balance), what's ready for collection with **Collect** and **Write off**, and the payment history with audit, change amount, cancel and undo write-off. The figures come from the same service as before. **2. Collecting, writing off and adjusting come back to the Payments tab.** They used to return to the separate summary page. **3. Nutrition tab:** the patient's enrollments (package, doctor, start, end, price, status), **Enroll in a package** for people who can enroll, and **Open** for each. Enrollment rules are unchanged. **4. The row of section buttons above the tabs is gone.** Every section is now a tab. **5. The first tab:** nutritionists open a patient on **Nutrition**. The front desk is unchanged (Visits), and so are doctors (Records). **6. Old addresses redirect:** `/Payments/PatientSummary?patientId=…` opens the Payments tab and `/Nutrition/PatientEnrollments?patientId=…` opens Nutrition. Existing links that use them (Collect, Write off, the reservation page, Enroll's Cancel) keep working |
| Not yet | The Payments tab's own text is still English, as the summary page was; it's translated with the billing screens (M2). The Nutrition tab is in Arabic |
| Database | None |
| Tests | +4 (who sees Payments and Nutrition; nutritionists start on Nutrition; doctors asking for Payments get Records). The browser test opens the old payment summary address and checks that it lands on the Payments tab; collecting and writing off run there, and Payments and Nutrition are checked for accessibility at 1366, 1024 and 390 px |

## Files

| Action | Path |
|---|---|
| Add | `Web/Views/Patients/_PatientPayments.cshtml` (moved from the summary page), `Web/Views/Patients/_PatientNutrition.cshtml`, `wwwroot/js/payment-audit.js` (the audit dialog script, moved out of the page) |
| Edit | `Web/Models/PatientTabs.cs`, `Web/Controllers/PatientsController.cs`, `PaymentsController.cs` and `NutritionController.cs` (old pages redirect), `Web/Views/Patients/Details.cshtml` (two tabs; section buttons removed), `Resources/SharedResource.ar.resx` (+11) |
| Edit | tests: `Web/PatientTabsTests.cs`, `e2e/tests/pilot-flow.spec.js` |
| **Delete** (you, Step 1) | `Web/Views/Payments/PatientSummary.cshtml`, `Web/Views/Nutrition/PatientEnrollments.cshtml` |

## Rollback

Revert the PR's merge commit. This brings back the two pages.

---

## Your steps

Your folder is on an up-to-date `main` with the M1c changes, so in **PowerShell**:

```powershell
cd D:\MyDevWork\clinic-MS
Remove-Item SafyaClinic\SafyaClinic.Web\Views\Payments\PatientSummary.cshtml
Remove-Item SafyaClinic\SafyaClinic.Web\Views\Nutrition\PatientEnrollments.cshtml
.\.assessment\slice.ps1 -Branch feature/m1c-payments-nutrition-tabs `
  -Message 'M1c: Payments and Nutrition tabs in the patient workspace; collect and write-off return to the tab; old pages redirect'
```

### Manual check (while the app runs)

1. **As Reception**, open a test patient with paid and unpaid visits → **Payments**. The four totals agree with the visits and payments listed below them (the content and figures are moved unchanged, from the same service).
2. **Collect** part of a balance. You come back to the Payments tab, with the "Payment of …" message, and the totals updated.
3. In the history, **audit** (the clock icon) opens the audit dialog. **Change amount** and **Cancel** open their pages, and after saving you're back on the tab.
4. **As a supervisor**, **Write off** the rest with a reason. You're back on the tab, and the row shows **Written off**. **Undo write-off** also returns to the tab.
5. The old address `/Payments/PatientSummary?patientId=<id>` opens the Payments tab. The **Payment Summary** button on a reservation page does too.
6. **As a nutritionist** (or admin), open a patient with an enrollment: **Nutrition** lists it. **Enroll in a package** opens the form, and its **Cancel** returns to the tab. `/Nutrition/PatientEnrollments?patientId=<id>` opens the tab.
7. **As a doctor**: no Payments or Nutrition tab. `?tab=payments` opens Records.
8. Above the tabs there are no section buttons any more. The header keeps **Book appointment**, Timeline and **More Actions**.
9. **Arabic:** the Nutrition tab is in Arabic. At phone width both tabs fit without sideways scrolling.

When you've finished, press Enter in the script, then type `YES` to commit and push. Open the PR and send me its number and the CI result.

## Next: M1d, which closes M1

**Timeline** becomes a tab instead of a page that opens in a new browser tab. The Overview text gets Arabic, and the browser test visits every tab.
