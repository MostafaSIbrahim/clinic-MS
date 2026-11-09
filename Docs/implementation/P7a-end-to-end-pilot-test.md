# Slice P7a: the pilot flow as an end-to-end test in CI

Pilot item **P7** (acceptance), slice a. It follows your answers:
- a **separate CI job, report-only at first**;
- **axe fails on serious or critical** violations;
- the **old flow's clicks, counted from the code**, as the baseline;
- a **script and a results sheet** for the usability check.

**P7b** follows. It fixes what this job reports (accessibility, layout), and adds the baseline comparison, the usability script and sheet, and the acceptance report.

**Claude implemented this slice at the owner's request.** The owner runs one command, does a short check, and pushes. **There's no app or database change.**

| | |
|---|---|
| Branch | `feature/p7a-e2e` (from the working copy, after PR #53 is merged) |
| What's added | **1. A browser test of the whole pilot visit** (`SafyaClinic/e2e`, Playwright 1.64.1 + axe-core 4.14.0), run as the real roles. The **front desk** registers a patient allergic to penicillin, books the doctor's first free time from the patient page, and checks them in from Appointments. The **doctor** sees them **Waiting** in My Queue, starts the consultation, writes the record (the header shows the allergy), and prescribes *Penicillin V*. The allergy warning appears, and saving is blocked until **Prescribe anyway** is ticked. The doctor then completes the visit. The **front desk** collects 500 of the 800 and doesn't see **Write off**. The **supervisor** writes off the rest as a Discount. **2. axe on every screen** (WCAG 2.1 A/AA): any **serious or critical** violation fails the test, and all of them are listed (the run carries on to the end). **3. Screen sizes:** the visited screens are reopened at **1366, 1024 and 390 px**. They're checked for sideways scrolling and with axe again. **4. Workflow measurements**, recorded on each run: clicks to book a known patient, and clicks from "patient arrives" to "checked in". **5. A new CI job, `e2e`.** It starts SQL Server 2022 in a container, builds and starts the app (Development settings, migrations applied to a fresh `SafyaClinicE2E` database, admin seeded), creates three test accounts through the Users screen (front desk, doctor, supervisor), and runs the test. **All passwords are generated for each run and masked in the logs; none are in the repository.** The run's **summary** (axe and screen-size findings, measurements) appears on the CI run page. The full report, traces of any failure, the JSON results and the app's log are attached as the **e2e-results** artifact |
| Report-only | The job has `continue-on-error`, so **a failing e2e doesn't block merging**. It may show a red mark on its own while the main CI job passes. Once it's green and stable (P7b), mark **e2e** as a required check (Settings → Branches → main → Require status checks) |
| Expected on the first runs | **axe findings are likely**, because this is the first time all the pilot screens are checked together, including at 390 px. That's what P7b is for. The test may also need a locator adjusted after its first real run. I'll read the report and fix both |
| Known limit | The doctor's free times must still exist today, so a run after about **23:30 Cairo time** fails at booking (the test says so) |
| Database | None (the test database exists only inside the CI run) |
| Files | Add `SafyaClinic/e2e/` (`package.json`, `package-lock.json`, `playwright.config.js`, `global-setup.js`, `lib/helpers.js`, `tests/pilot-flow.spec.js`, `.gitignore`). Edit `.github/workflows/ci.yml` (+ job `e2e`). Locally, `.assessment/slice.ps1` now also commits `.github` |

## Rollback

Revert the PR's merge commit. That removes the job and the folder.

---

## Your steps

After merging **PR #53**, from `D:\MyDevWork\clinic-MS`:

```powershell
.\.assessment\slice.ps1 -Branch feature/p7a-e2e `
  -Message 'P7a: end-to-end pilot flow (Playwright + axe, 1366/1024/390) as a report-only CI job'
```

There's no migration. The app starts as usual, but nothing in it has changed, so a quick look is enough. Press Enter, then type `YES` to commit and push.

### Then, on GitHub

1. Open the PR. There are now two jobs, **build-and-test** (as before) and **e2e**.
2. When **e2e** finishes, open it. The **Summary** section lists the screens with accessibility problems, any sideways scrolling, and the click counts.
3. Download the **e2e-results** artifact (bottom of the run page), zip it as-is, and attach it here. I'll use it for P7b.

Send me the PR number, both job results, and the artifact. The PR can be merged even if **e2e** is red.
