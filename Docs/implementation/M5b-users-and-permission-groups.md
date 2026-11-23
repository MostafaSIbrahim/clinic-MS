# Slice M5b: users and permission groups

Group 4, item **M5** (settings), slice b. Follows the approved plan (`M5-settings-plan.md`): shared layout and Arabic, **rules unchanged** (F4).

**Claude implemented this slice at the owner's request.** The owner runs one command, does the manual check, and pushes.

| | |
|---|---|
| Branch | `feature/m5b-users-groups` (from `main` after PR #77; your folder is on `main` with the changes) |
| Database | None |

## Behaviour change

**1. Users list:**
- the shared layout and Arabic;
- **a search** by name, phone or email, which is new;
- a table that becomes cards on phones;
- **pages** for long lists. Before, only the first page was ever shown.

Each user's groups appear as small chips, and the remove button now has a name: before, it was a bare "×". *Add to a group* is a labelled list instead of an unlabelled "+" menu. Clinician types are translated. Deactivating a user now asks first.

**2. Add user and Reset password:** shared fields and Arabic. The hints are kept: at least 10 characters, the licence number is printed on prescriptions, and doctors and nutritionists appear in doctor lists.

**3. Permission groups:**
- the list, New group and the group page are in the shared layout and Arabic;
- **every permission, area and built-in group name is shown in Arabic**, with the permission's code kept beside it;
- removing a member names them.

**Unchanged:**
- password rules;
- built-in group names are fixed;
- Administrators always have every permission, and the last active admin is protected;
- changing a clinician type signs the user out;
- who can manage users: *Users.Manage*.

## Tests

- **+2:** every permission and area, and every built-in group's name and description, has an Arabic translation.
- **The browser test:**
  - it creates its test users through the new Add user form;
  - it checks Users, Add user, Permission groups and a group page for accessibility at the three widths.

## Files

| Action | Path |
|---|---|
| Add | `tests/.../Web/PermissionTranslationTests.cs` |
| Edit | `Web/Views/Users/Index.cshtml`, `Create.cshtml`, `ResetPassword.cshtml`, `Web/Views/PermissionGroups/Index.cshtml`, `Create.cshtml`, `Edit.cshtml` (rewritten) |
| Edit | `Application/Services/UserService.cs` (search), `Web/Controllers/UsersController.cs`, `wwwroot/css/site.css`, `Resources/SharedResource.ar.resx` (+116, including the permission catalogue), `e2e/tests/pilot-flow.spec.js` |

## Rollback

Revert the PR's merge commit.

---

## Your steps

In PowerShell:

```powershell
.\.assessment\slice.ps1 -Branch feature/m5b-users-groups `
  -Message 'M5b: users and permission groups in the shared layout and Arabic; user search and pages; permissions translated'
```

### Manual check (while the app runs)

As **Admin**:

1. **Users**: search for part of a name, then a phone number. The table shows groups as chips, the clinician type and the last sign-in.
2. Add a test user to a group with *Add to a group*, then remove them with the button on the chip: it asks first.
3. **Deactivate** the test user: it asks first, and the status shows *Inactive*. **Activate** again.
4. **Add user**: the fields have labels and hints. A password under 10 characters is refused, as before.
5. **Reset password** for the test user works as before.
6. **Permission groups**:
   - open *Reception*: the permissions are listed by area, each with its code;
   - tick one more and save, then untick it again;
   - a built-in group's name can't be edited.
7. **Arabic:** both screens are in Arabic, including every permission and the built-in group names. At phone width, the tables become cards.

When you've finished, press Enter in the script, then type `YES` to commit and push. Open the PR, merge it, run `git switch main` and `git pull`, and send me its number and the CI result.

## Next: M5c

Clinics, agreements and patient sources in the shared layout and Arabic. The *Lookups* home becomes a grouped **Settings** page.
