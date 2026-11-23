# Slice M5c: clinics, patient sources and the Settings home

Group 4, item **M5** (settings), slice c. Follows the approved plan (`M5-settings-plan.md`, approved 14 Nov 2026).

**Claude implemented this slice at the owner's request.** The owner runs one command, does the manual check, and pushes.

| | |
|---|---|
| Branch | `feature/m5c-clinics-sources-settings` (from `main` after the M5b PR) |
| Database | None |

## Behaviour change

**1. Clinics** (*Settings → Clinics*):
- **The list** uses the shared layout: a table that becomes cards on phones, an *Active* or *Inactive* badge, and named Open, Edit and Delete buttons. Delete asks first, and says that a clinic with bookings or payments is made inactive instead (as before).
- **Add and edit** use the shared fields with labels: name (required), address and phone. Edit has *Active (offered when booking)*.
- **The clinic page** has a header with *Edit clinic* and *All clinics*. Its **Patient source agreements** card explains what an agreement does, lists the agreements in a table with a named *Remove* button that asks first, and has a labelled form to add or change one. The source list shows each source's default deduction.
- **Opening hours:** the header and buttons match the other screens. The hours rules are unchanged.

**2. Patient sources** (*Settings → Patient Sources*):
- The list uses the shared table, with the default deduction, the number of patients and a status badge. Delete asks first.
- Add and edit use the shared fields with hints; edit has *Active (offered when registering patients)*.

**3. The Settings home.** The old *Lookups* page (a grid of cards) becomes **Settings**, in three groups, each link shown only to people who may use it:
- **People:** Users, Permission groups.
- **Clinic:** Clinics (details, hours, agreements), Patient sources.
- **Catalogues:** Treatment, Analysis, Injection and Vitamin types, each opening its tab of the catalogue editor (M5a).

The menu entry *Lookups* is renamed **Settings**. The clinic profile joins the Clinic group in M5d.

**4. Everything above is in Arabic and English** (+43 translations).

## One rule change, for your approval

**Deleting a patient source that patients came from.**
- **Before:** the source was deleted, and the database then **cleared it from every patient who came from it**. Their first-visit deduction would then use no source, and the patient's record no longer showed where they came from.
- **Now:** a source used by patients is **made inactive** instead, as one with payments already was. It's no longer offered when registering, but the patients keep it, and it can be made active again on its edit form.

If you'd rather keep the old behaviour, tell me and I'll revert that one change (in `PatientSourceService.DeleteAsync`).

## Unchanged

- Who can open each screen: Clinics with *Settings.Clinics* (Admin, Clinic admin); Patient sources and Settings with *Settings.PatientSources* and *Settings.Lookups* (Admin). The access baseline doesn't change.
- How agreements and default deductions are used for payments.
- The messages shown after saving (toasts) stay in English, as on the other screens; translating them is on the hygiene list.

## Tests

- **2 new unit tests:** a source used by a patient is made inactive and the patient keeps it; an unused source is deleted.
- **The localisation test** checks that every new text has an Arabic translation.
- **The browser test** checks Settings, Clinics, the seeded clinic's page and edit form, Patient sources and the add-source form for accessibility at the three widths.

## Files

| Action | Path |
|---|---|
| Add | `tests/.../Application/PatientSourceDeleteTests.cs`, this document |
| Edit | `Web/Views/Clinics/Index.cshtml`, `Create.cshtml`, `Edit.cshtml`, `Details.cshtml`, `Hours.cshtml`; `Web/Views/PatientSources/Index.cshtml`, `Create.cshtml`, `Edit.cshtml`; `Web/Views/Lookups/Index.cshtml` (rewritten as Settings); `Web/Navigation/NavigationCatalogue.cs` (menu label) and `tests/.../Web/NavigationCatalogueTests.cs`; `Web/wwwroot/css/site.css` (`.settings-groups`, `.settings-links`); `Web/Resources/SharedResource.ar.resx` (+43); `Application/Services/PatientSourceService.cs`; `e2e/tests/pilot-flow.spec.js` |

## Rollback

Revert the PR's merge commit.

---

## Your steps

In PowerShell, on the latest `main`:

```powershell
.\.assessment\slice.ps1 -Branch feature/m5c-clinics-sources-settings `
  -Message 'M5c: clinics, agreements and patient sources in the shared layout and Arabic; Settings home; a source used by patients is made inactive'
```

### Manual check (while the app runs)

As **Admin**:

1. **Settings** (menu): three groups, People, Clinic and Catalogues. Each link opens its screen; a catalogue link opens the right tab.
2. **Clinics:** the list shows the badges. Open a clinic:
   - add an agreement for a source with a deduction, then change its percentage with the same form;
   - remove it: it asks first.
3. **Edit clinic:** change the phone and save. **Hours** still opens and saves as before.
4. **Add clinic** with no name: it's refused with a message by the field.
5. **Patient sources:** add a test source, edit it, then delete it: being unused, it's deleted. Delete a source that a patient uses: it shows *Inactive* and the patient still shows it.
6. **Arabic:** all of these screens are in Arabic. At phone width, the tables become cards.

As **Clinic admin** (if you have such a test user): the menu shows Clinics but not Settings, as before.

When you've finished, press Enter in the script, then type `YES` to commit and push. Open the PR, merge it, run `git switch main` and `git pull`, and send me its number and the CI result.

## Next: M5d

The **clinic profile**: names in English and Arabic, tagline, address, phones, email and licence line, used by prescriptions and lab slips instead of the `ClinicInfo` server setting. One small new table, with your approval.
