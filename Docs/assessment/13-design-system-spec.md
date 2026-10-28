# 13 — Design-System Specification (Phase 6: Design)

Status: **for review**, 2026-10-27. Defines the foundation every redesigned screen must use. No source files are changed in this phase.

Basis: decision D12 (Direction A — violet & teal, light labelled sidebar — with Direction C's plainer tables), D10 (Western digits), D11 (1366 / 1024 / 390), K5 (English default, Arabic switchable), findings F25–F30.

Companion files:
- `Docs/design/tokens.css` — the complete token file (copied into the app in roadmap item F6).
- Design canvas *Safya Clinic — Brand & Layout Options* — Direction A screens plus a **Components & states** board.

## 1. Principles

1. **Clinical safety first.** Patient identity and allergies are always visible on clinical screens; destructive or irreversible actions always confirm; status is always text, never colour alone.
2. **One way to do each thing.** One button set, one table, one form field, one status badge. Pages compose components; they do not restyle them.
3. **Calm and dense where it matters.** Front-desk and billing screens favour scannable tables (Direction C); consultation favours focus and large targets.
4. **Accessible by construction.** Components cannot be rendered without a label, a name or a heading level.
5. **Bilingual from day one.** Logical properties, resource strings, RTL-safe icons; Western digits everywhere.

## 2. The UI rule — where a style belongs

Before adding any CSS, decide in this order (from the brief):

| # | Belongs in… | When | Example |
|---|---|---|---|
| 1 | **Design token** | A raw value that recurs or expresses brand/meaning | colour, spacing step, radius, status colour, header height |
| 2 | **Primitive** | A styled HTML element with no business meaning | `.app-btn`, `.app-input`, `.app-badge` |
| 3 | **Shared component** | A recurring composition of primitives with behaviour or a11y rules | page header, data table, patient header, confirm dialog |
| 4 | **Layout utility** | Arranging components without styling them | `.app-stack`, `.app-cluster`, `.app-grid-2`, `.app-split` |
| 5 | **Page-specific CSS** | Genuinely unique to one screen and not expected to recur | appointment calendar grid, prescription print layout |

Review rules for migrated screens: no inline `style=""`; no hex/rgb values outside `tokens.css`; no new class without a prefix (`app-`) and a home in one of the five layers; page CSS files must justify why the style is not reusable.

**CSS file layers (load order):** `tokens.css` → `bootstrap-bridge.css` → `primitives.css` → `components.css` → `utilities.css` → `pages/*.css` (rare). Bootstrap 5.3 stays underneath; the bridge maps tokens onto Bootstrap's own variables so existing Bootstrap markup inherits the new look during migration.

| Bootstrap variable | Mapped to |
|---|---|
| `--bs-body-font-family` / `-size` / `-color` / `-bg` | `--font-sans` / `--font-size-md` / `--color-text` / `--color-bg-page` |
| `--bs-primary`, `--bs-primary-rgb` | `--color-primary` |
| `--bs-link-color`, `--bs-link-hover-color` | `--color-link`, `--color-link-hover` |
| `--bs-border-color` | `--color-border` |
| `--bs-border-radius`, `-lg`, `-pill` | `--radius-md`, `--radius-lg`, `--radius-pill` |
| `--bs-success` / `--bs-warning` / `--bs-danger` / `--bs-info` | semantic success/warning/danger/info tokens |
| `.form-control`, `.form-select` border colour | `--color-border-control` (3.57:1) |
| `:focus-visible` box-shadow | `--focus-ring` |

## 3. Tokens

Full values and contrast notes are in `Docs/design/tokens.css`. Summary:

| Group | Tokens | Notes |
|---|---|---|
| **Colour — brand** | primary `#5B21B6` (hover `#4C1D95`, active `#3B0F7A`, subtle `#EDE9FE`), accent `#0E7490` (hover `#155E75`) | White on primary 8.98:1; on accent 5.36:1 |
| **Colour — neutral** | page `#F7F6FB`, surface `#FFFFFF`, subtle `#FBFAFE`, border `#E4E1EE`, **control border `#8A84A0`**, text `#1C1530`, muted `#5B5670` | Control border corrects the mock-ups' `#D6D2E3` (1.48:1) to meet WCAG 1.4.11 (3:1) |
| **Colour — feedback** | success / warning / danger / info as fg + bg + border + icon | All fg/bg pairs ≥ 6:1 |
| **Colour — domain status** | booked, waiting, in-consultation, completed, cancelled, no-show, paid, due, write-off, allergy | Replace DB `ColorCode` (white on `#FFC107` = 1.63:1) |
| **Typography** | Inter (en), IBM Plex Sans Arabic (ar); sizes 12 / 13 / 14 / 16 / 18 / 22 / 24 / 28 px; weights 400–700; line height 1.25 / 1.5; tabular numerals for money and time | Inputs use 16 px below 992 px to avoid mobile zoom |
| **Spacing** | 4 px base: 4, 8, 12, 16, 20, 24, 28, 32, 40, 48, 64 | Component padding uses only these steps |
| **Radius** | 6 / 10 / 14 / 20 / pill | Buttons + inputs 10, cards 14, modals 20 |
| **Shadow** | none, sm (cards on hover), md (dropdowns), lg (modals), focus ring | Cards are flat by default — borders, not shadows (C) |
| **Motion** | 0 / 120 / 180 / 240 ms; standard and exit easing | All set to 0 under `prefers-reduced-motion` |
| **Breakpoints** | Bootstrap: 576 / 768 / 992 / 1200 / 1400 | Reference sizes 1366, 1024, 390 (D11) |
| **Layout** | header 56 px; sidebar 232 px (collapsed 64); page gutter 28 / 20 / 16 px; content max 1440 px; form max 760 px; 12-column grid, 16 px gap; control height 40 px desktop / 44 px touch | |
| **Grid / table lines** | row divider `#EEECF4`, header line `#E4E1EE`, header bg `#FBFAFE`, hover `#F7F6FB`, selected `#F5F3FF`; cell padding 10 × 16 px | Direction C table style |
| **Z-index** | base 0 → sticky 100 → sidebar 200 → header 300 → dropdown 400 → modal 500/510 → toast 600 | |

## 4. Interaction states

Every interactive component implements these states; the table defines the default treatment, components may refine it.

| State | Visual treatment | Behaviour / semantics |
|---|---|---|
| **Default** | Token colours as specified per component | — |
| **Hover** | Background one step darker (primary → hover), or `--grid-row-hover-bg` for rows; cursor pointer; transition `--duration-fast` | Never the only way to reveal an action (touch has no hover) |
| **Focus** (keyboard) | `--focus-ring` (2 px white + 2 px violet) on `:focus-visible`; never removed | Visible on every focusable element, including table rows with actions and scroll regions |
| **Active / pressed** | Primary → `--color-primary-active`; secondary → `--color-surface-sunken` | — |
| **Selected** | `--color-primary-subtle` background + violet text + 3 px inline-start indicator (nav) or check icon (options) | `aria-current="page"` (nav), `aria-selected` (tabs), `aria-pressed` (toggle buttons), `aria-checked` |
| **Disabled** | `--color-text-disabled`, control background `--neutral-50`, no shadow, `cursor: not-allowed` | Use `disabled` attribute; prefer explaining why (hint text) over silently disabling; avoid disabling the only path forward |
| **Loading** | Button keeps its width, shows a 16 px spinner + "Saving…" text; tables/cards show skeleton rows; region marked `aria-busy="true"` | Prevents double submit (button disabled while loading) |
| **Success** | Toast (polite live region) with success icon, auto-dismiss 5 s, closable; inline success only for in-place edits | Focus is not moved |
| **Warning** | Inline alert with warning icon and text; persists until resolved | `role="status"` |
| **Error** | Field: `--color-danger` border + message below with icon, linked by `aria-describedby`, `aria-invalid="true"`; form: error summary at top listing linked fields, focused on submit; page: alert with retry | Server-side errors always shown (F15); never an empty error box (F26) |

## 5. Components

Delivery mechanism in Razor (from `11-…` §7): **tag helper** for primitives, **partial** for layout blocks, **view component** for data-loading components. Names are prefixed `app-`.

### 5.1 Buttons — tag helper `<app-button>`
| Variant | Use | Colours |
|---|---|---|
| Primary | The one main action per view region | `--color-primary` / white |
| Secondary | Other actions | white surface, `--color-border-control` border, text colour |
| Accent | Positive secondary flows (Collect payment, Add medicine) | white surface, accent border and text |
| Danger | Destructive (Cancel reservation, Delete) | `--color-danger` / white; always confirms |
| Ghost | Low-emphasis, toolbars | transparent, muted text, hover sunken |
| Icon button | Icon-only actions | ghost style; **`label` attribute required** → `aria-label`; 40/44 px square |

Sizes: md (40 px, default), sm (32 px, dense tables only — still ≥ 24 px target), lg (48 px, phone primary actions). Anatomy: optional leading icon (18 px), label, optional trailing icon. One primary button per region; order in RTL mirrors automatically.

### 5.2 Form field — tag helper `<app-field asp-for="…">`
Renders label (always visible, never placeholder-only), required marker ("Required" text for screen readers), control, hint, error. Works for input, textarea, select, date, time, number, money (EGP suffix), phone (Egyptian format hint, normalised on blur).
- Height 40 / 44 px; border `--color-border-control`; radius 10; padding 0 12 px.
- Notes and comment fields: `dir="auto"` (D: notes in Arabic or English).
- Validation: client hints via unobtrusive validation; server errors always rendered; error summary partial at form top only when errors exist.

### 5.3 Select and combobox
Native `<select>` for ≤ 15 options; **patient picker** and medicine autocomplete use the ARIA combobox pattern (`role="combobox"`, listbox, `aria-activedescendant`, Esc closes, Enter selects) — the existing `reservation-patient-picker.js` behaviour is kept and wrapped.

### 5.4 Data table — partial `_AppTable` + tag helpers for header/cell (Direction C)
- White surface, 1 px `--grid-line-strong` outer border, radius 14, header row `--grid-header-bg`, 12 px semibold muted header text (no all-caps in Arabic), row dividers `--grid-line`, no zebra.
- Numbers, money and times right-aligned with tabular numerals (aligned to the end side in RTL).
- `<caption>` (visually hidden if the card title already names it), `<th scope="col">`, row header `<th scope="row">` for the identifying column.
- Row actions: visible text buttons or an overflow menu with an accessible name per row ("Actions for Mona Adel").
- States: loading (skeleton rows), empty (empty-state component with next action), error (alert with retry), selected row (`--grid-row-selected-bg`).
- Sorting: header button with `aria-sort`. Pagination component below.
- **Responsive:** ≥ 768 px table; < 768 px either stacked cards (priority lists: appointments, queue, patients, collections) or horizontal scroll inside a focusable region with a visible scroll hint.

### 5.5 Card / panel — partial `_AppCard`
Surface, 1 px border, radius 14, padding 16–20; header with `h2`/`h3` (level passed in), optional actions slot; no shadow by default.

### 5.6 Alert — tag helper `<app-alert kind="info|success|warning|danger">`
Icon + title (optional) + text + optional action; colours from feedback tokens; radius 10. `role="alert"` only for errors that appear after an action; otherwise `role="status"`. Never rendered empty.

### 5.7 Badge / status pill — tag helper `<app-status value="Waiting" />`
Maps a domain status to its token set and **resource-localised label**; pill radius, 12 px semibold, 1 px border (C style). Optional leading dot icon for scanning. The DB `ColorCode` column is no longer used for display.

### 5.8 Tabs — partial `_AppTabs`
ARIA tabs pattern (arrow keys move, Home/End, `aria-selected`, `aria-controls`); underline indicator 3 px primary; for page-level sections that are separate URLs (patient workspace), render as a nav with `aria-current` instead of ARIA tabs.

### 5.9 Pagination — partial `_AppPagination` (replaces `_Pagination`)
`nav aria-label` + Previous / page numbers / Next; current page `aria-current="page"`; preserves filters (existing behaviour); shows "Showing 21–40 of 132"; 40/44 px targets.

### 5.10 Modal and confirm dialog — partial `_AppModal`, `_AppConfirm`
Bootstrap modal underneath; title is the accessible name; focus moves to the first field or the safest button; focus trapped; Esc and close button; returns focus to the trigger. **Confirm dialog** replaces all `confirm()` calls: states the consequence in plain words, names the object ("Cancel reservation #41 for Mona Adel?"), destructive button uses the danger variant and is not the default focus.

### 5.11 Toast — region in the shell
Bottom-end, max 3 stacked, polite live region; success auto-dismisses after 5 s, errors persist.

### 5.12 Empty state — partial `_AppEmpty`
Icon, one-line explanation, primary next action ("No reservations on this day — Book appointment"). Replaces bare "No records" text.

### 5.13 Application shell — `_Layout` + view components
| Part | Spec |
|---|---|
| Top bar (56 px, white, bottom border) | Logo (links to Today) · global patient search (combobox, phone-first) · notifications button (placeholder until E5) · language switch (EN / ع, `aria-pressed`) · user menu (name, role/group, profile, change password, sign out) |
| Sidebar (232 px, white, end border) | Grouped workspaces with section labels as headings (not list items); items 40 px, icon 18 px + label; selected item per §4; items rendered from the permission catalogue; collapsible to 64 px at 992–1199 px and by user choice; off-canvas below 992 px with focus trap |
| Main | Breadcrumb · patient header (if patient-scoped) · page header · content; gutter token; max width 1440 |
| Page header — partial `_AppPageHeader` | Exactly one `h1`; optional status badge; one primary action; secondary actions + overflow menu; wraps on small screens |
| Skip link | Existing; kept as the first focusable element |

### 5.14 Patient header — view component `PatientHeader(patientId)`
Avatar initials · full name (`h2` within the page, since the page `h1` is the task) · age, sex · primary phone · **allergies** (danger status style with warning icon; "No known allergies" stated explicitly when empty) · chronic diseases · last visit · balance due (only with `Payments` permissions). Sticky under the top bar on clinical screens; one line with an expand toggle below 768 px. Cached per request.

### 5.15 Appointment calendar (page-specific, P2)
Day and week views per doctor or clinic; time axis on the start side; free slots as buttons ("Book 15:30 with Dr. Karim"), booked slots as non-interactive blocks with status; past times not rendered (D7); keyboard: arrow keys across slots; on phones a single-day list of free slots.

## 6. Accessibility requirements (WCAG 2.1 AA)

| Area | Requirement | How it is guaranteed |
|---|---|---|
| Structure | `lang`/`dir` set from culture; one `h1`; ordered headings; landmarks (header, nav, main); lists only for lists | Shell + page-header component; axe in CI |
| Keyboard | Every action reachable and operable; visible focus; logical order; no keyboard traps except modals (with Esc) | Components; Playwright keyboard tests on pilot flow |
| Labels and names | Every control has a visible label; icon-only controls require a label attribute; links/buttons have unique names in context | Tag helpers refuse to render without label |
| Contrast | Text ≥ 4.5:1 (≥ 3:1 for ≥ 18.66 px bold / 24 px); UI component boundaries and focus ≥ 3:1 | Tokens pre-checked (§3); no ad-hoc colours |
| Colour use | Status, errors and allergies use text + icon, not colour alone | Status and alert components |
| Forms | Errors identified in text, linked to fields, summary on submit; required fields indicated in text; autocomplete attributes for phone/name | Form-field component |
| Motion | Respect `prefers-reduced-motion`; no auto-moving content; toasts pausable on hover/focus | Motion tokens |
| Targets | ≥ 44 × 44 px on touch layouts; ≥ 24 × 24 px minimum anywhere | Control-height tokens |
| Zoom/reflow | Usable at 200 % zoom and 320 px width without loss | Responsive rules §7 |
| Tables | Captions, header scopes, row headers, focusable scroll regions | Table component |
| Language | Arabic content marked `lang="ar"` when mixed; notes `dir="auto"` | Field component |
| Time limits | Session expiry warns before sign-out (8 h sliding today) | Shell script (later) |
| Verification | axe zero serious/critical per migrated screen; manual keyboard pass; NVDA spot check per workspace | Screen-done definition |

## 7. Responsive rules

| Width | Layout |
|---|---|
| ≥ 1200 px (reference 1366) | Sidebar 232 px; gutters 28 px; two/three-column page grids |
| 992–1199 px | Sidebar collapsible to 64 px icons with tooltips + accessible names; gutters 20 px |
| 768–991 px (reference 1024 landscape tablet falls in ≥ 992) | Sidebar off-canvas; two columns become one where content < 360 px wide; touch control height 44 px |
| < 768 px (reference 390) | Single column; priority tables become stacked cards; page actions wrap, primary action may dock at the bottom; patient header collapses to one line with expand |

Print: separate print stylesheet using the same tokens, hiding shell and interactive controls; A4/A5 prescription layouts preserved.

## 8. RTL and localisation rules

- Use logical properties only in new CSS: `margin-inline-start`, `padding-inline`, `inset-inline-end`, `border-inline-start`, `text-align: start`.
- Mirror directional icons (chevrons, arrows, back); do not mirror clocks, checkmarks, media icons, or the logo.
- Numbers, dates, money: formatted by the clinic helpers with Western digits in both languages; in Arabic UI, numbers keep their LTR order inside RTL text via `<bdi>` or `unicode-bidi: isolate`.
- Table column order mirrors in RTL; numeric columns stay end-aligned.
- Strings never concatenated from fragments; use resource strings with placeholders.

## 9. Definition of done for components

A component is done when: it uses only tokens; it renders all states in §4 that apply; it passes axe in isolation on the component sample page; it works in `en` and `ar` (RTL); it works at 1366, 1024 and 390; its Razor usage is documented with one example; it has at least one automated rendering test.

## 10. What changes for existing screens immediately (F6/F7)

Because the bridge maps tokens onto Bootstrap, existing pages pick up: new font and colours, control borders that meet 3:1, focus ring, status badges with passing contrast (once `_StatusBadge` uses `<app-status>`), the new shell with one `h1` per page, and no empty error boxes — before any page is individually redesigned.
