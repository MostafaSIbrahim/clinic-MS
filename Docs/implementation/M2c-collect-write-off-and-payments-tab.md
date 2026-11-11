# Slice M2c: collect, write off, adjustments and the Payments tab

Group 4, item **M2** (billing), slice c. Follows the approved plan (`M2-billing-plan.md`).

**Claude implemented this slice at the owner's request.** The owner runs one command, does the manual check, and pushes.

| | |
|---|---|
| Branch | `feature/m2c-payment-forms` (from `main` after PR #63; your folder is on it) |
| Behaviour change | **1. The patient's Payments tab is in Arabic** (it was the last English part of the patient workspace). It uses the shared cards and tables (cards on phones). Each action is a labelled button: **History**, **Change amount**, **Cancel payment**, **Undo write-off**, not icon-only buttons. **2. Payment times on the tab are clinic time.** Before, they showed the stored UTC time, two hours early. **3. The five payment forms** (Collect, Write off, Change amount, Cancel payment, Undo write-off) use the shared form field, show the **patient header** (name, phone, allergies), and are in Arabic. Each **Back to payments** returns to the patient's Payments tab. Cancel and Change amount used to return to the payments report. **4. Fix:** if a change of amount was refused (e.g. no reason given), the form couldn't be shown again, because the current amount was missing. It now shows again, with the message. **5. The payment history dialog** (audit) is in Arabic. **6.** Amount fields accept piastres (step 0.01) wherever the shared field is used for a number |
| Unchanged | Every payment rule, what each button does, and who sees it. The confirmation messages after saving ("Payment of … collected", and so on) are still English; they're translated with the remaining strings (M7) |
| Database | None |
| Tests | +1 (a refused amount change shows the form again with its payment). The browser test's collect and write-off steps run through the new forms and tab, with accessibility checks at the three widths |

## Files

| Action | Path |
|---|---|
| Edit | `Web/Views/Patients/_PatientPayments.cshtml`, `Web/Views/Payments/Collect.cshtml`, `WriteOff.cshtml`, `ChangeAmount.cshtml`, `Cancel.cshtml`, `CancelWriteOff.cshtml`, `Audit.cshtml` (rewritten) |
| Edit | `Web/Controllers/PaymentsController.cs` (payment on the Cancel and Change amount forms; refused form shown again), `Web/TagHelpers/FieldTagHelper.cs` (step on number fields), `wwwroot/js/payment-audit.js` (texts from the page), `wwwroot/css/site.css` (voided row), `Resources/SharedResource.ar.resx` (+76) |
| Edit | tests: `Web/Payments/PaymentsControllerAccessTests.cs`, `PaymentServiceSpy.cs` |

## Rollback

Revert the PR's merge commit.

---

## Your steps

Your folder is on an up-to-date `main` with the M2c changes. In PowerShell:

```powershell
.\.assessment\slice.ps1 -Branch feature/m2c-payment-forms `
  -Message 'M2c: Payments tab and payment forms in Arabic with the patient header; Back returns to the Payments tab; refused amount change fixed'
```

### Manual check (while the app runs)

Use a test patient with a finished, priced visit.

1. **Payments tab** (as Reception): the four totals, *Ready for collection* with **Collect**, and the history with clinic-time dates (compare one with when you actually collected it).
2. **Collect**: the form shows the patient header. Collect an amount with piastres, e.g. `150.50`: it's accepted, and you're back on the Payments tab with the message.
3. In the history, **History** opens the dialog with the payment's changes.
4. **Change amount**: the form shows the current amount and the patient. Submit **without a reason**: the form comes back with the message. Before, this failed. Then give a reason and save: you're back on the Payments tab.
5. **Cancel payment**: the warning, then a reason, then save. The row is struck through as *Cancelled (void)*, with the reason under it.
6. **As a supervisor**: **Write off** part of a balance (reason *Other* needs a note), then **Undo write-off**. Each form's **Back to payments** returns to the tab.
7. **Arabic:** the tab, the five forms and the history dialog are in Arabic, laid out right to left. At phone width the tables become cards.

When you've finished, press Enter in the script, then type `YES` to commit and push. Open the PR, and send me its number, the CI result and the **performance** table.

## Next: M2d, which closes M2

The payments report: the clinic clock instead of the fixed zone, Arabic, totals by method and doctor, and **Download as CSV**. The two old repair actions are removed.
