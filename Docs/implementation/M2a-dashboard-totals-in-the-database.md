# Slice M2a: the payment dashboard adds up in the database

Group 4, item **M2** (billing), slice a. Follows the approved plan (`M2-billing-plan.md`, approved 11 Nov 2026 with the recommended choices).

**Claude implemented this slice at the owner's request.** The owner runs one command, does the manual check, and pushes.

| | |
|---|---|
| Branch | `feature/m2a-dashboard-in-database` (from `main` after PR #61) |
| Behaviour change | **1. Same figures, computed differently.** The payment dashboard and the Unpaid list no longer load every reservation the clinic has ever had into memory. **Only visits that still owe money** are loaded (they are the rows of the unpaid lists). **Collected, settled, written-off, by-clinic and by-source figures** are sums and groups computed by SQL Server. **2. Proven equal:** the pre-M2 code is kept in the tests (`LegacyPaymentDashboard`), and a test runs old and new side by side on 600 varied visits and checks that every figure, row and group is identical. It covers the full and front-desk views, all time, one month, open-ended ranges and an empty range. **3. Two small, visible differences:** the front desk's Unpaid lists now come **newest first** (before, the order was whatever the database returned); and where two clinics, sources or reasons have exactly the same total, they're now listed by name |
| CI | **New job `performance`** (report-only, like e2e was at first), about 3–5 minutes. **(a)** It builds an empty SQL Server database by running **the reviewed idempotent migration script**, the same file you run on MonsterASP, so a script that fails on a fresh database, as on 9 Nov, is caught here. **(b)** It seeds **50,000 reservations** with payments and times the dashboard: full all-time, full one month, and front desk (10 runs each, after a warm-up). It shows the old version's time for comparison, checks the figures match on that data, and fails (reported, not blocking) if p95 is over 1 second. The results table appears on the job's **Summary** page |
| Database | None. No migration |
| Tests | +8: 6 side-by-side comparisons, one check that the test data covers every case (unpaid in both lists, settled payments, write-offs with and without a reason, no-clinic and no-source groups), and newest-first ordering |

## Files

| Action | Path |
|---|---|
| Edit | `Application/Services/PaymentService.cs` (`GetPaymentDashboardAsync` rewritten; nothing else changed) |
| Add | `tests/.../Application/LegacyPaymentDashboard.cs` (pre-M2 code, frozen), `DashboardTestData.cs` (repeatable test data, shared), `PaymentDashboardEquivalenceTests.cs` |
| Add | `tests/PerformanceChecks/` (console program, like `DeploymentChecks`; not part of the solution build) |
| Edit | `.github/workflows/ci.yml` (job `performance`) |

## Rollback

Revert the PR's merge commit.

---

## Your steps

Your folder is still on the M1d branch, with the M2a changes in it. They don't touch any M1d file, so merge **PR #61** and then:

```powershell
git switch main
git pull
git status
```

`git status` should list `PaymentService.cs` and `ci.yml` as changed, and the new test files, `tests/PerformanceChecks/` and this doc as new. Then:

```powershell
.\.assessment\slice.ps1 -Branch feature/m2a-dashboard-in-database `
  -Message 'M2a: payment dashboard totals computed in the database, proven equal to before; CI performance check with 50,000 reservations and the migration script on an empty database'
```

### Manual check (while the app runs)

The side-by-side test is the proof that the figures are unchanged. Your check is that the screens still work and add up.

1. **Payment dashboard** (as Admin or a clinic manager), no dates: the page loads. **Unpaid completed** equals the sum of the balances in its list, and **unpaid pending** likewise.
2. Choose a date range (e.g. this month): **fully paid** and **written off** change to that period's figures. A range with no payments shows zeros, not an error.
3. **Unpaid** (as Reception): the rows come newest first, and the total matches the rows.
4. Collect a payment for a visit with a balance. It leaves the unpaid list, and the totals change by that amount. Write off a balance as a supervisor: it appears under **written off by reason**.

### In CI

Open the PR's **Checks** → **performance** → **Summary**. Send me the table (or a screenshot). It shows each view's time before and after M2, with 50,000 reservations, and whether the figures matched.

When you've finished, press Enter in the script, then type `YES` to commit and push. Open the PR, run `git switch main` and `git pull` after merging, and send me its number and the CI results, including **performance**.

## Next: M2b, the Dashboard and Unpaid screens

New layout, Arabic, page links for long unpaid lists. The full dashboard's dates default to this month.
