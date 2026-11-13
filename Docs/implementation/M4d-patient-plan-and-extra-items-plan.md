# M4d plan: the patient's weekly plan, and items outside the package

From the owner's manual test of M4b (13 Nov 2026). This is the plan for review. **Nothing has been changed yet.**

## What was asked

1. **Plan the coming weeks.** When the doctor fills in the coming weeks on this week's form, those entries should appear on that week's form later, already filled in, and the doctor can change them.
2. **Add items outside the package:** vitamins, injections or other drugs, both for this week and in the plan for the coming weeks.

## Why this needs a database change

Today the system can only record items that are in the package. Every administered item must point to a package item, and there's nowhere to keep a plan for a patient's future weeks.

The section *Items planned for other weeks* in M4b is really "give an item from another week's package plan **this week**", not a plan. I'll rename it in M4b so it isn't mistaken for planning: *Items from other weeks (given early or late)*.

## Proposal

### New: the patient's plan

| Table | Content |
|---|---|
| **EnrollmentPlannedItems** (new) | One row per planned item per week, for one enrollment. Each row holds the week (1–4) and **exactly one** of:<ul><li>a package item;</li><li>an injection type;</li><li>a vitamin type;</li><li>a drug typed by name.</li></ul>It also holds the quantity, the unit and a note, and who planned it and when. |

### Changed: what was given

| Table | Change |
|---|---|
| **WeeklyAdministeredItems** | The package item becomes **optional**, and three optional columns are added: injection type, vitamin type, and a drug name typed by hand. Each row still names exactly one item, with its quantity. **Existing rows are kept as they are**: they all point to a package item. |

Both changes add to the database and delete nothing. The migration is run only with your approval, as before.

### How the form works

**This week (*Given this week*):**
- It starts from the patient's plan for this week, if one was saved earlier. Otherwise it starts from the package's items for this week.
- Planned items start **ticked**, with the planned quantity. The doctor unticks what wasn't given, and changes quantities.
- **Add an item**: choose an injection or vitamin from the lists, or type a drug name, with the quantity and unit.

**The coming weeks (*Plan for the coming weeks*, new):**
- There's one block per remaining week, up to week 4.
- Each block starts from the plan saved earlier, or from the package's items for that week.
- The doctor can change quantities, remove items, and **add** an injection, a vitamin or a typed drug.
- The plan is saved with the follow-up. It changes **this patient's** plan only, never the package.

**The follow-up page** shows what was given, including the extra items, marked *outside the package*. The enrollment page shows the plan for each week that isn't recorded yet.

## Decisions for you

1. **Do extra items cost the patient anything?**
   - *Recommended:* **no**, not now. They're recorded clinically; the package price stays what was agreed. Charging for extras would be a separate billing change.
   - *Alternative:* charge them as an extra on the enrollment's balance. That needs a price per item and changes billing.
2. **What does planned-but-not-given mean on the next week's form?**
   - *Recommended:* **ticked**, because the plan is what's expected; the doctor unticks what wasn't given.
   - *Alternative:* unticked; the doctor ticks what was given.
3. **Is M4b committed now?**
   - *Recommended:* **yes**. Commit M4b as it is, with the section renamed, since it fixes real defects (edit adding weeks, typed database numbers). Then do this as **M4d**, after M4c.
   - *Alternative:* hold M4b and add this to it, which makes it a bigger change with a migration.

## Slice M4d

| | |
|---|---|
| Database | **Yes.** One new table and three new columns; one column becomes optional. Hand-written migration plus the idempotent script for the live site. Additive only |
| Affected | Domain (two entities); Infrastructure (configurations, migration); `NutritionService` (plan save and load, extra items, checks); `NutritionController`; `RecordFollowUp.cshtml`, `FollowUpDetails.cshtml`, `EnrollmentDetails.cshtml`; resx |
| Rules | Each planned or given row names exactly one item. Quantities are above 0. Plan weeks are after the current week and at most 4. Injections and vitamins come from the catalogues (active ones); a typed drug needs a name up to 200 characters. A cancelled enrollment's plan can't be changed. |
| Tests | Unit tests for:<ul><li>saving the plan, and the next week's form starting from it;</li><li>package items as the fallback;</li><li>extra items (catalogue and typed);</li><li>the one-item rule;</li><li>refusals;</li><li>the plan not changing the package.</li></ul>Browser test: plan week 2 on week 1's form, then week 2's form shows it. |
| Risks | The migration on the live site. It's additive, and the idempotent script handles it. The follow-up form gets longer: the coming weeks are folded, and the current week is open. |
| Rollback | Revert the PR. The new table and columns can stay; old code ignores them. |
| Acceptance | Week N's form shows what was planned for week N, editable. An extra injection, vitamin or typed drug can be given and planned. The package is unchanged. Existing follow-ups still show correctly. |

## Next

Reply with your choices, or **"approve M4d"** for the recommended ones.
