# P6 plan: payment coverage and write-offs

Pilot item **P6**. This is the plan for review. **No code has been written.** Four questions at the end need your answer before I start.

## What the code does today (read-only review)

| # | Finding | Effect |
|---|---|---|
| 1 | **"Covered" is computed in 7 places.** These are `PaymentService` (collect, due amount, patient summary, dashboard, bulk recalculation, single recalculation) and `ReservationService` (list). All use the same rule written out again: Active + Cancelled payments ≥ price | It works today, but any change has to be made 7 times. That's how F12 happened |
| 2 | **Editing a reservation doesn't recompute "Covered".** `UpdateReservationAsync` keeps the old `IsPaid` when the price changes (unless the new price is 0) | **Bug (F12 again):** a visit paid at 500 whose treatment is changed to an 800 one still shows **Covered**, while the Collect form says 300 is due |
| 3 | **Cancelling a payment counts as a write-off.** A cancelled payment still covers the visit and shows as "Written off" on the dashboard and patient summary | A cash entry typed by mistake can't be undone without the visit staying "paid". Also, anyone with *Cancel payments and change amounts* (`Payments.Adjust`, which the **front desk** has) can in effect write off a balance |
| 4 | **There's no "write off the balance" action.** The only way to excuse a balance is to record a fake payment and then cancel it | No reason category, no specific permission, and the audit entry looks like a cancellation |
| 5 | **A zero-amount payment** is accepted only for visits priced at 0 (it's recorded automatically) | Correct, and kept |
| 6 | The **Collect form already pre-fills** the clinic and the amount due, and **Complete visit already sends Reception to Collect** when a balance remains | Already done (from roadmap P6). Only checked by the tests |

## What P6 will change

1. **One coverage rule** (`Application/Payments/Coverage.cs`): which payments cover a visit, the amount due, and "Covered" (due = 0). All 7 places use it, and `IsPaid` is written in one place only, recalculated after **every** change: collect, cancel, change amount, write-off, and **reservation edit** (fixes finding 2).
2. **Write off a balance**: a new action on the reservation's payment area, **Write off**. It covers up to the remaining balance, needs a **reason** (see Q3) and the new permission **`Payments.WriteOff`** (*Write off balances*, sensitive). It's recorded as a payment with status **WrittenOff**: never counted as revenue, shown separately in the dashboard, report and patient summary, and audited as `Payment.WrittenOff`.
3. **Cancel** follows your answer to Q1.
4. **Clinic Admin group**, created as a built-in group (see Q2 and Q4).
5. **Reports:** collected revenue (Active only) and written off appear as separate figures everywhere. The figures for past periods don't change (see Q1).

## Affected files (expected)

| Area | Files |
|---|---|
| Rule | add `Application/Payments/Coverage.cs`; edit `Domain/Enums/PaymentStatusEnum.cs` (+`WrittenOff`) |
| Services | `PaymentService.cs` (write-off, single recalculation, reports), `ReservationService.cs` (edit recalculates; list uses the rule), `IPaymentService.cs`, DTOs (`WriteOffRequest`, totals) |
| Permissions | `PermissionCatalogue.cs` (`Payments.WriteOff`; Clinic Admin group), `Audit.cs` |
| Web | `PaymentsController.cs` (`WriteOff` GET/POST), views: `Payments/WriteOff.cshtml` (new), patient summary, reservation details, dashboard, report; Arabic strings |
| Database | Status is stored as text, so adding **WrittenOff** needs **no schema change**. If you choose to convert existing cancelled payments (Q1), that's **one data migration (an UPDATE)**, with backup and YES approval through the script, as usual. The write-off reason category goes in a new short column (`WriteOffReason`), which is one small additive migration |
| Tests | access matrix (+2 actions), `access-matrix` groups (+Clinic Admin), new `CoverageTests`, `WriteOffTests`, reservation edit coverage test |

## Risks and how they're handled

| Risk | Handling |
|---|---|
| Past reports change | Old cancelled payments keep counting as written off (Q1, recommended option). A test compares a dashboard total before and after |
| Two people pay or write off at once | Write-off runs under the same per-patient payment lock as Collect, and re-checks the balance inside it |
| Write-off used as a discount loophole | Separate permission, required reason category, audit entry with amount and reason, and a separate figure on the dashboard |
| The coverage rule moves into one place and a page shows a different number | Tests cover each place against the same scenarios (paid, partly paid, written off, cancelled, 0-price, price edited) |

## Test approach

- **Unit tests** for the rule: what counts, the amount due, covered.
- **Service tests (in-memory database):**
  - write-off needs the permission, a reason and a balance, and can't exceed the balance;
  - it covers the visit, isn't revenue and is audited;
  - cancel behaves as decided;
  - editing the price recomputes Covered;
  - the dashboard shows revenue and written off separately.
- **Access matrix:** `Payments.WriteOff` on the two new actions; Clinic Admin's permissions.
- **Manual check script** for you, as before.

## Rollback

Revert the PR. If the data migration (Q1) ran, its `Down` sets converted payments back to Cancelled. A backup is also taken by the script before applying.

## Acceptance criteria (roadmap P6)

- "Covered" and the balance due **always agree**, on every page (tests).
- A write-off **requires `Payments.WriteOff` and a reason**, appears in the **audit log**, and is shown in reports **separately from revenue**.
- The Collect form needs **no re-entry** of known data (already true; tested).

---

## Questions

**Q1. What should "Cancel payment" mean from now on?**
- **A (recommended):** cancel **voids** a payment: the balance reopens, as if it was never collected. Excusing a balance is done only by the new **Write off**. Existing cancelled payments are converted to **WrittenOff** (the data migration), so past visits and reports don't change.
- **B:** keep cancel as a write-off, but require `Payments.WriteOff` to cancel. No void.
- **C:** like A, but existing cancelled payments become voids. Past visits with a cancelled payment may show a balance again.

**Q2. Who may write off?** Recommended: **Reception supervisors and Clinic Admin** (the roadmap's choice), plus Admin, who has everything. Alternatives: Clinic Admin only; or also the front desk.

**Q3. The write-off reason:** Recommended: **choose a category** (Discount, Hardship, Staff or family, Billing error, Other), plus a required note for "Other". This makes the report readable. Alternative: free text only.

**Q4. What is the Clinic Admin group for?** Recommended: **clinic manager.** Everything Reception supervisors have, plus write-offs, the full payment dashboard, reports for any date, clinic settings (including opening hours, `Settings.Clinics`) and all calendars. It has no user or permission administration and no clinical records. Alternative: write-offs only (you add more later in Permission groups).
