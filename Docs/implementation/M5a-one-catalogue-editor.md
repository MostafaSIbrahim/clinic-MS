# Slice M5a: one catalogue editor

Group 4, item **M5** (settings), slice a. Follows the approved plan (`M5-settings-plan.md`, approved 14 Nov 2026 with the recommended choices; decision 3: an item already used is made inactive, not deleted).

**Claude implemented this slice at the owner's request.** The owner runs one command, does the manual check, and pushes.

| | |
|---|---|
| Branch | `feature/m5a-catalogues` (from `main` after PR #76) |
| Database | None |

## Behaviour change

**1. One editor for the four catalogues** (*Settings → Lookups → any type*, or `/Lookups/Catalogue`):
- Tabs for **Treatment types**, **Analysis types**, **Injection types** and **Vitamin types**.
- A search box, and a table that becomes cards on phones.
- An *Active* or *Inactive* badge, with **Edit**, and **Delete** or **Make inactive**.
- Adding and editing use one form whose fields fit the catalogue:
  - **treatment:** category, price and duration;
  - **analysis:** price and preparation (printed on the lab slip);
  - **injection:** unit and usual dose;
  - **vitamin:** unit and form.
- In Arabic and English.

**2. The forms use their own input model** (fixes F16). Before, the form was saved straight into the database record, so a crafted form could set fields the screen doesn't show, such as the record's dates. The checks now live in one place (`CatalogueService`). The web layer no longer talks to the database directly for catalogues.

**3. Checks.** These were scattered or missing; now:
- The name is required, at most 100 characters, and not a **duplicate** in the same catalogue. Duplicates are new: two items with the same name were allowed before.
- **Treatment:** a category, a price of zero or more, and a duration of 5–480 minutes. A price of zero is still allowed only for the nutrition *Follow-up* visit, as before.
- **Injection and vitamin:** a unit.
- **Analysis:** a price can't be negative.

**4. Delete: as before, but complete** (decision 3).
- An item that was never used is deleted. One already used is made **inactive**, so history keeps its name, and it can be made active again on its edit form.
- *In use* now also counts lab requests and lab results (analysis types), and items given or planned outside a package (M4d). Before, deleting an injection given outside a package would have failed with an error.
- Analysis types can now be edited, made inactive and deleted. Before, they could only be added.

**5. The duplicate screens are gone.**
- The *Nutrition → Injection types / Vitamin types* screens (and their create and edit pages) are removed.
- Their addresses, and the old `/Lookups/TreatmentTypes`-style addresses, open the catalogue.
- The package screens link straight to it.

## Unchanged

- Who can edit catalogues: *Settings.Lookups*, which is Admin out of the box.
- Which items the booking, lab and package forms offer: active ones, as before.

## Tests

- **18 new test cases** replace the 4 old delete tests:
  - used items are made inactive, for a treatment with a reservation, an injection used in a package, given or planned, a vitamin in a package, and an analysis with a lab request;
  - unused items are deleted;
  - 8 invalid forms across the four catalogues are refused;
  - the free nutrition follow-up is allowed;
  - a duplicate name is refused;
  - editing changes only what the form shows, keeps the creation date, and can make an item active again.
- **The access baseline:** the old create, save and delete actions and the duplicate Nutrition screens are removed; the catalogue's four actions are added, with the same access (Admin).
- **The browser test** checks the catalogue and its form for accessibility at the three widths.

## Files

| Action | Path |
|---|---|
| Add | `Application/Catalogue/CatalogueService.cs`, `Web/Views/Lookups/Catalogue.cshtml`, `EditCatalogueItem.cshtml`, `tests/.../Application/CatalogueServiceTests.cs` |
| Edit | `Web/Controllers/LookupsController.cs` (rewritten), `NutritionController.cs` (type screens redirect), `Views/Nutrition/Packages.cshtml`, `CreatePackage.cshtml` (links), `Infrastructure/DependencyInjection/ApplicationServiceExtensions.cs`, `Resources/SharedResource.ar.resx` (+24), `tests/.../Web/Security/access-matrix.txt`, `e2e/tests/pilot-flow.spec.js` |
| Remove | `Web/Views/Lookups/AnalysisTypes.cshtml`, `TreatmentTypes.cshtml`, `InjectionTypes.cshtml`, `VitaminTypes.cshtml`; `Web/Views/Nutrition/InjectionTypes.cshtml`, `VitaminTypes.cshtml`, `CreateInjectionType.cshtml`, `CreateVitaminType.cshtml`, `EditInjectionType.cshtml`, `EditVitaminType.cshtml`; `tests/.../Web/Lookups/LookupsControllerDeleteTests.cs` |

## Rollback

Revert the PR's merge commit.

---

## Your steps

Your folder is on the M4d branch with the M5a changes in it. In PowerShell, run these one at a time.

1. Move to the latest `main`, keeping the changes:

```powershell
git fetch origin
git switch -C main origin/main
```

2. Remove the replaced files:

```powershell
git rm SafyaClinic/SafyaClinic.Web/Views/Lookups/AnalysisTypes.cshtml SafyaClinic/SafyaClinic.Web/Views/Lookups/TreatmentTypes.cshtml SafyaClinic/SafyaClinic.Web/Views/Lookups/InjectionTypes.cshtml SafyaClinic/SafyaClinic.Web/Views/Lookups/VitaminTypes.cshtml SafyaClinic/SafyaClinic.Web/Views/Nutrition/InjectionTypes.cshtml SafyaClinic/SafyaClinic.Web/Views/Nutrition/VitaminTypes.cshtml SafyaClinic/SafyaClinic.Web/Views/Nutrition/CreateInjectionType.cshtml SafyaClinic/SafyaClinic.Web/Views/Nutrition/CreateVitaminType.cshtml SafyaClinic/SafyaClinic.Web/Views/Nutrition/EditInjectionType.cshtml SafyaClinic/SafyaClinic.Web/Views/Nutrition/EditVitaminType.cshtml SafyaClinic/tests/SafyaClinic.Tests/Web/Lookups/LookupsControllerDeleteTests.cs
```

3. Run the slice script:

```powershell
.\.assessment\slice.ps1 -Branch feature/m5a-catalogues `
  -Message 'M5a: one catalogue editor for treatment, analysis, injection and vitamin types; own input model and checks; used items made inactive; duplicate screens removed'
```

### Manual check (while the app runs)

As **Admin**:

1. **Lookups → Treatment types**: it opens the new catalogue on the *Treatment types* tab. Try the other three tabs and the search.
2. **Add to Treatment types** with a price of **0** and category *Internal medicine*: refused, with a message next to the price. Name it `Follow-up`, category *Nutritional*, and it's allowed. Then delete it: it's deleted, because it has never been used.
3. Edit an existing treatment type that has bookings, and change its price: saved. Select **Make inactive**: it says it stays in the history, and the badge shows *Inactive*. Make it active again from its edit form.
4. Add an **Injection type** without a unit: refused. Add `B12` when one already exists: refused as a duplicate.
5. **Analysis types**: edit one's preparation and check that it prints on a new lab request slip.
6. The old addresses `/Nutrition/InjectionTypes` and `/Lookups/VitaminTypes` open the catalogue. The *Injection types* button on the package list does too.
7. **Arabic:** both screens are in Arabic. At phone width, the table becomes cards.

When you've finished, press Enter in the script, then type `YES` to commit and push. Open the PR, merge it, run `git switch main` and `git pull`, and send me its number and the CI result.

## Next: M5b, users and permission groups

Shared layout and Arabic. The rules stay as they are.
