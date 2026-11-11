# Slice M2b: the payment dashboard and Unpaid screens

Group 4, item **M2** (billing), slice b. Follows the approved plan (`M2-billing-plan.md`).

**Claude implemented this slice at the owner's request.** The owner runs one command, does the manual check, and pushes.

| | |
|---|---|
| Branch | `feature/m2b-dashboard-screens` (from `main` after PR #62; your folder is on it) |
| Behaviour change | **1. Unpaid lists in pages of 20,** newest first, with page links. The totals and the number of visits above them still cover **every** balance owed. They're added up in the database (price total minus the covering payments), and only the 20 rows on screen are loaded. **2. The payment dashboard opens on this month** (clinic time). Choose any dates, **Clear** returns to this month, and **Show all time** shows everything. The period is written under the heading. Unpaid lists always show every balance, whatever the period. **3. A fourth figure,** *Written off (not revenue)*, joins the top row. **4. Details are a button,** not a click on the row: each source and clinic row has a **Details** button, so it works from the keyboard and screen readers. It opens the same dialog: choose dates, then the details load. **5. New layout and Arabic** for the dashboard and the Unpaid screen: shared cards, tables that become cards on phones, status badges, and the patient's name linking to their Payments tab. **6.** The page-links component can now serve two lists on one page (`completedPage`, `pendingPage`) |
| Figures | Unchanged. The M2a side-by-side test still compares the complete lists with the pre-M2 code. A new test checks that page 2 of each list is rows 6–10 of the complete list, and that the totals and counts on a page equal those of the complete lists |
| Performance | The CI **performance** job now times what the screens ask for, one page of 20 rows per list. It still checks the complete figures against the pre-M2 code. Target: the all-time dashboard under 1 second (p95) with 50,000 reservations |
| Database | None |
| Tests | +2 (paged totals and rows, for the full and front-desk views). The browser test checks both dashboard views (this month and all time) for accessibility at 1366, 1024 and 390 px |

## Files

| Action | Path |
|---|---|
| Edit | `Application/Services/PaymentService.cs` (unpaid lists paged, totals in the database), `Interfaces/Services/IPaymentService.cs`, `DTOs/Payment/PaymentDtos.cs` (counts and page) |
| Edit | `Web/Controllers/PaymentsController.cs` (this month by default; pages), `Web/Views/Payments/Dashboard.cshtml`, `Unpaid.cshtml`, `_UnpaidSections.cshtml` (rewritten), `Web/Components/Pager.cs`, `Views/Shared/_AppPagination.cshtml` (page parameter name), `Resources/SharedResource.ar.resx` (+42) |
| Add | `wwwroot/js/payment-dashboard.js` (the details dialog, moved out of the page) |
| Edit | tests: `PaymentDashboardEquivalenceTests.cs`, `tests/PerformanceChecks/Program.cs`, `e2e/tests/pilot-flow.spec.js` |

## Rollback

Revert the PR's merge commit.

---

## Your steps

Your folder is on an up-to-date `main` with the M2b changes. In PowerShell:

```powershell
.\.assessment\slice.ps1 -Branch feature/m2b-dashboard-screens `
  -Message 'M2b: payment dashboard and Unpaid screens: pages of 20, this month by default, Details buttons, Arabic'
```

### Manual check (while the app runs)

1. **As Admin or a clinic manager**, open **Payment Dashboard**. The heading shows *Period: 1 Nov 2026 to 11 Nov 2026* (this month so far). **Fully paid** and **Written off** are this month's figures.
2. **Show all time**: the period reads *All time* and the figures widen. **Clear** returns to this month. A range of your own works too.
3. The unpaid totals don't change when you change the period.
4. With more than 20 unpaid visits, page links appear under a list. Moving through the pages of one list keeps the other list where it was.
5. On a source or clinic row, **Details** opens the dialog with the dashboard's dates filled in. **Show** loads the details, **Change dates** goes back, and **All time** loads everything. Try it with the keyboard: Tab to Details, then Enter.
6. A patient's name in an unpaid list opens their **Payments** tab. **Collect** opens the Collect form.
7. **As Reception**, **Unpaid Payments**: the two totals with the number of visits, the lists newest first, 20 at a time, and no revenue figures.
8. **Arabic:** both screens are in Arabic. At phone width the tables become cards.

When you've finished, press Enter in the script, then type `YES` to commit and push. Open the PR, and send me its number, the CI result and the **performance** table.

## Next: M2c

Collect, write off, change amount, cancel, undo write-off, the audit dialog and the Payments tab: shared form fields, Arabic and consistent Back links. The rules stay the same.
