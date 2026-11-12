# Slice M2d: the payments report (closes M2)

Group 4, item **M2** (billing), slice d. This slice **closes M2**. Follows the approved plan (`M2-billing-plan.md`; decisions 2 and 3: remove the repair actions; add **Download as CSV**).

**Claude implemented this slice at the owner's request.** The owner runs one command, does the manual check, and pushes.

| | |
|---|---|
| Branch | `feature/m2d-payments-report` (from `main` after PR #66; your folder is on it) |
| Behaviour change | **1. The report follows the clinic clock.** "Today" and the day boundaries come from `Clinic:TimeZone`, like every other screen since F2, not from a fixed *Africa/Cairo* in the code. For Cairo the results are identical. **2. Totals by payment method and by doctor** appear above the list, for collected money only. The doctor is the one on the visit or nutrition enrollment paid for. **3. Download as CSV** appears once the report has rows. It downloads exactly the rows on screen, with the same access rules: the front desk gets today only, doctors their own. The file opens in Excel with Arabic names intact, times in clinic time, and amounts as plain numbers. A value that Excel would treat as a formula is written as text. **4. A Doctor column**, and each patient's name opens their Payments tab. **5. Arabic and the shared layout:** filter bar, cards, a table that becomes cards on phones, status badges. **6. The two old repair actions are removed** (*recalculate paid status*, *back-fill zero-cost payments*). They had no screen, and since P6 every payment change keeps coverage correct |
| Unchanged | Who sees the report, which payments it includes, and the total |
| Database | None |
| Tests | +6 (the CSV: one line per payment in clinic time, plain amounts, the byte-order mark Excel needs for Arabic, quoting, no formulas). The existing report tests, for role scope, today only for the front desk and the inclusive end date, pass unchanged. The access baseline drops the two repair actions and adds the CSV download with the report's access. The browser test checks the report for accessibility at the three widths |

## Files

| Action | Path |
|---|---|
| Add | `Web/Reports/PaymentReportCsv.cs`, `tests/.../Web/Payments/PaymentReportCsvTests.cs` |
| Edit | `Web/Controllers/PaymentsController.cs` (report on the clinic clock; totals; `ReportCsv`; repair actions removed), `Web/Views/Payments/Report.cshtml` (rewritten), `Application/Services/PaymentService.cs` (doctor on report rows; repair methods removed), `Interfaces/Services/IPaymentService.cs`, `DTOs/Payment/PaymentDtos.cs`, `Resources/SharedResource.ar.resx` (+13) |
| Edit | tests: `PaymentsControllerAccessTests.cs`, `Security/access-matrix.txt`, `e2e/tests/pilot-flow.spec.js` |

## Rollback

Revert the PR's merge commit.

---

## Your steps

In PowerShell:

```powershell
.\.assessment\slice.ps1 -Branch feature/m2d-payments-report `
  -Message 'M2d: payments report on the clinic clock, totals by method and doctor, Download as CSV, Arabic; old repair actions removed; closes M2'
```

### Manual check (while the app runs)

1. **As Admin or a clinic manager**, **Payment Reports**: choose this month and select **Show**. You see the total collected, **By payment method** and **By doctor**, then the list with a Doctor column. Times match when the payments were made.
2. **Download as CSV** downloads `payments-…-to-….csv`. Open it in Excel: Arabic names read correctly, amounts are numbers you can add up, and there's one row per payment on screen.
3. A patient's name opens their **Payments** tab.
4. **As Reception**: there are no dates, only **Show today's payments**. Today's payments and the CSV show today only.
5. **As a doctor**: only payments for your own visits, in the report and in the CSV.
6. **Arabic:** the report is in Arabic. At phone width the table becomes cards.

When you've finished, press Enter in the script, then type `YES` to commit and push. Open the PR, merge it, run `git switch main` and `git pull`, and send me its number and the CI result.

## M2 is complete

Billing screens are done:
- the dashboard adds up in the database: proven equal to before, and 671 ms with 50,000 reservations;
- pages and this month by default;
- the forms and Payments tab are in Arabic;
- the report follows the clinic clock and downloads as CSV.

**Next on the roadmap: M3, the clinical screens.** Records and prescriptions get the shared layout and Arabic, and the lab screens are restyled only (their rules stay, D9). As with M1 and M2, I'll send the plan first.
