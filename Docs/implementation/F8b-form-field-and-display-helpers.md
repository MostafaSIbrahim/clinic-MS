# Slice F8b — Form field, money, date-time and icon-button helpers

Roadmap item **F8** (component library), slice b of 3 (Group 2).

**Implemented by Claude at the owner's request.** The owner builds, tests, checks and opens the PR.

| | |
|---|---|
| Branch | `chore/f8b-form-helpers` (from the working copy after PR #31 is merged) |
| Behaviour change | **None on existing screens.** This slice adds building blocks for the pilot and for migrated screens. They show up on the component sample page in F8c |
| Database | None |
| Tests | +6 (about 247) |

## The helpers

| Tag | What it renders | Guarantees |
|---|---|---|
| `<app-field asp-for="FullName" label="Full name" required="true" />` | label + control + optional hint + validation message, in a `div.app-field` | **Refuses to render without a label.** `for`/`id` are linked, and `aria-describedby` points to the hint and the error. It sets `aria-invalid` when the server rejected the value, and marks required fields with `*` (hidden from screen readers; the `required` attribute is what they announce). It uses the same validation as `asp-for` (client and server) |
| `type="email"`, `"tel"`, `"number"`, `"date"`, `"time"`, `"datetime-local"`, `"password"`, `"textarea"` (with `rows`), `"select"` (with `asp-items` and `option-label`) | the matching control | Dates are formatted for the browser's date input |
| `<app-money value="@amount" />` | `<span class="app-money">1,250.50 EGP</span>` | Western digits and tabular figures (D10). A missing value shows "—" |
| `<app-datetime utc="@x" />` (optional `format="dd MMM yyyy"`) | `<time datetime="…">02 Dec 2026 00:30</time>` | Stored UTC is shown in **clinic time** (F2). A missing value shows "—" |
| `<app-icon-button icon="bi-trash" label="Delete attachment" variant="outline-danger" type="submit" data-confirm="Delete this attachment?" />` | an icon button with hidden text and a tooltip | **Refuses to render without a label**, so it always has an accessible name. Other attributes (`data-confirm`, `formaction`, `name`, `value`) pass through |

They're registered for every view through `_ViewImports` (`@addTagHelper *, SafyaClinic.Web`). Styles live in `site.css` (`.app-field`, `.app-required`, `.app-field-error`, `.app-money`, `.app-datetime`, `.app-icon-button`) and use tokens only.

## Files

| Action | Path |
|---|---|
| Add | `SafyaClinic.Web/TagHelpers/FieldTagHelper.cs`, `TagHelpers/DisplayTagHelpers.cs` (money, date-time, icon button) |
| Edit | `Views/_ViewImports.cshtml` (register the app's tag helpers), `wwwroot/css/site.css` |
| Add | `tests/SafyaClinic.Tests/Web/TagHelperTests.cs` |

## Rollback

Revert the PR's merge commit.

---

## Your steps

Run these in PowerShell from `D:\MyDevWork\clinic-MS`.

### Step 1 — Branch

Merge **PR #31** first. Then:

```powershell
git switch -c chore/f8b-form-helpers
git status
```

You should see 2 modified files, plus the new `TagHelpers` folder and `TagHelperTests.cs`.

### Step 2 — Build and test

```powershell
dotnet build SafyaClinic\SafyaClinic.sln
dotnet test SafyaClinic\SafyaClinic.sln
```

Expected: about **247 passed**, 0 failed.

### Step 3 — Quick check

```powershell
dotnet run --project SafyaClinic\SafyaClinic.Web --launch-profile http
```

Click through a few screens (Dashboard, Patients, a reservation, Users). Nothing should look different; registering the new tag helpers must not change existing pages. The helpers become visible in F8c.

### Step 4 — Commit, push, PR

```powershell
git add -A SafyaClinic Docs
git status
git commit -m "F8b: form field, money, date-time and icon-button tag helpers (labels required)"
git push -u origin chore/f8b-form-helpers
```

Send me the PR number and the CI result.

## Next: F8c

1. A data table (Direction C: plain rows, header row, empty state, pagination, stacked layout on phones).
2. A filter bar, card, KPI tile and tabs.
3. A **component sample page** (Admin only) that shows every component, including these helpers, so each can be checked with axe in isolation.
