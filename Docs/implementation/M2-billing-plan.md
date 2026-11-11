# M2 plan: billing screens

Group 4, item **M2** of the roadmap: collections, the payments report, the financial dashboard with database totals and date-bounded queries, and payment audit. This is the plan for review. **Nothing has been changed yet.**

Roadmap acceptance: **dashboard totals equal before and after on the same data**; the dashboard answers in **under 1 second (p95) with 50,000 reservations**; every billing screen meets *screen done* (shared components, one heading, no serious accessibility problems, keyboard, Arabic, phone to desk widths, browser-tested).

## What I found

| Screen | Today |
|---|---|
| **Payment dashboard** (full, for clinic managers and admin) | Loads **every reservation the clinic has ever had** into memory on each visit, with its payments, then adds up the unpaid lists, by-clinic and by-source totals in code (F21). Fine today; slow as the years add up |
| **Unpaid payments** (front desk and supervisors) | The same query, limited to visits that can have a balance. Still all of history |
| **Payments report** | Search-first and date-bounded (good). It uses a fixed `Africa/Cairo` zone instead of the clinic clock that the rest of the app uses since F2 |
| **Collect, Write off, Change amount, Cancel, Undo write-off, Audit** | Correct (P6) but English only, older layout |
| **Payments tab** (M1c) | English only |
| Two one-off repair endpoints (*recalculate paid status*, *backfill zero-cost payments*) | Admin-only POST actions with no screen. Written before P6. P6a now recomputes coverage on every change |

There's no test that pins today's dashboard figures, so a rewrite can't yet prove it changed nothing.

## Slices

| Slice | Content | Risk |
|---|---|---|
| **M2a: Pin the figures, then let the database add up** | First, a test with a varied set of visits and payments records **today's dashboard figures**: every total, list and group, for the full and the unpaid view, with and without dates. Then the dashboard's sums and groupings move into the database query, and the same test must give **the same figures**. A **performance check** in CI (report-only, like e2e) seeds **50,000 reservations** in a real SQL Server and times the dashboard | Medium. Financial figures. Guarded by the before-and-after test |
| **M2b: Dashboard and Unpaid screens** | Shared components, Arabic, phone layout. Long unpaid lists get pages. The full dashboard's date range defaults to **this month** (decision 1) | Low |
| **M2c: Collect, write off and adjustments** | Collect, Write off, Change amount, Cancel, Undo write-off, the audit dialog and the **Payments tab**: shared form fields, Arabic, consistent Back links to the Payments tab. Rules unchanged | Low. The browser test covers collect and write off |
| **M2d: Payments report** | The clinic clock instead of the fixed zone (same result for Cairo). Arabic, totals by method and by doctor, and an export (decision 3). The repair endpoints are dealt with (decision 2) | Low |

Each slice is the usual flow: I implement it, you run the script, check and push. **No database migration** is expected.

## Decisions for you

1. **What the dashboards cover by default.**
   - *Recommended:* the **unpaid lists always show every balance still owed**, however old, because money owed doesn't expire, with page links for long lists. The full dashboard's **collected and written-off figures** default to **this month**, and you can choose any range.
   - *Alternative:* everything defaults to the last 12 months.
2. **The two repair endpoints.**
   - *Recommended:* **remove them.** P6 keeps coverage correct on every change, and they have no screen.
   - *Alternative:* keep them for admin, on the readiness page.
3. **Exporting the payments report.**
   - *Recommended:* add **Download as CSV**, which opens in Excel, for the chosen dates, for people who can see the report.
   - *Alternative:* no export for now.
4. **The performance check.** Add the 50,000-reservation timing to CI as a report-only job, like e2e was at first? *Recommended:* yes. It costs about two minutes per PR.

## Risks

| Risk | Handling |
|---|---|
| A figure changes in the rewrite | M2a pins every figure first. The same test must pass after, and the old and new queries run side by side in the test |
| The database query adds up differently from the old code (rounding, empty groups, "no clinic" rows) | Test cases for each: zero amounts, write-offs before P6, payments with no clinic or source, cancelled visits |
| Front desk sees financial totals | Unchanged rule (`Payments.ViewFullDashboard`), covered by the existing access tests |
| CI gets slower | The performance job runs alongside the others and only reports |

## Rollback

Each slice is reverted on its own by reverting its PR's merge commit.

## Next

Reply with your choices, or **"approve M2"** for the recommended ones. I'll then implement M2a.
