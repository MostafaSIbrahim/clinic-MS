# Slice M3c: lab screens (closes M3)

Group 4, item **M3** (clinical screens), slice c. This slice **closes M3**. It follows the approved plan (`M3-clinical-screens-plan.md`): the lab screens are **restyled only**, and their rules stay as they are (D9). The request slip is bilingual, like the prescription (M3b).

**Claude implemented this slice at the owner's request.** The owner runs one command, does the manual check, and pushes.

| | |
|---|---|
| Branch | `feature/m3c-lab-screens` (from `main` after PR #69; your folder is on `main`) |
| Behaviour change | **1. Lab list** (menu *Analysis*): shared filter bar, a table that becomes cards on phones, status badges, an *Urgent* badge instead of a red dot, and Arabic. It still searches first. Each patient's name opens their Lab tab, and **Open** opens the request. **2. Request analysis:** the patient header with allergies, each analysis with its price and *Preparation*, *Urgent*, Arabic. Cancel goes back to the Lab tab. **3. The request page:** shared layout, the patient header, status badges, Arabic; *Print request form* and *Back to the record* buttons; the preparation is shown. The **delete** button for a results file now has a name for screen readers; before, it was a bare bin icon. **4. Fix: saving a new status no longer wipes the result.** Before, the result date and notes boxes always started empty, so changing only the status erased the saved result. They now start with the saved values. **5.** The status and result form and the upload are only shown to people who may change lab requests (*Analyses.Manage*). The server already refused everyone else. **6. The request slip is bilingual** whatever language the person printing uses: each heading in English with Arabic beside it. Analysis names and preparation are printed as set up. *Printed* uses the clinic clock; before, it used the server's clock. **7. Dates:** the request date on the list, the request page, the slip and the patient's Lab tab is the clinic's date. Before, it was the UTC date, so a request after 22:00 Cairo time showed the next day |
| Unchanged | The statuses, who can request or change them, the attachments and their limits, the search, and the slip's contents |
| Database | None |
| Tests | No new unit tests (layout and text). The browser test now goes further as the doctor: **request a Blood Test** from the Lab tab, see the slip, open the request, save **Completed** with result notes, then change only the status and check the notes are still there. It checks the request form, the slip and the request page for accessibility, and the list and request page at the three widths |

## Files

| Action | Path |
|---|---|
| Edit | `Web/Views/Analysis/Index.cshtml`, `Request.cshtml`, `Details.cshtml`, `PrintRequestSlip.cshtml` (rewritten) |
| Edit | `Web/Views/Patients/_PatientLab.cshtml` (request date in clinic time), `wwwroot/css/site.css` (`.analysis-choices`), `Resources/SharedResource.ar.resx` (+25) |
| Edit | tests: `e2e/tests/pilot-flow.spec.js` |

## Rollback

Revert the PR's merge commit.

---

## Your steps

In PowerShell:

```powershell
.\.assessment\slice.ps1 -Branch feature/m3c-lab-screens `
  -Message 'M3c: lab list, request, details and slip in the shared layout and Arabic; bilingual slip; saving a status keeps the result; closes M3'
```

### Manual check (while the app runs)

As a **doctor** (or Admin), with a test patient:

1. Patient → **Lab** tab → **Request analysis**. You see the patient header, each analysis with its price and preparation, and *Urgent*. Tick two, tick *Urgent*, request.
2. The **slip** opens. Headings are in English with Arabic beside them, *Printed* shows your local time now, and the date is today. Print preview fits one page.
3. Back on the Lab tab, **Open** one request. It shows the status badge, *Urgent*, the preparation and the patient header.
4. Set **Completed**, a result date and notes, then **Save**: they show under Details. Change only the status to **In Progress** and save. **The date and notes are still there.**
5. Upload a small PNG, preview it, then delete it. It asks first, as before.
6. Menu **Analysis**: it asks you to search first. Choose status **Completed** and search: the request is listed with badges, and the patient's name opens their Lab tab.
7. **Arabic:** the list, request form and request page are in Arabic. The slip looks the same (bilingual).
8. At **phone width**, the list becomes cards.

When you've finished, press Enter in the script, then type `YES` to commit and push. Open the PR, merge it, run `git switch main` and `git pull`, and send me its number and the CI result.

## M3 is complete

- **Records:** shared fields, allergies in view, Arabic (M3a).
- **Prescriptions:** labelled drug fields, a bilingual printout, typing kept across a language switch, and no lost drugs when a row is removed (M3b).
- **Lab:** the shared layout, Arabic, a bilingual slip, and results kept when the status changes (M3c).

**Next on the roadmap: M4, nutrition.** As before, I'll send the plan first.
