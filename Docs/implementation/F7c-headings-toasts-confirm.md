# Slice F7c — One heading per page, toasts, one confirm dialog, no empty error boxes

Roadmap item **F7** (app shell), slice c of 3 (Group 2). Closes **F7**. Fixes **F29** (empty validation boxes, `confirm()`) and the `page-has-heading-one` part of **F26**.

**Implemented by Claude at the owner's request.** The owner builds, tests, checks and opens the PR.

| | |
|---|---|
| Branch | `chore/f7c-shell-feedback` (from the working copy after PR #29 is merged) |
| Behaviour change | 1. **Messages appear as toasts** at the top right instead of banners above the page. Success closes by itself after 6 seconds; errors and warnings stay until closed. Screen readers announce them. 2. **One confirmation dialog** replaces the browser's grey `confirm()` box in all 19 places: delete, remove, cancel, lock, complete, change clinician type, and so on. It has the same question with **Cancel** and **Yes, continue**. 3. **No empty red boxes.** A validation summary with no errors no longer shows (31 forms had one). 4. **Every page has exactly one `h1`.** Pages that already have their own keep it. For the others (60 of 88 views), the title in the top bar becomes the `h1` |
| How confirmations work now | Add `data-confirm="Question?"` to a form, a submit button or a link. For a select that submits on change, also add `data-confirm-submit`. The script in `site.js` does the rest |
| Database | None |
| Tests | +2 (226): no view uses the browser `confirm()`; the layout provides the toasts, the dialog and the fallback `h1` |

## Files

| Action | Path |
|---|---|
| Add | `Views/Shared/_Toasts.cshtml` (live region), `Views/Shared/_ConfirmDialog.cshtml` (Bootstrap modal) |
| Edit | `Views/Shared/_Layout.cshtml`: toasts and dialog included; the old alert banners removed; the page body is rendered first so the layout can tell whether it has an `h1` |
| Edit | 13 views: `onsubmit/onclick="return confirm('…')"` → `data-confirm="…"`. Users: the clinician-type select uses `data-confirm` + `data-confirm-submit` |
| Edit | `wwwroot/js/site.js`: toast start-up and the confirm logic (forms, submit buttons, links, selects). The unused `confirmDelete()` helper is removed |
| Edit | `wwwroot/css/site.css`: hide valid summaries; toast styles (tokens); top-bar `h1` size |
| Add | `tests/SafyaClinic.Tests/Web/AppShellTests.cs` |

## Rollback

Revert the PR's merge commit.

---

## Your steps

Run these in PowerShell from `D:\MyDevWork\clinic-MS`.

### Step 1 — Branch

Merge **PR #29** first. The changes are in your working copy and move with you:

```powershell
git switch -c chore/f7c-shell-feedback
git status
```

### Step 2 — Build and test

```powershell
dotnet build SafyaClinic\SafyaClinic.sln
dotnet test SafyaClinic\SafyaClinic.sln
```

Expected: **226 passed**.

### Step 3 — Check

```powershell
dotnet run --project SafyaClinic\SafyaClinic.Web --launch-profile http
```

1. **Toasts:**
   - Save a patient's basic details. A green toast appears at the top right and closes itself.
   - Do something that fails, for example removing the primary phone. A red toast stays until you close it.
2. **Confirm dialog:**
   - On a patient page, click **Remove** on a secondary phone. The dialog opens. **Cancel** does nothing; **Yes, continue** removes it.
   - Also try: Users → change a clinician type (Cancel puts the old value back), Users → × on a group, Permission Groups → Delete group, and cancelling a reservation.
3. **No empty red boxes:** open Users → Add User, Patients → New and Permission Groups → New group. There's no empty red box above the form. Submit empty and the errors appear.
4. **Headings:** on the Dashboard or Users page, the title in the top bar is the page heading. On pages with their own big heading (e.g. Permission Groups), the top bar shows the title as plain text.
5. **Keyboard:** Tab to a delete button and press Enter. The dialog opens with focus inside it, and Esc cancels.

### Step 4 — Commit, push, PR

```powershell
git add -A SafyaClinic Docs
git status
git commit -m "F7c: one h1 per page, toasts, one confirm dialog (no browser confirm), no empty validation boxes"
git push -u origin chore/f7c-shell-feedback
```

Send me the PR number and the CI result.

## Where we are after this

**F7 (app shell) is complete.** Group 2 still has:
- **F8:** the component library (form field, status badge, data table, patient header with allergies);
- **F9:** Arabic/English plumbing;
- **F4d-3:** a small migration that drops the old `Roles` tables.

After those comes the **pilot** (Group 3).
