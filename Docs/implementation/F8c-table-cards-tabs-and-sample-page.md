# Slice F8c: data table, filter bar, card, KPI tile, tabs, pagination, empty state and the component sample page

Roadmap item **F8** (component library), slice c of 3 (Group 2). This slice **closes F8**.

**Claude implemented this slice at the owner's request.** The owner builds, tests, checks and opens the PR.

| | |
|---|---|
| Branch | `chore/f8c-table-and-sample-page` (from the working copy, after PR #32 is merged) |
| Behaviour change | 1. **Patients list** uses the new components. It has a filter bar with **Search** and **Clear**, and the Direction C table, where the patient's name is the row header. On a phone, each row becomes a small card with labelled values. There are friendlier empty states: "Find a patient to get started", and "No patients match this search" with a **Register patient** button. A "Showing 1–20 of 45" line sits above the page links. Screen readers hear "View *patient name*" on each row's button. 2. **Reservations and Analysis lists** use the new pagination. It shows "Showing 21–40 of 132" and adds links to the first and last page. Filters are kept, as before. 3. **New Admin-only page `/Components`**. It shows every shared component with sample data. Nothing on it is saved |
| Database | None |
| Tests | +18 .NET (**265** in total). The new tests cover the pagination text and page window, and confirm the old partial is gone. They check that the table, KPI tile, tabs and filter bar refuse to render without a label or caption, that a card heading must be level 2 to 4, and that the tabs' ARIA wiring is correct. +4 JavaScript tests cover the tab keys, including right-to-left |

## The components

| Use | What it guarantees |
|---|---|
| `<app-table caption="Patient results" caption-hidden="true" stack="true"> <thead>…</thead><tbody>…</tbody> </app-table>` | **A caption is required.** The table sits in a scroll region that can be focused and is named by the caption. It uses the Direction C style: plain rows, a header row, no zebra stripes. Add `class="app-table__num"` for right-aligned numbers and `app-table__actions` for the actions column. With `stack="true"`, rows become cards below 768 px, and each cell shows its `data-label` |
| `<app-filter-bar label="Find patients"> <div class="app-filter-bar__field">label + input</div> </app-filter-bar>` | A GET form with `role="search"` and a name. It adds **Search** and **Clear** buttons, and Clear returns to the page with no filters |
| `<app-card title="…" level="2" icon="bi-…"> <app-card-actions>buttons</app-card-actions> body </app-card>` | A `section` named by a real heading. **The level must be 2, 3 or 4**, because the page owns the `h1`. Actions are optional and sit next to the title |
| `<app-kpi label="Waiting now" value="7" tone="warning" href="…" hint="…" />` | **A label is required.** With `href`, the whole tile is one link. Tone is shown by an edge colour only; the words carry the meaning. Wrap tiles in `div.app-kpi-grid` |
| `<app-tabs label="…" id="patient"> <app-tab id="details" title="Details">…</app-tab> … </app-tabs>` | Follows the ARIA tabs pattern. Arrow keys move between tabs (mirrored in right-to-left pages), and Home and End jump to the first and last tab. The first tab is selected unless `selected="…"` names another. **For sections that are separate pages, use links instead** (as the patient workspace will in the pilot) |
| `<partial name="_AppPagination" model="PagerModel.From(Model)" />` | Shows "Showing 21–40 of 132" in Western digits, Previous and Next, two pages either side of the current one, and the first and last page. The current page has `aria-current="page"`, and filters are kept in every link. **This replaces `_Pagination`, which is removed** |
| `<partial name="_AppEmpty" model="new EmptyStateModel(…)" />` | An icon, one line saying what is empty, an optional hint and one optional next action |

All the styles are in `site.css` and use tokens only. The tab script is in `wwwroot/js/app-tabs.js`, which the layout loads.

## The sample page: `/Components` (Admin only)

The sample page shows each component with sample data:
- the form fields, with a **Show error state** button;
- money, date-time and icon buttons;
- every status badge;
- KPI tiles;
- the filter bar and table, which stack at phone width;
- pagination, an empty state, tabs, and the confirm dialog;
- a **right-to-left** block in Arabic.

The page is used to check each component with axe, the keyboard, at 390 px and in right-to-left. Access uses the **Manage users** permission, so in practice only Administrators can open it. It is reached by URL and isn't in the sidebar.

The patient header isn't on the sample page because it needs a real patient. Check it on a medical record, as in F8a.

## Files

| Action | Path |
|---|---|
| Add | `SafyaClinic.Web/TagHelpers/LayoutTagHelpers.cs` (table, card and card actions, KPI, tabs and tab, filter bar) |
| Add | `SafyaClinic.Web/Components/Pager.cs` (`PagerModel`, `EmptyStateModel`) |
| Add | `Views/Shared/_AppPagination.cshtml`, `Views/Shared/_AppEmpty.cshtml` |
| **Remove** | `Views/Shared/_Pagination.cshtml` (you do this in Step 1: the session can't delete files on your computer) |
| Add | `Controllers/ComponentsController.cs`, `Models/ComponentSampleModel.cs`, `Views/Components/Index.cshtml` |
| Add | `wwwroot/js/app-tabs.js`; edit `Views/Shared/_Layout.cshtml` (loads it) |
| Edit | `Views/Patients/Index.cshtml` (filter bar, table, empty states, pagination) |
| Edit | `Views/Reservations/Index.cshtml`, `Views/Analysis/Index.cshtml` (new pagination) |
| Edit | `wwwroot/css/site.css` (F8c section) |
| Edit | `tests/.../Web/Security/access-matrix.txt` (+ `Components.Index GET: Admin`) |
| Add | `tests/SafyaClinic.Tests/Web/LayoutComponentTests.cs`, `tests/app-tabs.test.cjs` |

## Rollback

Revert the PR's merge commit. This also brings back `_Pagination.cshtml`.

---

## Your steps

Run these in PowerShell from `D:\MyDevWork\clinic-MS`.

### Step 1: Branch and remove the old pagination partial

Merge **PR #32** first. Then:

```powershell
git switch -c chore/f8c-table-and-sample-page
git rm SafyaClinic\SafyaClinic.Web\Views\Shared\_Pagination.cshtml
git status
```

You should see 7 modified files, 1 deleted file and the new files listed above.

### Step 2: Build and test

```powershell
dotnet build SafyaClinic\SafyaClinic.sln
dotnet test SafyaClinic\SafyaClinic.sln
node --test "SafyaClinic/tests/**/*.test.cjs"
```

Expected results:
- **265 passed** for .NET;
- all JavaScript tests passing, including the 4 new tab tests.

If the .NET count is off by one or two but nothing fails, that's fine; tell me the number.

### Step 3: Check

```powershell
dotnet run --project SafyaClinic\SafyaClinic.Web --launch-profile http
```

1. **Patients**, as Reception or Admin:
   - Before searching, you see "Find a patient to get started".
   - Search for a name that doesn't exist. You see "No patients match this search" with a **Register patient** button.
   - Search for something common, such as one letter. You see the table, with "Showing 1–20 of N" above the page links if there's more than one page.
   - **Clear** empties the search.
   - Make the window narrow (phone width). Each patient becomes a small card, with labels such as "Phone" and "Age".
2. **Reservations** and **Analysis**: the "Showing…" line appears. Paging keeps your filters, as before.
3. **Sample page:** as Admin, open `http://localhost:<port>/Components`.
   - Click **Show error state**. Full name and Clinic show red error messages under the fields.
   - Tab to the tabs. The arrow keys, Home and End move between them. In the Arabic block, the arrows are mirrored.
   - The icon-button trash and **Cancel sample reservation** open the confirm dialog. Nothing changes.
   - Optional: run axe (the browser extension) on this page and tell me anything serious or critical it finds.
4. **As Reception or mm**, open `/Components`. You get Access denied.

### Step 4: Commit, push, PR

```powershell
git add -A SafyaClinic Docs
git status
git commit -m "F8c: data table, filter bar, card, KPI tile, tabs, pagination, empty state; component sample page"
git push -u origin chore/f8c-table-and-sample-page
```

Send me the PR number and the CI result.

## Where we are after this

**F8 (component library) is complete.** Group 2 still has two items:
- **F9:** Arabic/English plumbing (culture switch in the top bar, resource strings, `lang` and `dir` on the page).
- **F4d-3:** a small reviewed migration that drops the old `Roles` / `UserRoles` tables.

After those comes the **pilot** (Group 3), which uses these components on the real screens.
