# Slice F4d-2 — Roles retired: users belong to groups

Roadmap item **F4**, slice d, part 2 (Group 2). Completes decision **D1** (permission groups instead of fixed roles).

**Implemented by Claude at the owner's request.** The owner builds, tests, runs the checks and opens the PR.

| | |
|---|---|
| Branch | `chore/f4d2-retire-roles` (from the working copy after PR #23 is merged; see Step 1) |
| Behaviour change | 1. **Access comes only from group membership.** Roles are no longer read. 2. **Users screen:** a "Groups" column (add with ⊕, remove with ×) replaces "Roles", and a **Clinician** dropdown (None / Doctor / Nutritionist) sets who appears in doctor pickers. Changing it signs that user out once, because the type is in their sign-in cookie. 3. **Add User:** choose a clinician type and permission groups instead of roles. 4. **Permission Groups:** built-in groups now take members directly too. 5. **Last administrator:** can't be removed from Administrators or deactivated. 6. **Roles screen removed.** |
| Database | ⚠ No schema change. **Data:** on the first start, the seeder copies each user's roles into memberships of the matching built-in groups. It does this **once** and never again, so later changes on the groups screen are kept. The `Roles` and `UserRoles` tables stay for now and are dropped in a later slice |
| Same access | Everyone ends up in the groups matching their roles, so nobody's access changes. `access-matrix.txt` changes only for the Users and Roles screen actions |
| Tests | 193 (190 − 1 + 4: role tests replaced by group, clinician-type and last-admin tests) |

## Design

| Decision | Choice |
|---|---|
| One-time move | `DbSeeder.MoveRoleMembershipsAsync` only runs while no built-in group has any member. On a fresh database, the first administrator is put straight into Administrators, so there is nothing to move |
| Clinician type | Set directly by an admin and audited (`User.ClinicianTypeChanged`). The user's stamp is renewed because the type is in the cookie |
| Last administrator | "Active members of Administrators". Checked when deactivating a user and when removing a member of Administrators |
| Users screen | Group changes go through the same service as the Permission Groups screen (audited, no sign-out, apply within a minute) |
| Sign-in | The role claims are gone; nothing reads them any more |

## Files

| Action | Path |
|---|---|
| Edit | `Application/DTOs/Patient/PatientDtos.cs` (`CreateUserRequest`: `ClinicianType`, `GroupIds`; `UserDto`: `Groups`, `ClinicianType`) |
| Edit | `Application/DTOs/Auth/AuthDtos.cs`, `Services/AuthService.cs` (no roles) |
| Edit | `Application/Interfaces/Services/IUserService.cs`, `Services/UserService.cs` (`SetClinicianTypeAsync`; last-admin guard by group; role methods removed) |
| Edit | `Application/Common/Auditing/Audit.cs`, `Application/Security/IPermissionGroupService.cs` |
| Edit | `Infrastructure/Security/PermissionStore.cs` (groups only), `PermissionGroupService.cs` (built-in members; last-admin guard) |
| Edit | `Infrastructure/Data/DbSeeder.cs` (first admin in Administrators; one-time move) |
| Edit | `Web/Controllers/AuthController.cs`, `UsersController.cs`; `Views/Users/Index.cshtml`, `Create.cshtml`; `Views/PermissionGroups/Edit.cshtml`; `Views/Shared/_Layout.cshtml` |
| **Delete (you, Step 1)** | `Web/Controllers/RolesController.cs`, `Web/Views/Roles/Index.cshtml`, `Web/Models/RoleLookupViewModel.cs` |
| Tests | `TestData.cs` (new helper); `UserServiceAdminGuardTests`, `ClinicianTypeTests`, `AuditTrailTests`, `AuthServiceLoginTests`, `DbSeederTests`, `PermissionStoreTests`, `PermissionGroupServiceTests`, `ReservationServiceRulesTests`; `access-matrix.txt` |

## Rollback

Revert the PR's merge commit. The roles are still in `UserRoles`, so the previous code works as before. The group memberships created by the move stay and do no harm.

---

## Your steps

Run these in PowerShell from `D:\MyDevWork\clinic-MS`.

### Step 1 — Branch and delete the Roles screen

First merge **PR #23** on GitHub. Then:

```powershell
git switch -c chore/f4d2-retire-roles
git rm SafyaClinic/SafyaClinic.Web/Controllers/RolesController.cs SafyaClinic/SafyaClinic.Web/Views/Roles/Index.cshtml SafyaClinic/SafyaClinic.Web/Models/RoleLookupViewModel.cs
```

### Step 2 — Build and test

```powershell
dotnet build SafyaClinic\SafyaClinic.sln
dotnet test SafyaClinic\SafyaClinic.sln
```

Expected: **Passed: 193**, Failed: 0. Send me any errors.

### Step 3 — Back up, then run (your approval of the data change)

```powershell
powershell -ExecutionPolicy Bypass -File .assessment\backup-db.ps1
dotnet run --project SafyaClinic\SafyaClinic.Web --launch-profile http
```

Starting the app is your **explicit approval** for one data change on the **dev** database: each user's roles are copied into `UserGroups` (Admin → Administrators, Doctor → Doctors, and so on). Nothing is deleted.

Check it worked (read-only):

```powershell
$cfg = Get-Content SafyaClinic\SafyaClinic.Web\appsettings.json -Raw | ConvertFrom-Json
$conn = New-Object System.Data.SqlClient.SqlConnection $cfg.ConnectionStrings.SafyaClinicDb
$conn.Open(); $cmd = $conn.CreateCommand()
$cmd.CommandText = "SELECT g.Name, COUNT(m.UserId) AS Members FROM PermissionGroups g LEFT JOIN UserGroups m ON m.GroupId = g.Id GROUP BY g.Name ORDER BY g.Name"
$t = New-Object System.Data.DataTable; $t.Load($cmd.ExecuteReader()); $t | Format-Table -AutoSize; $conn.Close()
```

Expected, from your Step 1 report in F4a: Administrators 1, Doctors 2, Nutritionists 2, Reception 3, Reception supervisors 1.

### Step 4 — Manual check

1. **Everyone signs in as before** and sees exactly the same menus. Check one user per group, including mm and Enas.
2. **Admin → Users:**
   - the **Groups** column shows each user's groups;
   - the **Clinician** column shows Doctor for Enas and the other doctor, Nutritionist for the second nutritionist, and None for everyone else;
   - **Roles** is gone from System Lookups.
3. **Add User** with clinician type **Doctor** and group **Doctors**. The new user appears in Reservations → New → doctor list and can open Medical Records.
4. **Change a test user's clinician type** to None. They disappear from the doctor list, and if they were signed in, they're asked to sign in again.
5. **Permission Groups → Administrators:** try to **Remove** yourself while you're the only administrator. You're refused.
6. **Users:** add a test user to **Reception** with ⊕. Within a minute they get the queue and check-in. Remove them with ×.

### Step 5 — Commit, push, PR

```powershell
git add -A SafyaClinic Docs
git status
git commit -m "F4d-2: roles retired: users belong to permission groups; clinician type set by admin; Roles screen removed"
git push -u origin chore/f4d2-retire-roles
```

Send me the PR number and the CI result.

## Acceptance criteria

- [ ] Access comes only from group membership; nobody's access changed after the one-time move (Step 4.1).
- [ ] Admins manage groups and clinician type on the Users screen; the Roles screen is gone.
- [ ] The last active administrator can't be removed or deactivated.
- [ ] 193 tests pass locally and in CI.

## After this

- **F4d-3 (small):** a reviewed migration drops `Roles` and `UserRoles` and removes the role seeding. Do it after this has run on dev for a while.
- **Then Group 2 continues with F6** (design tokens and local assets).
