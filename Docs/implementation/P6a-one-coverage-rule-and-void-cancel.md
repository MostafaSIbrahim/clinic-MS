# Slice P6a: one coverage rule, and cancel means void

Pilot item **P6** (payment coverage), slice a. This follows the plan (`P6-payment-coverage-plan.md`) and your answers: **cancel voids; old cancelled payments become write-offs**. P6b adds the **Write off** action, its permission, reason categories and the Clinic Admin group.

**Claude implemented this slice at the owner's request.** The owner runs one command, approves the data change, does the manual check, and pushes.

| | |
|---|---|
| Branch | `feature/p6a-coverage-rule` (from the working copy, after PR #48 is merged) |
| Behaviour change | **1. One rule** (`Application/Billing/Coverage.cs`) decides what covers a visit: **Active** (collected) and **WrittenOff** (excused) payments. All 7 places that worked it out separately now use it: collect, amount due, patient summary, unpaid dashboard, reservation list, single and bulk recalculation. **2. Cancel = void.** Cancelling a payment now makes the amount **due again**, and the visit stops showing **Covered**. For a nutrition enrollment, the amount is taken off the enrollment's paid total. Before, a cancelled payment still covered the visit. The Cancel page explains this. **3. Write-offs have their own status**, **WrittenOff**. They're shown as a yellow **Written off** badge, and voids as a grey **Cancelled (void)** badge, struck through. They're never revenue (unchanged: revenue counts Active only). The patient summary's tile reads **Written Off**. **4. A write-off can't be cancelled or have its amount changed** from the payment adjustments (Change amount / Cancel). P6b adds undoing a write-off, under the new permission. **5. Fix (F12): editing a reservation recomputes Covered** from its payments. Before, raising the price (e.g. changing the treatment) kept **Covered** while the Collect form showed a balance |
| Database | **One data-only migration**, `CancelledPaymentsAreWriteOffs`: `UPDATE Payments SET Status = 'WrittenOff' WHERE Status = 'Cancelled'`. **No schema change.** Status is stored as text. Because of this, past visits keep the same coverage, and past dashboards keep the same "written off" totals. The script backs up the database first and asks for **YES** |
| Down (rollback) | Sets **WrittenOff** back to **Cancelled**. Note: any write-off made after P6b would also become Cancelled. Before P6b nothing else creates WrittenOff, so for now this is exact |
| Tests | +15 (about **420**). They cover: which statuses cover (enum, text and the database expression); due and covered agree for any price; no price owes nothing. In memory: collected plus written off settles the visit; a cancelled payment doesn't cover; **cancelling reopens the balance and clears Covered**; a write-off can't be cancelled or changed here; the patient summary counts write-offs, not voids, and its balance equals the Collect form's |
| Not tested automatically | The reservation-edit fix runs as a direct database update, which the in-memory test database can't execute. It's covered by the rule's tests and by manual check step 5 |

## Files

| Action | Path |
|---|---|
| Add | `Application/Billing/Coverage.cs`, `Infrastructure/Migrations/20261108180000_CancelledPaymentsAreWriteOffs.cs` (hand-written, data only), `tests/.../Application/CoverageTests.cs` |
| Edit | `Domain/Enums/PaymentStatusEnum.cs` (+`WrittenOff`) |
| Edit | `Application/Services/PaymentService.cs` (the rule everywhere; cancel voids; write-offs protected), `ReservationService.cs` (list and create use the rule; **edit recomputes Covered**) |
| Edit | `Views/Payments/PatientSummary.cshtml`, `Report.cshtml` (badges), `Cancel.cshtml` (explains the void) |
| Local only | `.assessment/slice.ps1` now accepts a data-only migration (one with no named operations). It's already updated on your machine |

## Rollback

Revert the PR's merge commit, then roll the database back to the previous migration (the `Down` above), or restore the backup the script took.

---

## Your steps

**Optional, read-only, before running:** see what will be converted.

```sql
SELECT Status, COUNT(*) AS Payments, SUM(Amount) AS Total FROM Payments GROUP BY Status;
```

After merging **PR #48**, from `D:\MyDevWork\clinic-MS`:

```powershell
.\.assessment\slice.ps1 -Branch feature/p6a-coverage-rule -Migration CancelledPaymentsAreWriteOffs `
  -Require 'WrittenOff','Cancelled' `
  -Message 'P6a: one coverage rule; cancel voids a payment; old cancelled payments become write-offs; edit recomputes Covered'
```

The script should show `Up contains:` with nothing listed, because the migration is plain SQL with no named operations. It should then show **Migration matches the slice doc**. Type **YES** to approve the data change. The script backs up the database, and the app applies the migration when it starts.

### Manual check (while the app runs)

Use a **test patient**.

1. **History unchanged:** open a patient who had a cancelled payment before. It now shows **Written off** (yellow), and the visit is still **Covered**. The summary's **Written Off** tile shows the same total as before. You can run the SQL above again: the Cancelled rows are now WrittenOff.
2. **Void:** take a finished visit (e.g. 800) and collect the full amount. It shows **Covered**. **Cancel** that payment (the page explains it's a void) with a reason. The row is struck through as **Cancelled (void)**, the visit shows **Not fully covered**, and **Collect** offers 800 again.
3. **Partial:** collect 500 of 800, then cancel it. 800 is due again.
4. **Write-off protected:** on a **Written off** row there's no Change amount or Cancel button. Opening `/Payments/Cancel?paymentId=<that id>` and submitting is refused ("A write-off can't be cancelled here").
5. **Edit fix:** take an upcoming visit priced 200 that has been paid in full (**Covered**). As **Admin**, edit it to a treatment priced 800 (or change the price). It now shows **Not fully covered**, and once the patient is checked in, Collect offers 600. Before, it stayed **Covered**.
6. **Dashboard:** the payment dashboard's unpaid lists and totals look as before. Revenue doesn't include write-offs.

When you've finished, press Enter in the script, then type `YES` to commit and push. Open the PR and send me its number and the CI result.

## Next: P6b, write off a balance

- A **Write off** action with the `Payments.WriteOff` permission, for Reception supervisors, Clinic Admin and Admin.
- A reason category (Discount, Hardship, Staff or family, Billing error, Other, with a note required for Other).
- Undoing a write-off, under the same permission.
- An audit entry for each write-off.
- A separate **Written off by reason** figure in the reports.
- The **Clinic Admin** group (clinic manager).
