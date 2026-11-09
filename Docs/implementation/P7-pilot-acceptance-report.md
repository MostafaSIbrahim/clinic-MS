# P7: pilot acceptance report

Pilot (Group 3) acceptance for the front-desk visit flow: find or register patient → book a free time → check in → doctor queue → consultation → complete → collect or write off. Written 9 Nov 2026, after PR #54.

**Status: ready for owner acceptance.** The automated part is complete and green in CI. What remains is yours: the short usability session with 2–3 staff, marking the end-to-end check as required, and the sign-off below.

## Roadmap acceptance criteria (P7)

| Criterion | Result | Evidence |
|---|---|---|
| End-to-end test green in CI | **Met.** The whole visit passes as front desk, doctor and supervisor | CI job `e2e` on PR #54 (Playwright 1.64.1, fresh SQL Server 2022 database per run) |
| axe: zero serious or critical violations on the pilot screens | **Met.** 0 on every screen the flow visits, at 1366, 1024 and 390 px | Same job; failures print the element and the reason |
| Responsive checks at 1366 / 1024 / 390 | **Met.** No sideways scrolling on any pilot screen | Same job, *pilot screens at 1366, 1024 and 390 px* |
| Workflow metrics recorded, baseline against pilot | **Met for clicks** (below). **Time to book: pending** the usability session | Test measurements; `Pilot usability check` page |
| Owner acceptance | **Pending** | Sign-off at the end |

## §13 workflow metrics

| Metric | Baseline (old flow) | Pilot | Target | Result |
|---|---|---|---|---|
| Clicks from "patient arrives" to "checked in", existing booking, from the dashboard | **3**: Appointment Board → the booking → Check In on the reservation page | **2**: Appointments → Check in | −30 % | **−33 %, met** |
| Clicks to book a known patient | **7 clicks and 3 typed fields**: Reservations → New Reservation → pick patient → doctor → clinic → treatment → (date, time, typed) → Book. No free times shown; a clash was found only after saving | **4**: Book appointment → (visit type → Show) → a free time → Book. With the default visit, 3 | — | Fewer steps; only free times can be chosen |
| Time to book a known patient | not measured | from the usability session | < 60 s | **Pending** |

How the baseline was counted: from the code as it stood at the assessment (the commit before S1). The old dashboard listed today's reservations without actions. Check-in existed only on the reservation page, reached from the Appointment Board. Booking used the full reservation form.

## §13 correctness and quality metrics touched by the pilot

| Metric | Baseline (27 Oct) | Now |
|---|---|---|
| Visits completed without a medical record | possible | **impossible**: Complete visit works only from the visit's record (P5, P5c; tests) |
| "Covered" while a balance is due | possible (reproduced) | **impossible**: one coverage rule everywhere, and editing the price recomputes it (P6a; tests) |
| Overlapping doctor bookings accepted | possible | **blocked** under the doctor's booking lock (P2a; tests) |
| Duplicate primary phones | possible | **blocked** by the database and the service (F5b, P3) |
| Automated tests | 0 / no CI | about 440 .NET + JavaScript tests, plus the end-to-end pilot flow with axe in CI |

## Faults the end-to-end test found and fixed (P7a)

| Fault | Effect before the fix | Fix |
|---|---|---|
| Registering a patient without a city failed silently | The form came back with no message | An empty address row means no address; a started address asks for its city |
| A new database got only the free nutrition follow-up visit type | On the planned fresh hosted deployment, every visit would have cost 0 | The priced defaults are seeded beside it, unless the clinic has its own types |
| Queue filters had no accessible names | Screen readers couldn't name Clinic and Doctor | Labels linked to the fields |
| The remove-drug button had no name | Announced as just "button" | Hidden name "Remove this drug" |
| Collect's icon was read as part of its name | A screen-reader quirk | Icon marked as decoration |
| Screen-reader text in scrolling tables escaped the table | Patient payments scrolled sideways at 1024 and 390 px | Scrolling tables position their own contents |

## What's left for you

1. **Make the check required.** GitHub → Settings → Branches → `main` → *Require status checks* → add **e2e**. From then on, a change that breaks the pilot flow or its accessibility can't be merged.
2. **Run the usability session** with 2–3 staff using the **Pilot usability check** page. It holds the script, a form for each attempt, and a summary against the targets. Use test data only, and record codes and roles, not names. The main number to confirm is **booking a known patient in under 60 seconds** (T2).
3. **Decide on what the session shows.** Anything that blocks staff becomes a fix before go-live. Anything else goes into the screen migration (Group 4).
4. **Sign off** below. That closes gate **G3** (pilot accepted; §13 pilot metrics recorded).

### Known limits carried forward

- The browser test books today's first free time, so a run after about 23:30 Cairo time fails at booking. It runs on each PR, normally during the day.
- Allergy matching compares names, not drug classes (P5a).
- Write-offs cover visits, not nutrition enrollments (P6b).
- Repository hygiene still to do: the unused `GetAppointmentBoardAsync` and `AppointmentBoardDto`, three old nullable warnings, deleting `D:\MyDevWork\history-rewrite` once GitHub Support confirms the history purge, and the F34 owner decision.

## Sign-off

| | |
|---|---|
| Usability session held on | |
| Participants (codes, roles) | |
| T2 median time to book | |
| Decision | ☐ Accepted ☐ Accepted with fixes listed below ☐ Not accepted |
| Fixes required before go-live | |
| Owner | |
| Date | |

## Next

After acceptance:
- **the fresh hosted deployment** (the seeding fix above matters there);
- then **Group 4**, the screen migration, starting with **M1, the patient workspace**.
