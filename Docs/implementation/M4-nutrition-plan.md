# M4 plan: nutrition

Group 4, item **M4** of the roadmap (`docs/assessment/12-prioritized-roadmap.md`): enrollments, weekly follow-ups and packages, with **explicit, tested rules for enrollment status** (Active, On hold, Completed, Cancelled). This is the plan for review. **Nothing has been changed yet.**

The injection and vitamin type screens are left for **M5** (settings), where the roadmap merges all catalogues into one editor.

## What I found

### Defects (things that are wrong today)

| # | Where | Problem | Effect |
|---|---|---|---|
| N1 | **Edit follow-up** | The edit page sends its changes to *Record follow-up*, not to *Edit*. | Editing week 2 **adds a new week** (week 3, or week 4, which also closes the enrollment) instead of changing week 2. The original stays unchanged. |
| N2 | **Enrollment status** | Any status can be set from any status, and the server doesn't check. If a change is refused, nobody is told. | A Completed or Cancelled enrollment can be set back to Active by a crafted request. There are no rules to test. |
| N3 | **Record follow-up: items and lab results** | The nurse or doctor must type a **package item number** and an **analysis type number**. These are database IDs, which nobody can know. Wrong numbers are silently dropped. | In practice, administered items and lab results are never recorded correctly. |
| N4 | **Edit follow-up** | Administered items and lab results aren't loaded or saved when editing. | They can't be corrected. |
| N5 | **Edit follow-up** | Reading back the blood pressure crashes the page if it was saved with only one of the two numbers. | Error page. |
| N6 | **Deleting a follow-up** | Any week can be deleted. The next one recorded is "highest week + 1". | Deleting week 2 of 3, then recording, gives weeks 1, 3, 4. The enrollment closes after three visits. Deleting week 4 leaves the enrollment Completed. |
| N7 | **Enroll** | The price preview shows **US dollars**. If the server refuses the enrollment (for example, a discount above the package's maximum), the package and doctor lists come back **empty**. The start date defaults to the server's date, not the clinic's. | Wrong currency on screen; the form has to be reloaded; wrong default date near midnight. |
| N8 | **Enrollment page** | Edit and Delete for follow-ups are shown to everyone who can view (the server refuses). *Record week N* is hidden after week 4 even if the package said otherwise. | Buttons that fail. |
| N9 | **Balance of a cancelled enrollment** | The unpaid part of a **cancelled** enrollment still counts as owed: on the patient's Payments tab, the Unpaid list and the dashboard. A cancelled visit doesn't. | Money shown as owed that the clinic may not expect (decision 1). |

### Layout and language

All 13 nutrition screens are English only, on the older layout:
- statuses are plain coloured words;
- inline width styles;
- placeholders instead of labels on the follow-up and package rows.

There is **no browser test** of nutrition.

## Proposed rules for enrollment status

| From | To | Who / how |
|---|---|---|
| Active | On hold, Cancelled | People with *Nutrition.Manage* |
| On hold | Active (resume), Cancelled | People with *Nutrition.Manage* |
| Active | Completed | **Automatically**, when the last week's follow-up is recorded (as today). Not by hand |
| Completed | Active | **Automatically**, if that last week's follow-up is deleted (decision 3) |
| Completed, Cancelled | anything | Refused: they're final (decision 2) |

- Follow-ups can only be recorded while the enrollment is **Active** (as today).
- Every status change is written to the **audit log**.

## Slices

| Slice | Content | Risk |
|---|---|---|
| **M4a: Enrollments** | The status rules above, enforced by the server, with unit tests and audit (N2). The enrollment page and Enroll form in the shared layout and Arabic, with the patient header (N7, N8). The cancelled-balance rule from decision 1 (N9) | Medium. It changes rules and, with decision 1, the Unpaid figures; covered by tests |
| **M4b: Weekly follow-ups** | Edit really edits (N1), including items and lab results (N4); no crash (N5). Items are **chosen from that week's package items**, and lab results from the analysis list (N3). The delete rule from decision 3 (N6). Record, edit and details screens in the shared layout and Arabic | Medium. Unit tests for numbering, delete and edit |
| **M4c: Packages** | Package list, details and *New package* (labelled weekly item rows) in the shared layout and Arabic. Rules unchanged | Low |

- **Each slice:** I implement it, you run the script, check and push.
- **Database:** no table changes expected. Decision 1 changes how balances are counted, not the stored data.
- **Browser test:** the test gains a nutrition run as Admin: enroll a patient, record week 1, edit it, and check the week count stays at 1.

## Decisions for you

1. **Unpaid balance of a cancelled enrollment.**
   - *Recommended:* **no longer owed**, like a cancelled visit. Money already paid stays recorded and in the reports; any refund is done by a supervisor as today.
   - *Alternative:* still owed, as today.
2. **Reopening Completed or Cancelled enrollments.**
   - *Recommended:* **final**; to continue, enroll the patient again. That's allowed once no enrollment is Active.
   - *Alternative:* Admin and clinic managers may reopen them.
3. **Deleting a follow-up.**
   - *Recommended:* **only the latest week** can be deleted. Deleting the last week of a Completed enrollment makes it Active again.
   - *Alternative:* any week, as today, accepting the gaps.

## Risks

| Risk | Handling |
|---|---|
| Decision 1 changes figures on the dashboard and Unpaid list | The M2 dashboard equivalence tests get a cancelled-enrollment case; the change is stated in the slice doc |
| Existing data already has gaps or extra weeks from N1/N6 | Read-only check first: I'll give you a `SELECT` to run locally, which lists enrollments with week gaps or duplicate weeks. Nothing is changed automatically |
| A nurse loses a typed follow-up | The draft keeper from M3b is added to the follow-up form |

## Rollback

Each slice is reverted on its own by reverting its PR's merge commit.

## Next

Reply with your choices, or **"approve M4"** for the recommended ones. I'll then implement M4a.
