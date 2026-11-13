# Slice M4b: weekly follow-ups

Group 4, item **M4** (nutrition), slice b. Follows the approved plan (`M4-nutrition-plan.md`; decision 3: only the latest week can be deleted, and deleting the last week of a completed enrollment makes it active again).

**Claude implemented this slice at the owner's request.** The owner runs one command, does the manual check, and pushes.

| | |
|---|---|
| Branch | `feature/m4b-follow-ups` (from `main` after the M4a fix PR) |
| Database | None |

## Behaviour change

**1. Edit really edits** (fixes N1).
- Before, saving the edit page **recorded a new week** (week 3, or week 4, which also closed the enrollment) and left the original unchanged.
- Now it saves to the same week; its number never changes.
- The height is kept. It was silently cleared on every edit, because it wasn't loaded into the edit form.
- After saving, the follow-up page opens.

**2. Administered items are ticked from the package** (fixes N3).
- The form lists the enrollment's package items: this week's first, then the other weeks under *Items planned for other weeks*.
- Each item shows its name, its type (injection or vitamin) and the planned quantity. Tick what was given and check the quantity.
- Before, people had to type the item's database number, and wrong numbers were silently dropped.

**3. Lab results are chosen from the analysis list.** Each row has *Analysis*, *Result*, *Reference range* and *Normal?*, with **Add lab result** and a remove button. Before, the analysis also had to be typed as a database number.

**4. Editing includes items and lab results** (fixes N4). They're loaded into the form and saved, replacing what was there.

**5. No crash when editing a blood pressure saved with only one number** (fixes N5).

**6. The server checks the form, instead of dropping bad entries.** It refuses, with a message:
- an item from another package;
- a ticked item without a quantity;
- the same item twice;
- a lab result without its analysis, or with an unknown one;
- an invalid diet compliance;
- a missing date.

**7. Deleting** (fixes N6, decision 3):
- Only the **latest** week can be deleted, so weeks stay 1–4 with no gaps.
- Deleting week 4 of a **completed** enrollment makes it **active** again. That's refused if the patient has since started another active enrollment.
- A **cancelled** enrollment's follow-ups can't be edited or deleted.
- Edits and deletions are in the audit log as *FollowUp.Edited* and *FollowUp.Deleted*.

**8. Screens:**
- The record, edit and follow-up pages are in the shared layout and Arabic, with the patient header.
- On the follow-up page, **Edit**, **Delete** (latest week only) and **Mark completed** are shown to people who manage nutrition.
- The administered time is shown in clinic time.
- The record form starts on the clinic's date, and typing is kept across a language switch.
- A refused form keeps its item and analysis lists.

## Unchanged

- Four weeks per enrollment.
- Recording only while Active.
- Week 4 completes the enrollment.
- Who can record.
- The BMI calculation.

## Tests

There are 9 new test cases:
- Edit changes the same week and replaces its items and lab results. The edit form gets them back, including the height.
- An item from another package is refused, and nothing is saved.
- Three kinds of invalid lab result or diet compliance are refused.
- A ticked item needs a quantity.
- Only the latest week can be deleted, and the next one recorded fills the same number again.
- Deleting week 4 of a completed enrollment makes it active again, audited.
- A cancelled enrollment's follow-ups can't be edited or deleted.

The browser test gains the nutrition flow in M4c, which creates a package through the screens.

## Files

| Action | Path |
|---|---|
| Add | `Web/Views/Nutrition/_FollowUpItemRow.cshtml`, `Web/Models/FollowUpItemRow.cs`, `tests/.../Application/FollowUpTests.cs` |
| Edit | `Application/Services/NutritionService.cs` (checks, edit, delete rule), `DTOs/Nutrition/WeeklyFollowUpDto.cs`, `Common/Auditing/Audit.cs` |
| Edit | `Web/Controllers/NutritionController.cs`, `Views/Nutrition/RecordFollowUp.cshtml`, `FollowUpDetails.cshtml` (rewritten), `EnrollmentDetails.cshtml` (Delete on the latest week only), `wwwroot/css/site.css`, `Resources/SharedResource.ar.resx` (+52) |

## Rollback

Revert the PR's merge commit.

---

## Your steps

1. If you haven't yet, merge the **M4a fix** PR (from `feature/m4a-enrollments`). Your folder is on `main` with the M4b changes in it.
2. In PowerShell:

```powershell
git pull
.\.assessment\slice.ps1 -Branch feature/m4b-follow-ups `
  -Message 'M4b: follow-up edit saves the same week; items ticked from the package and lab results from the list; only the latest week can be deleted; screens in the shared layout and Arabic'
```

### Manual check (while the app runs)

As **Admin**, with a test patient enrolled (Active) in a package that has items in weeks 1 and 2.

1. On the enrollment, select **Record week 1**.
   - The form shows the patient header and today's date.
   - Under **Given this week** you see week 1's items, with the planned quantity. Week 2's items are under *Items planned for other weeks*.
2. Enter a weight and height, tick one item and change its quantity. Add a lab result, choosing the analysis from the list. Save. Week 1 is recorded.
3. Open week 1 and select **Edit**.
   - The ticked item, its quantity, the lab result and the height are filled in.
   - Change the weight and save. **It's still week 1**, there's no week 2, and the height is kept.
4. Record week 2. On the enrollment, only **week 2** has a delete button. Delete it, then record it again: it's week 2 again.
5. Tick an item but clear its quantity, then save. It's refused with a message, and the lists are still there.
6. Optional, to check that deleting week 4 reopens an enrollment: record up to week 4, and the enrollment is Completed. Delete week 4: it's Active again.
7. **Arabic:** the form and the follow-up page are in Arabic. Switch language while filling in the form: your typing is kept.

When you've finished, press Enter in the script, then type `YES` to commit and push. Open the PR, merge it, run `git switch main` and `git pull`, and send me its number and the CI result.

## Next: M4c, packages (closes M4)

- The package list, details and *New package*, with labelled weekly item rows, in the shared layout and Arabic.
- The browser test runs the nutrition flow: create a package, enroll, record and edit a week.
