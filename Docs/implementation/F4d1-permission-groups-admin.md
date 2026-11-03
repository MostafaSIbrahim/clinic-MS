# Slice F4d-1 — Permission groups admin screen

Roadmap item **F4**, slice d, part 1 (Group 2). Builds on F4a–F4c: access already comes from groups.

**Implemented by Claude at the owner's request.** The owner builds, tests, runs the manual checks and opens the PR.

| | |
|---|---|
| Branch | `chore/f4d1-permission-groups` (created from the working copy; see Step 1) |
| Behaviour change | New **Administration → System Lookups → Permission Groups** screen (Users.Manage, so Admin). Admins can: **create** a group; **choose its permissions** (grouped by area, with sensitive ones marked); **add and remove members**; **delete** their own groups. Built-in groups: their permissions can be changed, but not their names; their members come from the user's **role** (shown as "via role"); they can't be deleted. **Administrators always keep every permission.** Changes reach the members within a minute. Every change is written to the audit log |
| Not changed | Roles still decide built-in group membership and the clinician type. Retiring roles (moving those memberships into groups, setting the clinician type on the user, removing the Roles screen) is **F4d-2** |
| Database | None (the tables exist since F4a) |
| Tests | +9 (190 in total) |

## Design

| Decision | Choice |
|---|---|
| Who | `PermissionGroupsController` requires `Users.Manage`, the same as Users and Roles |
| Permissions list | Comes from the code catalogue (`PermissionCatalogue`). An unknown code is refused, so admins can only grant what the code enforces |
| Built-in groups | Members follow the role until F4d-2, so direct membership is refused for them. Their names are fixed. Administrators' permissions can't be reduced: the service ignores changes and the seeder restores anything missing |
| When changes apply | Permissions are cached for a minute per user (F4b), so members get a change within a minute. No sign-out is needed |
| Audit | `Group.Created`, `Group.Updated` (with the codes added and removed), `Group.Deleted`, `Group.MemberAdded`, `Group.MemberRemoved` |
| Access list | `access-matrix.txt` gains the 8 new actions, all **Admin**. Nothing else in it changes |

## Files

| Action | Path |
|---|---|
| Add | `SafyaClinic.Application/Security/IPermissionGroupService.cs` (interface + read models) |
| Add | `SafyaClinic.Infrastructure/Security/PermissionGroupService.cs` |
| Edit | `SafyaClinic.Infrastructure/DependencyInjection/InfrastructureServiceExtensions.cs` (registration) |
| Edit | `SafyaClinic.Application/Common/Auditing/Audit.cs` (5 action names) |
| Add | `SafyaClinic.Web/Controllers/PermissionGroupsController.cs`, `Models/PermissionGroupModels.cs` |
| Add | `SafyaClinic.Web/Views/PermissionGroups/Index.cshtml`, `Create.cshtml`, `Edit.cshtml` |
| Edit | `SafyaClinic.Web/Views/Shared/_Layout.cshtml` (menu item) |
| Edit | `tests/SafyaClinic.Tests/Web/Security/access-matrix.txt` (8 lines added) |
| Add | `tests/SafyaClinic.Tests/Infrastructure/PermissionGroupServiceTests.cs` |

## Rollback

Revert the PR's merge commit. Groups or memberships created on the screen stay in the tables but have no effect without the code. Ask me before removing them.

---

## Your steps

Run these in PowerShell from `D:\MyDevWork\clinic-MS`.

### Step 1 — Merge PR #22, then put this work on its own branch

Merge **PR #22** on GitHub first. Then, with the changes still uncommitted in your working copy:

```powershell
git switch -c chore/f4d1-permission-groups
git status
```

The changes move with you to the new branch. Once the branch reaches `main`, the PR shows only F4d-1, because the F4c commit is already there.

### Step 2 — Build and test

```powershell
dotnet build SafyaClinic\SafyaClinic.sln
dotnet test SafyaClinic\SafyaClinic.sln
```

Expected: **Passed: 190**, Failed: 0. If anything fails, send me the output.

### Step 3 — Manual check

```powershell
dotnet run --project SafyaClinic\SafyaClinic.Web --launch-profile http
```

1. **Sign in as Admin.** Administration → System Lookups → **Permission Groups**. You should see five built-in groups with 39 / 17 / 7 / 17 / 5 permissions and their member counts.
2. **Open Administrators.** Every box is ticked and greyed out.
3. **Open Doctors.** The members are shown "via role", and there's no "Add member" form, only a pointer to the Users page.
4. **Create a group** called `Test billing`. On its page, tick **Collect payments** and click **Save group**. Then add a **Doctor** user as a member.
5. In a private window, **sign in as that Doctor**. Within a minute, opening `http://localhost:5070/Payments/Collect?patientId=1` shows the collect form instead of Access denied. The menu won't show a link to it, because the payment menu items use the payment report and dashboard permissions.
6. **Back as Admin**, remove the Doctor from `Test billing`. Within a minute the same URL is denied again.
7. **Delete** `Test billing`.
8. **Audit check (optional):**

   ```sql
   SELECT TOP 10 Action, EntityId, Details FROM AuditLogs WHERE Action LIKE 'Group.%' ORDER BY Id DESC
   ```

   It shows the create, update, member-added, member-removed and delete rows.

The test group doesn't change any real access. If you also try removing a permission from a built-in group, add it back afterwards.

### Step 4 — Commit, push, PR

```powershell
git add -A SafyaClinic Docs
git status
git commit -m "F4d-1: permission groups admin screen (create, grant, members, delete), audited"
git push -u origin chore/f4d1-permission-groups
```

Send me the PR number and the CI result.

## Acceptance criteria

- [ ] Admin can create a group, grant a permission, add a user, and the user gains the action within a minute (Step 3, and a test).
- [ ] Built-in groups: fixed names, members via role, not deletable; Administrators keep every permission.
- [ ] Only catalogue permissions can be granted; every change is audited.
- [ ] `access-matrix.txt` differs only by the 8 new Admin-only lines; 190 tests pass locally and in CI.

## Next: F4d-2 — retire roles

1. A migration copies role memberships into `UserGroups`.
2. Permissions come only from groups.
3. The Users screen assigns groups instead of roles and sets the clinician type directly.
4. The last-Admin guard moves to the Administrators group.
5. The Roles screen goes.
6. A later migration drops `Roles` and `UserRoles`.
