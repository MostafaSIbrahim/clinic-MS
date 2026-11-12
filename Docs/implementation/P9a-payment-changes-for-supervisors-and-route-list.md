# Slice P9a: payment changes for supervisors only, and the Route drop-down

From the owner's manual test of 11 Nov 2026 (comments 1 and 4), with the owner's choice of route list.

**Claude implemented this slice at the owner's request.** The owner runs one command, approves the data change, does the manual check, and pushes.

| | |
|---|---|
| Branch | `feature/p9a-adjust-and-route` (from `main` after PR #64; your folder is on it) |
| Behaviour change | **1. The front desk can't change or cancel a collected payment** from any screen. The **Reception** group no longer holds *Cancel payments and change amounts* (`Payments.Adjust`). On the Payments tab, Reception no longer sees **Change amount** or **Cancel payment**, and opening those pages by address is refused. Reception still **collects**, fully or partly. **Reception supervisors**, **Clinic managers** and **Administrators** keep the permission. You can still grant it to any group on the Permission Groups screen. **2. Route is a drop-down** on the new-prescription form and the add-drug row: Oral, Sublingual, Intravenous (IV), Intramuscular (IM), Subcutaneous (SC), Topical, Inhaled, Nasal, Eye, Ear, Rectal, Vaginal, Transdermal, Other route. It can be left empty. The server refuses a route that isn't in the list. **Routes typed on older prescriptions are kept**, and still shown and printed as typed. The list is in Arabic on screen |
| Database | **One data-only migration**, `ReceptionCannotAdjustPayments`: removes `Payments.Adjust` from the built-in Reception group. **No schema change.** The route is stored as before (text), now the route's name. The script backs up the database first and asks for **YES** |
| Down (rollback) | Gives the permission back to the Reception group |
| Tests | +11: the route list matches the enum, only listed routes or none are accepted, old typed routes show as typed. The group permission table and the access baseline are updated: Change amount and Cancel payment are no longer open to Reception |

## Files

| Action | Path |
|---|---|
| Add | `Domain/Enums/AdministrationRoute.cs`, `Application/Clinical/AdministrationRoutes.cs`, `Infrastructure/Migrations/20261112130000_ReceptionCannotAdjustPayments.cs` (hand-written, data only), `tests/.../Application/AdministrationRoutesTests.cs` |
| Edit | `Application/Security/PermissionCatalogue.cs` (Reception), `Application/Services/PatientRecordService.cs` (route checked on create and add) |
| Edit | `Web/Views/MedicalRecords/CreatePrescription.cshtml`, `PrescriptionDetails.cshtml`, `PrintPrescription.cshtml`, `Web/Views/Patients/_PatientPayments.cshtml` (adjust buttons need the permission), `Resources/SharedResource.ar.resx` (+15) |
| Edit | tests: `PermissionCatalogueTests.cs`, `Security/access-matrix.txt`, `Payments/PaymentsControllerAccessTests.cs` |

## Rollback

Revert the PR's merge commit, then roll the database back to the previous migration (the `Down` above), or restore the backup the script took.

## On the live site

The next publish needs this migration. Run the idempotent script on the live database first (D2b Step 1), as for P8a.

---

## Your steps

In PowerShell:

```powershell
.\.assessment\slice.ps1 -Branch feature/p9a-adjust-and-route -Migration ReceptionCannotAdjustPayments `
  -Require 'Reception','Payments.Adjust' `
  -Message 'P9a: only supervisors, clinic managers and admin change or cancel collected payments; prescription route from a list'
```

The script should show `Up contains:` with nothing listed (plain SQL), then **Migration matches the slice doc**. Type **YES** to approve the data change.

### Manual check (while the app runs)

1. **As Reception**, a patient's **Payments** tab: collected payments show **History** only. There's no Change amount or Cancel payment. **Collect** still works for a balance, fully or partly.
2. Still as Reception, open `/Payments/ChangeAmount?paymentId=<an id>` by address: access is refused.
3. **As a supervisor** (or clinic manager), the same tab shows **Change amount** and **Cancel payment**, and both work.
4. **As a doctor**, **New prescription**: Route is a drop-down with the 14 routes. Save one drug with *Intravenous (IV)* and one with no route. The details page shows *Intravenous (IV)* and "—". The printout shows the route.
5. On that prescription's page, **Add Another Drug** has the same drop-down.
6. An **older prescription** with a typed route (e.g. "after meals") still shows it as typed.
7. **Arabic:** the route list is in Arabic on the forms.

When you've finished, press Enter in the script, then type `YES` to commit and push. Open the PR, merge it, run `git switch main` and `git pull`, and send me its number and the CI result.

## Next: P9b

Cancelled and No-show reservations can only be reopened by supervisors (new permission). Reservations not checked in within 24 hours of their appointment become No show automatically.
