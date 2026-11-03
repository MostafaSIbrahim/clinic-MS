# Slice F6b — Design tokens and the Bootstrap bridge

Roadmap item **F6**, part b (Group 2). Closes **F6**. Fixes the colour and contrast parts of **F27/F25** for shared styles.

**Implemented by Claude at the owner's request.** The owner builds, tests, checks and opens the PR.

| | |
|---|---|
| Branch | `chore/f6b-design-tokens` (from the working copy after PR #25 is merged) |
| Behaviour change | **Visual only.** Every colour, radius and shadow in the shared styles now comes from **one file**, `wwwroot/css/tokens.css` (the approved specification, copied unchanged). A new **Bootstrap bridge** feeds those tokens into Bootstrap, so existing Bootstrap buttons, badges, links, form controls, menus and pagination take the brand colours. The changes you'll notice are the **contrast fixes**, listed below |
| Database | None |
| Tests | +5 (211): the app tokens equal `Docs/design/tokens.css`; `site.css`, `branding.css` and the bridge contain no hard-coded colours; the layout loads the files in the right order |

## What you will notice

| Before | After | Why |
|---|---|---|
| Very light input and select borders (1.3:1) | Darker grey borders (3.57:1) | Form controls must be visible: WCAG 3:1 for control borders |
| Grey "muted" text (`.text-muted`, form hints) | Slightly darker | 6.98:1 for readable secondary text |
| Yellow `bg-warning` badges with dark text; pale-cyan `bg-info` | Soft amber / soft teal badges with dark-coloured text and a thin border | Readable at ≥ 6:1 |
| Bright green "success" and red "danger" buttons and badges | Deeper green (#15803D) and red (#B91C1C) | White text on them now meets 4.5:1 (was about 3:1) |
| Dashboard teal, amber and rose cards | Deeper teal, amber and red gradients | White figures on them now meet 4.5:1 |
| Sidebar section labels and icons in light grey | Darker grey | Labels readable (they were 2.5:1) |
| Card corners 16px | 14px | The design-system radius |
| Alerts with their own colours | Alerts use the status tokens | One set of success, warning, danger and info colours |

The layout, spacing, sidebar width, the violet brand colour and the page structure are **unchanged**. The sidebar and app shell are redesigned in **F7**.

## Files

| Action | Path |
|---|---|
| Add | `wwwroot/css/tokens.css`: an exact copy of `Docs/design/tokens.css` (a test keeps them identical) |
| Add | `wwwroot/css/bootstrap-bridge.css`: Bootstrap 5.3 variables mapped to tokens; buttons, badges, form controls, focus ring, dropdowns, pagination and pills |
| Edit | `wwwroot/css/site.css`: the old `--brand-*`, `--muted` and similar names now point to tokens, and every remaining hard-coded colour is replaced by a token |
| Edit | `wwwroot/css/branding.css`: logo focus outline uses the focus-ring token |
| Edit | `Views/Shared/_Layout.cshtml`: loads `tokens.css` and `bootstrap-bridge.css` (fonts → Bootstrap → icons → tokens → bridge → site → branding) |
| Edit | `Home/Index`, `PrintPrescription`, `PrintRequestSlip`, `PatientReservationHistoryReport`: load `tokens.css`, which `branding.css` needs |
| Add | `tests/SafyaClinic.Tests/Web/DesignTokensTests.cs` |

**Rule from now on (design system §2):** no hex or `rgb()` colours outside `tokens.css`. Shared CSS uses token names, and the test enforces this for the shared files.

## Rollback

Revert the PR's merge commit.

---

## Your steps

Run these in PowerShell from `D:\MyDevWork\clinic-MS`.

### Step 1 — Branch

Merge **PR #25** first. Then:

```powershell
git switch -c chore/f6b-design-tokens
git status
```

### Step 2 — Build and test

```powershell
dotnet build SafyaClinic\SafyaClinic.sln
dotnet test SafyaClinic\SafyaClinic.sln
```

Expected: **Passed: 211**, Failed: 0.

### Step 3 — Visual check

```powershell
dotnet run --project SafyaClinic\SafyaClinic.Web --launch-profile http
```

Reload with Ctrl+F5 and look at:

1. **Dashboard:** the four coloured cards are readable, and the sidebar labels and icons are darker.
2. **Patients → New / Users → Add User:** the input borders are clearly visible, and clicking a field shows a violet focus ring.
3. **A reservation and the Users list:**
   - green, red and grey badges are readable;
   - "Sensitive" badges (on a permission group page) and other amber or teal badges are soft with dark text.
4. **Buttons:** primary, outline, success and danger buttons all look right on hover.
5. **Alerts:** save something to see the green success message, and trigger a validation error for the red one.
6. **Print a prescription:** it looks the same as before.

If any screen looks broken (not just slightly different), send me a screenshot.

### Step 4 — Commit, push, PR

```powershell
git add -A SafyaClinic Docs
git status
git commit -m "F6b: design tokens and Bootstrap bridge; shared styles use tokens; contrast fixes for controls, badges, buttons and cards"
git push -u origin chore/f6b-design-tokens
```

Send me the PR number and the CI result.

## Acceptance criteria (F6)

- [ ] All colours in the layout and shared CSS come from tokens (test).
- [ ] Status badges and filled buttons meet 4.5:1; control borders meet 3:1.
- [ ] The app works offline (F6a), with no Font Awesome.
- [ ] 211 tests pass locally and in CI.

## Next: F7 — app shell (3 slices)

1. A top bar with patient search by phone and the user menu.
2. A labelled sidebar grouped by workspace, built from a navigation list with permissions.
3. One page-header pattern with a single `h1`, a toast region, a shared confirm dialog (replacing `confirm()`), and a validation summary that shows nothing when the form is valid.
