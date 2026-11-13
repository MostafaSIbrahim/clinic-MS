# Slice M4a: enrollment status rules and screens

Group 4, item **M4** (nutrition), slice a. Follows the approved plan (`M4-nutrition-plan.md`, approved 13 Nov 2026 with the recommended choices):
1. Nothing more is owed on a cancelled enrollment.
2. Completed and Cancelled enrollments are final.
3. The delete rule for weeks comes in M4b.

**Claude implemented this slice at the owner's request.** The owner runs one command, does the manual check, and pushes.

| | |
|---|---|
| Branch | `feature/m4a-enrollments` (from `main` after PR #70) |
| Database | None |

## Behaviour change

**1. Status rules, enforced by the server** (fixes N2):
- **Active** → *Put on hold* or *Cancel enrollment*.
- **On hold** → *Resume* or *Cancel enrollment*.
- **Completed** is only set automatically, when the last week's follow-up is recorded.
- **Completed** and **Cancelled** are final: to continue, enroll the patient again.
- Resuming is refused while the patient has another active enrollment.
- A refused change now shows why; before, nothing was said.
- Every change is in the audit log as *Enrollment.StatusChanged*, with the old and new status.

**2. Nothing more is owed on a cancelled enrollment** (N9, decision 1):
- Its unpaid part no longer appears on the patient's **Payments** tab: not in *Waiting for payment*, not in the balance.
- **Collect** refuses it.
- Money already paid stays recorded and counted as paid.
- Cancelling asks for confirmation and says this.

The payment dashboard and Unpaid list never included enrollments, so they're unchanged.

**3. The enrollment page:**
- the shared layout, the patient header, status badges and Arabic;
- the price, discount, paid and **amount due**;
- the four weeks side by side, with *Not recorded yet* for the weeks still to come;
- only the status buttons the rules allow.

Record, Edit, Delete and the status buttons appear only for people who manage nutrition. **Collect** appears only for people who can collect (fixes N8).

**4. Enroll** (fixes N7):
- the shared fields, the patient header and Arabic;
- the price preview is in **EGP**; it showed US dollars;
- if the server refuses (for example, a discount above the package's maximum), the package and doctor lists stay filled in; they came back empty;
- the start date defaults to the **clinic's** date;
- typing is kept across a language switch (as in M3b).

**5. Fix: the Collect amount sometimes couldn't be saved** (from the owner's test, 13 Nov 2026).
- A package price with a discount can work out to more than two decimals. For example, 850.00 with 12% gives 748.0000, and 333.33 with 12% gives 293.3304. The amount box then showed four decimals.
- The browser check allows two decimals, so it refused to save until the amount was retyped. This was most visible after the form came back with *choose a clinic*.
- The amount due on an enrollment is now rounded to whole piastres: on Collect and on the Payments tab.
- Every number box shows as many decimals as it allows: two for money.
- The Enroll discount box now also takes two decimals, as the package allows.

## Unchanged

- Who can enroll.
- The maximum discount.
- One active enrollment per patient.
- Recording follow-ups, which is fixed in M4b.
- Packages.

## Tests

There are 23 new test cases:
- **Every status change, allowed or refused (11 cases).** The buttons offered are exactly the allowed changes.
- **The service, in memory.** It covers:
  - putting on hold and resuming, saved and audited;
  - six refused changes, which change nothing, write no audit entry and give a reason;
  - resuming refused while another enrollment is active;
  - a cancelled enrollment owes nothing: Collect sees 0, it isn't waiting for payment, and only the paid part is charged;
  - an active one still owes its balance;
  - the amount due has at most two decimals (293.3304 becomes 293.33), on Collect and on the Payments tab.
- **Browser test:** the Enroll form is checked for accessibility at the three widths.

## Files

| Action | Path |
|---|---|
| Add | `Application/Services/EnrollmentRules.cs`, `tests/.../Application/EnrollmentRulesTests.cs` |
| Edit | `Application/Services/NutritionService.cs` (rules, audit), `PaymentService.cs` (cancelled enrollment owes nothing), `DTOs/Nutrition/PatientEnrollmentDto.cs` (`AmountDue`), `Common/Auditing/Audit.cs` |
| Edit | `Web/Controllers/NutritionController.cs` (clinic date, lists kept, result shown), `Views/Nutrition/Enroll.cshtml`, `EnrollmentDetails.cshtml` (rewritten), `Components/StatusStyles.cs` (Active, On hold), `wwwroot/css/site.css`, `Resources/SharedResource.ar.resx` (+26) |
| Edit | `e2e/tests/pilot-flow.spec.js` |

## Rollback

Revert the PR's merge commit.

---

## Your steps

Your folder is still on the M3c branch, and the M4a changes are in it. First, in PowerShell (the changes stay in the folder):

```powershell
git switch main; git pull
```

Then:

```powershell
.\.assessment\slice.ps1 -Branch feature/m4a-enrollments `
  -Message 'M4a: enrollment status rules enforced and audited; nothing owed on a cancelled enrollment; enrollment and Enroll screens in the shared layout and Arabic'
```

### Manual check (while the app runs)

As **Admin**, with a **test patient**. If no package exists, create one under **Nutrition → New package**.

1. Patient → **Nutrition** tab → **Enroll in a package**. You see the patient header. Choose a package and type a discount: the final price shows in **EGP**.
2. Type a discount above the package's maximum and select **Enroll**. The error shows, and the package and doctor lists are still filled in.
3. Enroll with a valid discount. The enrollment page shows the price, discount, paid and amount due, and four weeks marked *Not recorded yet*.
4. **Put on hold**: the status is *On hold*, and there's only **Resume** and **Cancel enrollment**. **Resume**: Active again.
5. On the patient's **Payments** tab, the enrollment's amount is waiting for payment. Collect part of it.
6. Back on the enrollment, select **Cancel enrollment**. It warns that the unpaid balance won't be owed. Confirm.
   - The page says it's cancelled.
   - The amount due is 0.00 EGP, with no status buttons.
   - On the Payments tab, the enrollment is no longer waiting for payment, and the part you collected is still listed.
7. **Collect amount:** enroll in a package with a discount that leaves odd piastres (for example 12% on a price ending in .33). Select **Collect**: the amount has two decimals. Leave the clinic empty and save. The clinic error shows, the amount still has two decimals, and choosing a clinic then saves it.
8. **Arabic:** both screens are in Arabic.
9. Optional: check the audit log:
   ```sql
   SELECT TOP 5 OccurredAtUtc, Action, EntityId, Details FROM AuditLogs WHERE Action = 'Enrollment.StatusChanged' ORDER BY Id DESC;
   ```

When you've finished, press Enter in the script, then type `YES` to commit and push. Open the PR, merge it, run `git switch main` and `git pull`, and send me its number and the CI result.

## Next: M4b, weekly follow-ups

- Edit really edits.
- Items are chosen from the package, not typed as numbers.
- Only the latest week can be deleted.
