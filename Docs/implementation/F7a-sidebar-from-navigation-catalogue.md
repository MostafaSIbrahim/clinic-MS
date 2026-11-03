# Slice F7a — Sidebar built from a navigation catalogue

Roadmap item **F7** (app shell), slice a of 3 (Group 2). Fixes the sidebar part of **F26** (list structure, labelled groups) and finishes "sidebar from catalogue" from D1.

**Implemented by Claude at the owner's request.** The owner builds, tests, checks and opens the PR.

| | |
|---|---|
| Branch | `chore/f7a-sidebar-catalogue` (from the working copy after PR #26 is merged) |
| Behaviour change | The sidebar is now drawn from **one list of destinations** (`Web/Navigation/NavigationCatalogue.cs`), and each entry names the permission that shows it. Every group sees **the same pages as before**. Visible differences: 1. Links are grouped under clear workspace headings: **Front desk** (Patients, Appointment Board, Reservations, Queue, Follow-ups), **Clinical** (Analysis, Nutrition), **Billing** and **Administration**. 2. **Administration** is a plain list (Users, Permission Groups, Lookups, Patient Sources, Clinics) instead of the "System Lookups" dropdown. The individual lookup types are reached from the Lookups page, as before. 3. The queue has its own icon. 4. Exactly one link is marked as the current page (`aria-current="page"`) |
| Accessibility | Each group is a real `<ul>` named by its `<h2>` heading. The section labels were list items before, which broke list semantics (axe `list` rule) |
| Database | None |
| Tests | +14 (224): the exact menu for each built-in group; empty groups left out; the current-page link; every menu permission exists; every link points to an existing action |

## Design

| Decision | Choice |
|---|---|
| Where the menu lives | `NavigationCatalogue`: sections, then entries with label, icon, controller, action and the permission that shows it. Two entries change label by permission: **My Queue / Doctor Queues** and **Unpaid Payments / Payment Dashboard** |
| Rendering | `SidebarNavViewComponent` checks the menu's permissions for the current user (from the per-request cache) and renders `Views/Shared/Components/SidebarNav/Default.cshtml` |
| Current page | Matched by controller, with action rules where one controller has several entries (Reservations: Board / Queue / everything else; Payments: Report / everything else) |
| Layout | The sidebar markup in `_Layout` becomes one line. The role and permission variables at the top of the layout are gone |
| Adding a page later | Add one entry to the catalogue with its permission. The menu, the tests and the permission check stay in step |

## Files

| Action | Path |
|---|---|
| Add | `SafyaClinic.Web/Navigation/NavigationCatalogue.cs`, `SidebarNavViewComponent.cs` |
| Add | `SafyaClinic.Web/Views/Shared/Components/SidebarNav/Default.cshtml` |
| Edit | `SafyaClinic.Web/Views/Shared/_Layout.cshtml` (sidebar list replaced; top code block removed) |
| Edit | `SafyaClinic.Web/wwwroot/css/site.css` (two small rules for the section lists and headings) |
| Add | `tests/SafyaClinic.Tests/Web/NavigationCatalogueTests.cs` |

## Rollback

Revert the PR's merge commit.

---

## Your steps

Run these in PowerShell from `D:\MyDevWork\clinic-MS`.

### Step 1 — Branch

Merge **PR #26** first. Then:

```powershell
git switch -c chore/f7a-sidebar-catalogue
git status
```

### Step 2 — Build and test

```powershell
dotnet build SafyaClinic\SafyaClinic.sln
dotnet test SafyaClinic\SafyaClinic.sln
```

Expected: **Passed: 224**, Failed: 0. If the count is off by one but nothing fails, that's fine; tell me the number.

### Step 3 — Check the menu per user

```powershell
dotnet run --project SafyaClinic\SafyaClinic.Web --launch-profile http
```

| User | Sidebar should show |
|---|---|
| **Admin** | Dashboard · **Front desk**: Patients, Appointment Board, Reservations, Doctor Queues, Follow-ups · **Clinical**: Analysis, Nutrition · **Billing**: Payment Dashboard, Payment Reports · **Administration**: Users, Permission Groups, Lookups, Patient Sources, Clinics |
| **Doctor** | Dashboard · Front desk: Patients, Appointment Board, Reservations, My Queue, Follow-ups · Clinical: Analysis · Billing: Payment Reports |
| **Nutritionist** (not Enas) | Dashboard · Front desk: Patients, Appointment Board, Reservations · Clinical: Nutrition |
| **Reception** | Dashboard · Front desk: Patients, Appointment Board, Reservations, Doctor Queues, Follow-ups · Billing: Unpaid Payments, Payment Reports |
| **mm** | Dashboard · Billing: Unpaid Payments, Payment Reports |

Also check:
- **The current page is highlighted:** open a reservation's Edit page and Reservations is highlighted; open the queue and only the queue is highlighted.
- **On a narrow window** (under about 990px wide), the ☰ button opens the menu and the × closes it.
- **Admin → Lookups → a lookup type** still opens.

### Step 4 — Commit, push, PR

```powershell
git add -A SafyaClinic Docs
git status
git commit -m "F7a: sidebar built from the navigation catalogue; labelled workspace groups; one current page"
git push -u origin chore/f7a-sidebar-catalogue
```

Send me the PR number and the CI result.

## Next: F7b and F7c

- **F7b:**
  - a top bar with patient search by phone or name, available from every page;
  - a user menu (name, change password, sign out), which moves sign-out out of the sidebar footer;
  - a placeholder for the language switch.
- **F7c:**
  - one page-header pattern with a single `h1` and optional breadcrumb;
  - a toast region for messages;
  - a shared confirm dialog replacing the browser's `confirm()` in shared flows;
  - a validation summary that shows nothing when the form is valid.
