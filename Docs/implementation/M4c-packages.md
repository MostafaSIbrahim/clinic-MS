# Slice M4c: packages (closes M4's planned screens)

Group 4, item **M4** (nutrition), slice c. Follows the approved plan (`M4-nutrition-plan.md`). The rules are unchanged, apart from two missing checks (point 4). The injection and vitamin type screens stay for **M5**, which merges all catalogues into one editor.

**Claude implemented this slice at the owner's request.** The owner runs one command, does the manual check, and pushes.

| | |
|---|---|
| Branch | `feature/m4c-packages-code`. PR #74 (`feature/m4c-packages`) carried only this document, because the code wasn't in the folder yet; this branch carries the code |
| Database | None |

## Behaviour change

**1. Package list:**
- the shared layout and Arabic;
- a table that becomes cards on phones, with the price, maximum discount and number of items;
- an empty state with **New package**.

The *Injection types* and *Vitamin types* buttons appear only for people who can edit catalogues.

**2. Package page:**
- the price, maximum discount and duration;
- the four weeks side by side, each item marked injection or vitamin, with its quantity, unit and note.

**Deactivate** asks first, says that current enrollments continue, and reports if it fails. Before, it always said "deactivated".

**3. New package:**
- The shared fields.
- Each weekly item row has visible labels: Week, Item, Quantity, Unit and Notes.
- There's **one Item list**, with injections and vitamins grouped. Before, there were two lists, and exactly one had to be left empty.
- Choosing an item fills in its usual unit.
- If the server refuses the form, the rows you entered come back. Before, they were lost.
- Typing is kept across a language switch.
- Removing a row in the middle no longer drops the rows after it, the same fix as for prescriptions in M3b.

**4. Two missing checks.** A package needs a **name**, and each item needs a **quantity above zero**.

## Unchanged

- Four weeks per package.
- Price and maximum discount checks.
- Each item is exactly one injection or vitamin.
- Who can create or deactivate packages.

## Tests

- **5 new unit tests:** a missing name or a zero or negative quantity is refused and nothing is saved, and a valid package is saved with its items.
- **New browser test, *nutrition: package, enroll, record and edit a week*, as Admin.** It:
  - creates a package through the screens;
  - enrolls a new patient;
  - records week 1 with an item ticked;
  - edits week 1, so the item comes back ticked and the weight changes;
  - checks it's **still week 1**, which covers defect N1.

The package, enrollment and follow-up screens are checked for accessibility, and at the three widths.

## Files

| Action | Path |
|---|---|
| Add | `tests/.../Application/PackageTests.cs` |
| Edit | `Web/Views/Nutrition/Packages.cshtml`, `PackageDetails.cshtml`, `CreatePackage.cshtml` (rewritten), `Web/Controllers/NutritionController.cs` (deactivate result), `Application/Services/NutritionService.cs` (name and quantity checks), `wwwroot/css/site.css`, `Resources/SharedResource.ar.resx` (+31) |
| Edit | `e2e/tests/pilot-flow.spec.js` (nutrition flow) |

## Rollback

Revert the PR's merge commit.

---

## Your steps

In PowerShell (the code is in your folder):

```powershell
.\.assessment\slice.ps1 -Branch feature/m4c-packages-code `
  -Message 'M4c: package list, page and New package in the shared layout and Arabic; one item list per row; name and quantity checks; browser test for the nutrition flow'
```

### Manual check (while the app runs)

As **Admin**:

1. **Nutrition** (menu): the package list in the shared layout, with price, maximum discount and items.
2. **New package**.
   - Fill in the name and price.
   - In the first row, choose week 1 and an item from the **Item** list: the unit fills in.
   - Add a second row for week 2, and a third row. Remove the **second** row.
   - Set the maximum discount to **150** and select **Create package**: the server refuses it, and **both remaining rows are still there**.
   - Set it back to 20 and create the package.
3. The package page shows the items under the right weeks. **Deactivate** asks first. Cancel the question.
4. **Arabic:** the three screens are in Arabic, and at phone width the list becomes cards.

When you've finished, press Enter in the script, then type `YES` to commit and push. Open the PR, merge it, run `git switch main` and `git pull`, and send me its number and the CI result.

## Next: M4d (approved 13 Nov 2026)

- The patient's own plan for the coming weeks, which pre-fills each week's form.
- Items outside the package: catalogue injections or vitamins, or a typed drug.
- Not charged; planned items start ticked.
- With a database migration, run with your approval.
