# Slice F7b — Top bar: patient search and account menu

Roadmap item **F7** (app shell), slice b of 3 (Group 2).

**Implemented by Claude at the owner's request.** The owner builds, tests, checks and opens the PR.

| | |
|---|---|
| Branch | `chore/f7b-top-bar` (from the working copy after PR #28 is merged) |
| Behaviour change | 1. **Find a patient from any page.** The top bar has a search box ("Find patient: name or phone") for everyone who can view patients. Pressing Enter opens the Patients list with that search, and phone numbers in any format match (F5b). 2. **Account menu** at the top right: the user's initial and name, **Change password**, **Sign out**. The sign-out block at the bottom of the sidebar is removed. 3. The page title in the top bar is plain text instead of an out-of-order `h6` heading. Pages get their single `h1` in F7c. 4. The date is shortened and shown only on wide screens |
| Not included | The language switch. It comes with the localisation work (F9), so the top bar doesn't show a control that does nothing yet |
| Database | None |
| Tests | No new tests (layout only); still **224** |

## Files

| Action | Path |
|---|---|
| Edit | `SafyaClinic.Web/Views/Shared/_Layout.cshtml`: new top bar; sidebar footer removed |
| Edit | `SafyaClinic.Web/wwwroot/css/site.css`: top-bar title, search width, avatar size (token-based) |

## Rollback

Revert the PR's merge commit.

---

## Your steps

Run these in PowerShell from `D:\MyDevWork\clinic-MS`.

### Step 1 — Branch

Merge **PR #28** first. The changes are in your working copy (currently on `fix/landing-header`) and move with you:

```powershell
git switch -c chore/f7b-top-bar
git status
```

You should see only `_Layout.cshtml` and `site.css` modified.

### Step 2 — Build and test

```powershell
dotnet build SafyaClinic\SafyaClinic.sln
dotnet test SafyaClinic\SafyaClinic.sln
```

Expected: **224 passed**.

### Step 3 — Check

```powershell
dotnet run --project SafyaClinic\SafyaClinic.Web --launch-profile http
```

1. **As Reception:**
   - On the Dashboard, type part of a patient's name in the top bar and press Enter. The Patients list opens with the results.
   - Try a phone number with spaces or `+20`. The patient is still found.
2. **As mm (ReceptionAdmin):** there's no search box, because mm can't open patients. The account menu still works.
3. **Account menu:** click your name.
   - **Change password** opens the change-password page.
   - **Sign out** signs you out.
4. **Narrow window** (phone width): ☰, the search box and the avatar fit on one line, and the menu opens.
5. **The sidebar** no longer has the name and Logout block at the bottom.

### Step 4 — Commit, push, PR

```powershell
git add -A SafyaClinic Docs
git status
git commit -m "F7b: top bar with patient search and account menu (change password, sign out)"
git push -u origin chore/f7b-top-bar
```

Send me the PR number and the CI result.

## Next: F7c

1. One page-header pattern: a single `h1`, optional breadcrumb and actions.
2. Messages shown as toasts in a live region.
3. A shared confirm dialog replacing the browser's `confirm()` in shared flows.
4. Validation summaries that render nothing when the form is valid (no empty red boxes).
