# Slice F4d-3: drop the old Roles and UserRoles tables

Roadmap item **F4** (permissions), slice d-3. This slice **closes F4 and Group 2**.

**Claude implemented the code at the owner's request.** The owner runs the read-only checks, generates and reviews the migration, approves it, then builds, tests and opens the PR.

| | |
|---|---|
| Branch | `chore/f4d3-drop-roles` (from the working copy, after PR #36 is merged) |
| Behaviour change | **None for users.** Since F4d-1/F4d-2, access has come only from permission groups, and nothing reads roles any more. Visible changes: 1. **Lookups page:** the dead **Roles** card, which linked to the Roles screen removed in F4d-2, becomes **Permission Groups**. The Users and Permission Groups cards show only to people who may manage users. 2. The seeder no longer creates roles or copies role memberships into groups. That copy ran once on every existing database in F4d |
| Database | ⚠ **Destructive: drops two tables, `UserRoles` and then `Roles`, with all their rows.** Nothing else changes. The data has no remaining use: memberships were copied to groups in F4d, and the copy is checked in Step 2. **This needs your explicit approval** (Step 4), after a backup |
| Tests | 8 removed (role seeding, the one-time role→group copy, and the role→clinician-type helper) and 2 added. The new tests confirm that the model has no `Roles`/`UserRoles` tables and that seeding twice creates one admin and the built-in groups once. Expect **6 fewer** than after F9c (about **296**) |

## What is removed

| Kind | Item |
|---|---|
| Classes (you remove them in Step 1) | `Domain/Identity/Role.cs`, `Domain/Identity/UserRole.cs`, `Infrastructure/Data/Configurations/RoleConfiguration.cs`, `Infrastructure/Data/Configurations/UserRoleConfiguration.cs`, `Application/Security/ClinicianTypes.cs` (role → clinician type; it was only used once, in F4a's migration) |
| Code (done) | `User.UserRoles`; the `Roles`/`UserRoles` sets on `SafyaDbContext`; `IUnitOfWork.Roles`/`UserRoles`; the seeder's `SeedRolesAsync`, `EnsureReceptionAdminRoleAsync`, `ReceptionAdminRoleName` and `MoveRoleMembershipsAsync`; the dead Roles card |
| Tables (migration, Step 4) | `UserRoles`, `Roles` |

Kept: the built-in groups' fixed keys (`SystemKey` = Admin, Doctor, Nutritionist, Reception, ReceptionAdmin). They still come from `SystemGroups`; the field is still called `RoleName` in code, but only as the key.

## Risks and how they're handled

| Risk | Handling |
|---|---|
| A role membership that never became a group membership is lost | **Step 2** lists any role membership without a matching built-in group membership. If one appears that you didn't remove on purpose on the Permission Groups screen, stop and tell me |
| The data is needed later | **Step 3** backs up the database. Step 2 also shows the role memberships, so you can keep a copy |
| The hosted sites (old code) still have role-only memberships | They are being **freshly deployed** before the pilot, with no real data (your decision). A fresh database never had roles. **Don't migrate the old hosted databases in place** |

## Rollback

- **Code:** revert the PR's merge commit.
- **Database (dev):** `dotnet ef database update AddUserPreferredLanguage --project SafyaClinic\SafyaClinic.Infrastructure --startup-project SafyaClinic\SafyaClinic.Web` recreates **empty** tables. To get the rows back, restore the Step 3 backup. Nothing in the app reads them either way.

---

## Your steps

Run these in PowerShell from `D:\MyDevWork\clinic-MS`.

### Step 1: Branch and remove the role classes

Merge **PR #36** first. Then:

```powershell
git switch -c chore/f4d3-drop-roles
git rm SafyaClinic\SafyaClinic.Domain\Identity\Role.cs SafyaClinic\SafyaClinic.Domain\Identity\UserRole.cs
git rm SafyaClinic\SafyaClinic.Infrastructure\Data\Configurations\RoleConfiguration.cs SafyaClinic\SafyaClinic.Infrastructure\Data\Configurations\UserRoleConfiguration.cs
git rm SafyaClinic\SafyaClinic.Application\Security\ClinicianTypes.cs
dotnet build SafyaClinic\SafyaClinic.sln
dotnet test SafyaClinic\SafyaClinic.sln
```

`git status` should show 11 modified files and 5 deleted ones. Expect about **296 passed**. The tests don't need the migration.

### Step 2: Read-only checks (SSMS, `SafyaClinicDb`)

```sql
-- 2a. What will be dropped
SELECT COUNT(*) AS Roles FROM Roles;
SELECT r.RoleName, COUNT(ur.Id) AS Members
FROM Roles r LEFT JOIN UserRoles ur ON ur.RoleId = r.Id
GROUP BY r.RoleName ORDER BY r.RoleName;

-- 2b. Role memberships with no matching built-in group membership
SELECT u.FullName, r.RoleName
FROM UserRoles ur
JOIN Roles r ON r.Id = ur.RoleId
JOIN Users u ON u.Id = ur.UserId
WHERE NOT EXISTS (SELECT 1 FROM UserGroups ug JOIN PermissionGroups g ON g.Id = ug.GroupId
                  WHERE ug.UserId = ur.UserId AND g.SystemKey = r.RoleName);
```

Expected results:
- **2a** matches what we saw in F4a: Admin 1, Doctor 2, Nutritionist 2, Reception 3, ReceptionAdmin 1, Patient 0.
- **2b** returns **no rows**. The only acceptable rows are for people you deliberately removed from a group afterwards on the Permission Groups screen.

**Send me the 2b result if it isn't empty**, without the names if you prefer.

### Step 3: Generate and review the migration

```powershell
Push-Location SafyaClinic\SafyaClinic.Web
dotnet ef migrations add DropRoles --project ..\SafyaClinic.Infrastructure
Pop-Location
```

Open the new `…_DropRoles.cs`:
- `Up` must contain **only** `DropTable(name: "UserRoles")` and `DropTable(name: "Roles")`.
- `Down` recreates both tables, which is expected and will be empty.

If `Up` touches anything else, **stop and send me the file**.

Then check the model and migrations agree:

```powershell
dotnet run --project SafyaClinic\tests\DeploymentChecks -- D:\MyDevWork\migrations-idempotent.sql
```

It must print `Model changes without a migration: False`.

### Step 4: Back up, then apply (needs your approval)

```powershell
powershell -ExecutionPolicy Bypass -File .assessment\backup-db.ps1
dotnet run --project SafyaClinic\SafyaClinic.Web --launch-profile http
```

**Running these two commands is your explicit approval** for this destructive change to the **dev** database: dropping `UserRoles` and `Roles` with their rows. Only run them once Step 2 looked right and the backup succeeded. In Development the app applies the migration when it starts, as set in S8. If it fails, nothing changes; send me the error.

### Step 5: Check

1. **Tables gone (read-only):**
   ```sql
   SELECT name FROM sys.tables WHERE name IN ('Roles', 'UserRoles');   -- no rows
   SELECT TOP 1 MigrationId FROM __EFMigrationsHistory ORDER BY MigrationId DESC;   -- …_DropRoles
   ```
2. **Sign in as each kind of user** (Admin, a Doctor, a Nutritionist, Reception, mm). Each sees the same menu as before, as in the F7a table.
3. **Admin → Lookups:** the cards are Users and **Permission Groups**, and both open.
4. **Reception → Lookups,** if Reception can open it: there's no Users or Permission Groups card.
5. **Permission Groups:** add someone to a group and remove them again. It still works.

### Step 6: Commit, push, PR

```powershell
git add -A SafyaClinic Docs
git status
git commit -m "F4d-3: drop the old Roles and UserRoles tables and role seeding (access comes from permission groups only)"
git push -u origin chore/f4d3-drop-roles
```

`git status` should include the 5 deletions, the new `…_DropRoles` migration with its `.Designer.cs`, and the changed model snapshot.

Send me the PR number and the CI result.

## Where we are after this

**Group 2 (foundations) is complete:** F2 to F10, including permissions, the design system, the app shell, the components and the two languages. Next is **Group 3, the pilot**: the front-desk visit flow on the new foundations. It starts with **P1, clinic hours and doctor schedules**. Before writing any code, I'll bring you the pilot plan with its open decisions (slot length, booking rules).
