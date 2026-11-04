# P1: Scheduling design (clinic hours and doctor schedules)

Pilot item **P1** (Group 3). Implements **D7** and target-state **§15**. This document records the owner's decisions of **2026-11-04** and the design all three P1 slices build on. P2 (slot booking) uses the same model.

## Owner decisions (2026-11-04)

| # | Question | Decision |
|---|---|---|
| S1 | More than one working period in a day? | **Yes, several periods**, for clinics and doctors (e.g. 10:00–14:00 and 17:00–21:00). This also covers one doctor in two clinics on the same day |
| S2 | How open calendars are cut into slots | **Each doctor has a step** (e.g. 15 min). **A visit takes its treatment's length** (a 30-min treatment uses two steps); the next free start times are offered |
| S3 | How specific-slots doctors define slots | A **weekly pattern plus date changes**: slots per weekday, then add or remove slots or take a day off on particular dates |
| S4 | A doctor with no schedule yet, once slot booking is live | **Bookable within the clinic's opening hours** (as an open calendar with the default step) until a schedule is set, so the front desk is never blocked |

These come in addition to the earlier decisions (2026-10-27): no double booking; only current and future free slots; clinic opening hours per clinic and weekday; doctor breaks; removing booked availability prompts a reschedule; one doctor in several clinics on the same day in non-overlapping times.

## Model

All times are **clinic (Cairo) time**, as reservations already store them (`ReservationDate` + `ReservationTime` + `DurationMinutes`).

| Table | Columns | Slice |
|---|---|---|
| `ClinicOpeningHours` | ClinicId, DayOfWeek, OpensAt, ClosesAt (`time(0)`, check Opens < Closes). Up to 3 periods a day; a day with none is closed | **P1a** |
| `DoctorSchedules` | DoctorId, ClinicId (unique pair), Mode (`Open` / `SpecificSlots`), StepMinutes (default 15), IsActive | P1b |
| `DoctorScheduleTimes` | ScheduleId, DayOfWeek, Kind (`Working` / `Break` / `Slot`), StartTime, EndTime. **Open:** working periods and breaks. **Specific slots:** each slot (or a range cut into steps) | P1b |
| `DoctorScheduleChanges` | ScheduleId, Date, Kind (`DayOff` / `Working` / `Break` / `Slot` / `RemoveSlot`), StartTime?, EndTime?. Date changes override the weekly pattern for that date | P1c |

## Rules

| Rule | Where it's enforced |
|---|---|
| A doctor's working times **never overlap across clinics** (weekly pattern and date changes together) | Save of a doctor schedule (P1b/P1c) |
| A doctor's times must lie **inside the clinic's opening hours** | Save (P1b); clinic-hour changes warn when a schedule would fall outside (P1c) |
| Breaks lie inside working periods; slots lie inside opening hours | Save (P1b) |
| **Removing availability that holds future bookings** can't be saved until each booking is moved or cancelled | P1c lists the affected reservations and blocks the save; the move itself uses P2's reschedule |
| Who edits schedules | `Schedule.ManageOwn` (a clinician, their own schedule) and `Schedule.ManageAll` (granted to Administrators and Reception Admin by default; changeable on the Permission Groups screen) | P1b |
| Free slots for a date | One pure function: (clinic hours, doctor schedule, date changes, existing reservations, treatment length, now) → start times. **No past times.** Used by P2 for both display and the server check | P1c (function and tests), P2 (screens) |

## Slices

| Slice | Content | Database |
|---|---|---|
| **P1a** | Clinic opening hours: rules, service, edit screen, clinic page summary, audit, Arabic | `ClinicOpeningHours` |
| **P1b** | Doctor schedules: mode, step, weekly working periods, breaks or slots per clinic; permissions `Schedule.ManageOwn` / `ManageAll`; "My schedule" for clinicians and "Doctor schedules" for the front desk | `DoctorSchedules`, `DoctorScheduleTimes` |
| **P1c** | Date changes (day off, extra or removed slots), the free-slot function, the booked-availability guard | `DoctorScheduleChanges` |

Existing booking is **unchanged during P1**. Schedules start to limit booking in **P2**.
