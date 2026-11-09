# Slice P6b: write off a balance, and the Clinic Admin group

Pilot item **P6**, slice b. This **closes P6**. It follows the plan (`P6-payment-coverage-plan.md`) and your answers:
- write-offs for **Reception supervisors and Clinic Admin** (and Admin);
- a **category plus a note** as the reason;
- **Clinic Admin = clinic manager**.

**Claude implemented this slice at the owner's request.** The owner runs one command, approves the database change, does the manual check, and pushes.

| | |
|---|---|
| Branch | `feature/p6b-write-offs` (from the working copy, after PR #52 is merged) |
| Behaviour change | **1. Write off.** For people with the new permission **Write off a visit's balance** (`Payments.WriteOff`, sensitive), a **Write off** button appears: next to **Collect** on the patient's payment summary (balances of visits), and as **Write off instead** on the Collect form. The page shows the remaining balance and asks for the **amount** (up to the balance; write off part to leave the rest to collect), the **reason** (Discount, Hardship, Staff or family, Billing error, Other) and a **note** (required for Other). The server re-checks the balance under the same per-patient lock as Collect. **2. What a write-off is:** the visit counts as **covered** for that amount (P6a's rule), but it is **never revenue**. It isn't in collected totals, the payment report's revenue, "Fully paid" or clinic/source amounts, and its clinic net is 0. In the patient's payments it shows as **Written off** with its reason. It's recorded in the payment history (Audit History) and in the audit log as `Payment.WrittenOff`, with amount, reason and visit. **3. Undo write-off** (same permission): the ↺ button on a written-off row, with a reason. The write-off becomes a void and **the amount is due again**. It's audited as `Payment.WriteOffCancelled`. **4. Payment dashboard:** a new **Written off (not revenue)** card shows the period's total and the amount **by reason**. Write-offs converted from old cancelled payments (P6a) appear as "Before P6 (cancelled payment)". **5. Collect** never accepts the write-off method. **6. Clinic Admin group** ("Clinic managers", built in). It has everything Reception supervisors have (payments, collect, adjust, reports for all doctors, all doctors' calendars), plus **write-offs**, the **full payment dashboard**, **reports for any date** and **clinic settings** (clinics, agreements and opening hours). It has no users, permission groups or medical records. Add people to it in **Permission groups**. **Reception supervisors** gain **write-offs**. **Admin** has everything, as before |
| Scope | Write-offs are for **visits** (reservations). Nutrition enrollment balances still work as before (collect only) |
| Database | **One migration**, `WriteOffReasons`, adds `Payments.WriteOffReason` (text, up to 20, nullable). There are no data changes. At start-up the seeder creates the **Clinic managers** group and grants `Payments.WriteOff` to Reception supervisors once, the same way new permissions were added before. Backup and **YES** approval as usual |
| Tests | About **+13 .NET** and **4 lines** in the access baseline. The tests cover: writing off the rest covers the visit and isn't revenue; it's audited and kept in the payment history, and a partial write-off leaves the rest due; five invalid write-offs are refused and change nothing (no reason, unknown reason, Other without a note, more than the balance, zero); no write-off without a balance; Collect refuses the write-off method; undo makes the amount due again, is audited and can't be done twice; the dashboard shows write-offs by reason apart from collected money. The permission table and the access matrix include **Clinic Admin** (`ClinicAdmin` is added to the matrix lines it can open, plus `Payments.WriteOff` and `Payments.CancelWriteOff`) |

## Files

| Action | Path |
|---|---|
| Add | `Domain/Enums/WriteOffReason.cs`, `Views/Payments/WriteOff.cshtml`, `Views/Payments/CancelWriteOff.cshtml`, `tests/.../Application/WriteOffTests.cs` |
| Edit | `Domain/Entities/Payment/Payment.cs` (`WriteOffReason`), `Domain/Enums/PaymentMethodEnum.cs` (+`WriteOff`), `Infrastructure/Data/Configurations/PaymentConfiguration.cs` |
| Generated | `Infrastructure/Migrations/*_WriteOffReasons.cs` (+ Designer and snapshot), by the script |
| Edit | `Application/Security/PermissionCatalogue.cs` (`Payments.WriteOff`; Clinic managers; supervisors), `Common/Auditing/Audit.cs`, `DTOs/Payment/PaymentDtos.cs`, `Interfaces/Services/IPaymentService.cs`, `Services/PaymentService.cs` (`WriteOffAsync`, `CancelWriteOffAsync`, dashboard, Collect guard) |
| Edit | `Web/Controllers/PaymentsController.cs` (`WriteOff`, `CancelWriteOff`), `Views/Payments/PatientSummary.cshtml`, `Collect.cshtml`, `Dashboard.cshtml`, `Audit.cshtml` |
| Edit | tests: `PermissionCatalogueTests.cs`, `AccessMatrixTests.cs`, `access-matrix.txt` |

## Rollback

Revert the PR's merge commit, and roll the database back to the previous migration (drops the column; write-offs keep their status but lose their category) or restore the script's backup. The Clinic managers group can be deleted in Permission groups.

---

## Your steps

After merging **PR #52**, from `D:\MyDevWork\clinic-MS`:

```powershell
.\.assessment\slice.ps1 -Branch feature/p6b-write-offs -Migration WriteOffReasons `
  -Expect 'AddColumn WriteOffReason' -Require 'maxLength: 20' `
  -Message 'P6b: write off a visit balance with a reason (audited, not revenue, undoable); Clinic Admin group; supervisors can write off'
```

### Manual check (while the app runs)

Use a **test patient** with a **finished visit priced 800**.

1. **As Reception** (front desk): collect **500**. The patient's payment summary shows a balance of **300** with **Collect**, but **no Write off** button (the front desk can't write off).
2. **As a Reception supervisor or Admin:** the same row now has **Write off**. Open it: it says the balance is 300. Try **Other** with no note: refused. Try **301**: refused.
3. Write off **100**, reason **Discount**. Back on the summary, the balance is **200**. The payments list shows a yellow **Written off** row with "Discount", and its **Audit History** shows "Written off".
4. **Collect → Write off instead** opens the same page for the remaining 200. Write it off as **Hardship**. The visit is now **Covered** on Appointments.
5. **Payment dashboard:** **Written off (not revenue)** shows 300 (Discount 100, Hardship 200). **Fully paid** and the clinic and source amounts count only the 500 collected.
6. **Undo:** on the Hardship row, click ↺, give a reason and confirm. The balance is **200** again, and the visit is **Not fully covered**.
7. **Clinic Admin:** Admin → **Permission groups**. **Clinic managers** is there. Add a test user to it, sign in as them, and check: Payments, the full dashboard and the report (any dates) work; Clinics and opening hours open; Patients and medical records don't appear in the menu.
8. **Audit (optional, read-only):** `SELECT TOP 5 Action, EntityId, Details FROM AuditLogs WHERE Action LIKE 'Payment.WrittenOff%' OR Action = 'Payment.WriteOffCancelled' ORDER BY Id DESC;`

When you've finished, press Enter in the script, then type `YES` to commit and push. Open the PR and send me its number and the CI result.

## Where we are after this

**P1–P6 are complete.** Next is **P7, pilot acceptance**:
- an end-to-end browser test of the whole flow, with an accessibility check on each screen;
- checks at 1366 / 1024 / 390 px;
- workflow measurements;
- a short usability check with 2–3 staff.
