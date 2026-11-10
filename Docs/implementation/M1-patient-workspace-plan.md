# M1 plan: one patient workspace

Group 4, item M1 of the roadmap (`docs/assessment/12-prioritized-roadmap.md`). This is the plan for review. **Nothing has been changed yet.**

## Today

A patient's information is spread over **six separate pages**. Each is reached from a row of buttons on the patient page, and each has its own header and its own way back:

| Page | Address | Who sees it |
|---|---|---|
| Patient page (info, contacts, last and next visit) | `/Patients/Details/{id}` | everyone with *view patients* |
| Medical records | `/MedicalRecords/PatientRecords?patientId=` | doctors, admin |
| Lab analyses | `/Analysis/PatientAnalyses?patientId=` | doctors, admin |
| Nutrition enrollments | `/Nutrition/PatientEnrollments?patientId=` | nutritionists, admin |
| Payments | `/Payments/PatientSummary?patientId=` | front desk, supervisors, admin |
| Timeline | `/Patients/Timeline/{id}` | doctors, admin |

There's also no list of **all** the patient's visits. The patient page shows only the last completed visit and the next one.

## Target

**One page per patient**, `/Patients/Details/{id}`, made of:

- the **patient header** (name, age, phone, allergies) and the main actions (**Book appointment**, **Edit**), always visible;
- **tabs** below it:

| Tab | Content | Shown to people who can |
|---|---|---|
| **Overview** | The patient's details, medical information, contacts, and the last and next visits | view patients |
| **Visits** *(new)* | Every reservation, upcoming first, then past, with status and coverage. Actions on each row: **Check in**, **Collect**, **Open**, **Move** (same rules as Appointments and the dashboard) | view reservations |
| **Records** | Medical records, as on the records page today | view records |
| **Lab** | Lab requests and results | view analyses |
| **Nutrition** | Enrollments and follow-ups | view nutrition |
| **Payments** | The payment summary: balances, collect, write off, history | view payments |
| **Timeline** | Everything in date order | view the timeline |

- The six old addresses **redirect** to the matching tab, so bookmarks, links in other screens and the browser test keep working.
- Each tab meets the roadmap's *screen done* rules:
  - shared components only;
  - one heading per page;
  - zero serious accessibility problems;
  - works from the keyboard;
  - Arabic text;
  - works at phone, tablet and desk widths;
  - checked by the browser test.

## Slices

| Slice | Content | Risk |
|---|---|---|
| **M1a** | The tab bar and header. **Overview** (today's patient page, restyled) and the new **Visits** tab. Tabs a person can't use are hidden | Low |
| **M1b** | **Records** and **Lab** tabs; their old pages redirect | Low. The content is moved, not changed |
| **M1c** | **Payments** and **Nutrition** tabs; their old pages redirect. Collect and write off return to the Payments tab | Medium. The money screens must show the same figures (the browser test checks the collect and write-off flow) |
| **M1d** | **Timeline** tab; old views removed; the browser test visits every tab at the three widths | Low |

Each slice is the usual flow: I implement it, you run the script, check and push.

**No database change** in any slice.

## Decisions for you

1. **How tabs load.**
   - *Recommended:* each tab is a quick page load of its own (`?tab=records`). The page stays light for a patient with years of history, every tab can be bookmarked, and the server shows only what a person may see.
   - *Alternative:* everything loads at once and the tabs switch instantly. The page becomes slower as records grow.
2. **Which tab opens first.**
   - *Recommended:* the tab that fits the person's work: **Visits** for the front desk, **Records** for doctors, **Nutrition** for nutritionists. Any tab can then be chosen.
   - *Alternatives:* always **Overview**, or the tab the person used last.
3. **The patient list.** Should opening a patient from the list or the search go straight to this page? (Recommended: yes. Today it already opens the patient page.)

## Risks

| Risk | Handling |
|---|---|
| Staff look for a page that moved | Old addresses redirect to the tab. The tab names match today's button names |
| Payment figures change while moving | The content is moved, not rewritten. The browser test covers collect and write-off, and the manual check compares figures before and after |
| Someone sees a tab they shouldn't | Tabs are hidden **and** the server refuses the content without the permission; covered by tests |
| A big page for long-standing patients | One tab is loaded at a time (decision 1) |

## Rollback

Each slice is reverted on its own by reverting its PR's merge commit.

## Next

Reply with your choices (or "approve M1" for the recommended ones). I'll then implement M1a.
