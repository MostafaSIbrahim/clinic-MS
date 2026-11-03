# Slice F6a — Local assets: the app works without internet access

Roadmap item **F6**, part a (Group 2). Fixes the CDN part of **F30** (I5). Part b (tokens and the Bootstrap bridge) follows.

**Implemented by Claude at the owner's request.** The owner builds, tests, checks and opens the PR.

| | |
|---|---|
| Branch | `chore/f6a-local-assets` (from the working copy after PR #24 is merged) |
| Behaviour change | **Every stylesheet, script, icon font and web font is served by the app** (`wwwroot/lib`). There are no more CDN or Google Fonts links, so screens look and work the same on a clinic PC without internet. **Font Awesome is removed**: its 9 icons now use Bootstrap Icons. The public landing page uses **IBM Plex Sans Arabic** instead of Cairo, the Arabic font chosen in the design system. Everything else looks the same |
| Versions | Same as the CDNs used before: Bootstrap 5.3.3, Bootstrap Icons 1.11.3, jQuery 3.7.1, jquery-validation 1.20.1, jquery-validation-unobtrusive 4.0.0. The landing page moves from Bootstrap 5.3.0 to 5.3.3. The old template copies in `wwwroot/lib` (Bootstrap 5.1, jQuery 3.6) are replaced |
| Database | None |
| Tests | +13 (206): no external asset links anywhere; each local file exists |

## What changed

| Area | Change |
|---|---|
| `wwwroot/lib/` | Bootstrap 5.3.3 (`dist`), Bootstrap Icons 1.11.3 (`font`), jQuery 3.7.1, jquery-validation 1.20.1, unobtrusive 4.0.0, fonts (Inter variable Latin and Latin-ext; IBM Plex Sans Arabic 400–700). Every package keeps its licence file. `wwwroot/lib/README.md` lists each package, its version and how to update it |
| `wwwroot/css/fonts.css` | New: `@font-face` rules for Inter and IBM Plex Sans Arabic |
| `_Layout` | Local fonts, Bootstrap and icons. No preconnects and no Font Awesome |
| `_ValidationScriptsPartial` | Local jQuery and validation scripts |
| Standalone pages | `Home/Index` (landing), `PrintPrescription`, `PrintRequestSlip`, `CreateSuccessPopup`, `PatientReservationHistoryReport` now use local files |
| Icons | Syringe → `bi-eyedropper` (Lookups and menu). Landing page: sign-in → `bi-box-arrow-in-left`, stethoscope → `bi-heart-pulse`, apple → `bi-basket2`, child → `bi-person-hearts`, Instagram and Facebook → `bi-instagram` and `bi-facebook`, arrow → `bi-arrow-left` |

## Rollback

Revert the PR's merge commit.

---

## Your steps

Run these in PowerShell from `D:\MyDevWork\clinic-MS`.

### Step 1 — Branch

Merge **PR #24** first. Then:

```powershell
git switch -c chore/f6a-local-assets
git status
```

About 70 files are changed or new. Most of them are library files under `wwwroot/lib`.

### Step 2 — Build and test

```powershell
dotnet build SafyaClinic\SafyaClinic.sln
dotnet test SafyaClinic\SafyaClinic.sln
```

Expected: **Passed: 206**, Failed: 0.

### Step 3 — Offline check (the point of this slice)

```powershell
dotnet run --project SafyaClinic\SafyaClinic.Web --launch-profile http
```

1. **Turn off Wi-Fi or unplug the network**, then reload `http://localhost:5070` with Ctrl+F5.
2. **Landing page** (signed out): Arabic text in the new font, all icons visible, and the layout right-to-left.
3. **Sign in and open:**
   - the **Dashboard**, Patients, a **Reservation** and **Users**: styles, icons and dropdown menus all work;
   - **System Lookups**: the Injection Types card and menu item show the dropper icon;
   - **Add User**: submit it empty and the client-side validation messages appear (that's the local jQuery working);
   - **Print a prescription** and a **lab request slip**: they're styled as before.
4. Press F12 → **Network**, reload, and filter by "Failed" or check for red rows. There should be none.
5. Turn the network back on.

### Step 4 — Commit, push, PR

```powershell
git add -A SafyaClinic Docs
git status
git commit -m "F6a: all CSS, JS, icons and fonts served locally; Font Awesome removed; IBM Plex Sans Arabic on the landing page"
git push -u origin chore/f6a-local-assets
```

Send me the PR number and the CI result.

## Acceptance criteria

- [ ] No CDN or web-font links in any view, stylesheet or script (test).
- [ ] The app looks and works the same with the network off (Step 3).
- [ ] No Font Awesome; licence files kept; versions documented in `wwwroot/lib/README.md`.
- [ ] 206 tests pass locally and in CI.

## Next: F6b — tokens and the Bootstrap bridge

1. Copy `Docs/design/tokens.css` into the app.
2. Add `bootstrap-bridge.css`, which feeds the brand colours, radius, focus ring and control borders into Bootstrap.
3. Replace the 40-odd hard-coded colours in `site.css` and `branding.css` with tokens.
4. Make status badges meet 4.5:1 contrast.
