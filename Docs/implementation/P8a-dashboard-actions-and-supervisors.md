# Slice P8a: dashboard actions, and supervisors who work the front desk

A follow-up to the pilot, from the owner's manual test of 10 Nov 2026 (comments 1, 2 and 4). It also follows the owner's choice: **supervisors and clinic managers include the front desk**.

**Claude implemented this slice at the owner's request.** The owner runs one command, approves the data change, does the manual check, and pushes.

| | |
|---|---|
| Branch | `feature/p8a-dashboard-actions` (from an up-to-date `main`) |
| Behaviour change | **1. Today's list on the dashboard has actions.** Each row has **Check in** (for people who may check in, before the patient has arrived), **Collect** (once there's a balance to collect) and **Open** (the reservation). The patient's name opens the reservation too. After check-in you stay on the dashboard, and the row shows **Waiting**. **2. Shortcuts for each task**, each shown only to people allowed to use it: **Book appointment** (the Appointments screen, not the old full form), **Register patient**, **Find patient**, the queue, and payments. **3. Supervisors and clinic managers work the front desk.** The **Reception supervisors** and **Clinic managers** groups now hold everything **Reception** has (patients, reservations, check-in, queues, follow-ups, drug search), on top of their own permissions. One group is enough per person. **4. Money figures are for people who may see payments.** *Today's revenue* and *Balance due today* are hidden from doctors and nutritionists; before, every signed-in user saw the day's revenue. Today's list needs *view reservations*. **5.** The dashboard uses the shared status badges, the table that stacks on phones, and Arabic text |
| Database | **One data-only migration**, `SupervisorsIncludeFrontDesk`. It adds the 12 front-desk permissions to the two built-in groups in existing databases. On a new database it does nothing, because the groups are created at start-up with the full set. It updates the supervisors' group description if nobody has changed it. **No schema change.** The script backs up the database first and asks for **YES** |
| Down (rollback) | Removes those 12 permissions from the two groups, and restores the old description |
| Tests | +4 (the dashboard shows the list and money figures only to people who may see them; revenue isn't queried for others). The access baseline gains supervisors and clinic managers on the 36 front-desk pages. The group permission table and the supervisors' menu are updated to match. The browser test checks in **from the dashboard**, checks the dashboard for accessibility at all three widths, and checks that a supervisor sees today's patients and **Book appointment** |
| Metric | Patient arrives → checked in: **1 click** from the dashboard (was 2: Appointments → Check in) |

## Files

| Action | Path |
|---|---|
| Edit | `Web/Views/Dashboard/Index.cshtml` (rewritten), `Web/Controllers/DashboardController.cs`, `Web/Models/DashboardViewModel.cs` |
| Edit | `Application/DTOs/Reservation/ReservationDtos.cs` (+ queue status on the summary), `Application/Services/ReservationService.cs` (today's list fills it) |
| Edit | `Application/Security/PermissionCatalogue.cs` (supervisors and clinic managers) |
| Add | `Infrastructure/Migrations/20261110160000_SupervisorsIncludeFrontDesk.cs` (hand-written, data only) |
| Edit | `Web/Resources/SharedResource.ar.resx` (+11 strings) |
| Edit | tests: `DashboardControllerTests.cs`, `PermissionCatalogueTests.cs`, `NavigationCatalogueTests.cs`, `Security/access-matrix.txt`; `e2e/tests/pilot-flow.spec.js` |

## Rollback

Revert the PR's merge commit, then roll the database back to the previous migration (the `Down` above), or restore the backup the script took.

## On the live site

The next publish needs this migration. Generate the idempotent script as in D2b Step 1, and run it on the live database before publishing. Only this migration will run; the others are already recorded.

---

## Your steps

**Optional, read-only, before running:** see what the two groups hold now.

```sql
SELECT g.SystemKey, COUNT(*) AS Permissions
FROM PermissionGroups g JOIN GroupPermissions gp ON gp.GroupId = g.Id
GROUP BY g.SystemKey ORDER BY g.SystemKey;
```

From `D:\MyDevWork\clinic-MS`:

```powershell
git switch main
git pull
.\.assessment\slice.ps1 -Branch feature/p8a-dashboard-actions -Migration SupervisorsIncludeFrontDesk `
  -Require 'ReceptionAdmin','ClinicAdmin','Reservations.CheckIn' `
  -Message 'P8a: dashboard check-in, collect and open; task shortcuts; supervisors and clinic managers include the front desk'
```

The script should show `Up contains:` with nothing listed (plain SQL), then **Migration matches the slice doc**. Type **YES** to approve the data change.

### Manual check (while the app runs)

Use test patients. Book one for today first, with a priced visit.

1. **As Reception**, on the dashboard:
   - the shortcuts are Book appointment, Register patient, Find patient, Doctor Queues and Unpaid Payments;
   - today's row has **Check in** and **Open**;
   - click **Check in**. You stay on the dashboard, and the row shows **Waiting**. The Check in button is gone.
2. **Register patient** opens the registration form. **Book appointment** opens Appointments.
3. **As the doctor:** start and complete the consultation. Back **as Reception**, the row now has **Collect**. It opens the Collect form for that visit, with the amount filled in.
4. **As a user in Reception supervisors only** (no Reception group):
   - the menu has Patients, Appointments, Reservations, Doctor Queues and Follow-ups;
   - the dashboard shows today's list with Check in, Collect and Open;
   - Write off still works from the patient's payment summary.
5. **As a doctor**, the dashboard shows only their own patients today, and no revenue or *Balance due today* figures.
6. **Arabic:** switch to العربية. The dashboard's words are in Arabic and the layout is right-to-left. At phone width the table becomes cards.
7. Optional: run the SQL above again. ReceptionAdmin and ClinicAdmin each have 12 more permissions.

When you've finished, press Enter in the script, then type `YES` to commit and push. Open the PR and send me its number and the CI result.

## Next: P8b, drug suggestions

The prescription drug search is replaced with a free, public-domain list of about 25,000 medicines registered in Egypt. It's searched on the server, so it works without an outside service.
