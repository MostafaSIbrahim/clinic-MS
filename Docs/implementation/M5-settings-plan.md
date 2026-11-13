# M5 plan: settings

Group 4, item **M5** of the roadmap: users, permission groups, clinics and agreements, patient sources, **one catalogue editor** for treatment, analysis, injection and vitamin types, and a **clinic profile** for printouts instead of the `ClinicInfo` server setting. This is the plan for review. **Nothing has been changed yet.**

## What I found

| Area | Today |
|---|---|
| **Catalogues** | Two copies of the injection and vitamin type screens: *Lookups → Injection/Vitamin types*, and *Nutrition → Injection/Vitamin types*, which work differently. The Lookups screens **save the form straight into database objects** (F16), so a crafted form can set fields the screen doesn't show, such as *active* or the record's dates, and checks are scattered through the screen code. The web layer talks to the database directly. English only, with eight inline styles |
| **Users, Permission groups** | Work correctly (F4), but English only, older layout and inline styles |
| **Clinics, Patient sources** | Clinic details and opening hours were partly done in P1/P2; the list, create and edit forms and the patient sources are English only, with inline styles |
| **Lookups home** | A grid of cards that mixes users, groups and catalogues |
| **Printouts** | The clinic name, tagline, address, phone and email come from the server's `ClinicInfo` setting. Changing them needs a redeploy, and the readiness check still warns that they're example values |

## Slices

| Slice | Content | Risk |
|---|---|---|
| **M5a: One catalogue editor** | One screen pattern for the four catalogues: a list with search, add and edit in the same layout, and active or inactive. Forms use their own input models with validation instead of database objects (fixes F16); the logic moves into a catalogue service. **Delete stays as it is:** deleted if never used, otherwise made inactive with a message. The *Nutrition → Injection/Vitamin types* screens are removed, and their old addresses open the catalogue. Arabic | Medium. The existing delete tests are kept and extended |
| **M5b: Users and permission groups** | Shared layout, table cards on phones, Arabic. **Rules unchanged** (F4: the last admin is protected, permissions as today) | Low |
| **M5c: Clinics, agreements, patient sources, settings home** | Shared layout and Arabic. The *Lookups* home becomes **Settings**, grouped as People (users, groups), Clinic (profile, clinics and hours, sources) and Catalogues. Rules unchanged | Low |
| **M5d: Clinic profile** | A **Clinic profile** screen: names in English and Arabic, tagline, address, phones, email, and licence or registration line. Prescriptions and lab slips print from it instead of `ClinicInfo`; `ClinicInfo` stays only as the starting values until the profile is saved. The readiness warning is cleared once the profile is filled in. **Database:** one small new table, additive | Medium. The prescription and slip printouts are checked again at A5 and A4 |

Each slice follows the usual flow: I implement it, you run the script, check and push. Only M5d changes the database, with your approval as before.

## Decisions for you

1. **Where the clinic profile is kept.**
   - *Recommended:* **in the database**, edited on a Settings screen by clinic managers and Admin (*Settings.Clinics*), with no redeploy.
   - *Alternative:* keep `ClinicInfo` in the server settings, as today.
2. **The clinic's name on bilingual printouts.**
   - *Recommended:* **both an English and an Arabic name** (and address), so the bilingual printouts (M3b, M3c) show each in its language.
   - *Alternative:* one name only.
3. **Catalogue items in use.**
   - *Recommended:* **as today**: an item that was ever used is made inactive, not deleted, so history keeps its name. Inactive items can be made active again.
   - *Alternative:* allow deleting only unused items, and hide the delete button otherwise.

## Risks

| Risk | Handling |
|---|---|
| Catalogue changes affect booking (treatment types), lab requests and nutrition | The same rules move into one service with unit tests; the browser test already books, requests a lab test and creates a package from these lists |
| An old link to the Nutrition type screens | Redirects to the catalogue |
| Printouts change look | Checked at A5 and A4 in both languages; the print stylesheet is kept |

## Rollback

Each slice is reverted on its own by reverting its PR's merge commit. M5d's table can stay if reverted; printouts fall back to `ClinicInfo`.

## Next

Reply with your choices, or **"approve M5"** for the recommended ones. I'll then implement M5a.
