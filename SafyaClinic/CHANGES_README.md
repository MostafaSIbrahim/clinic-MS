# SafyaClinic — Patient Source, Clinic & Payment Module Enhancements

This document summarizes the changes made to the solution to satisfy the four requirements:

1. Flexible, admin-managed **Patient Sources** (Vezeeta, Ekshef, Instagram, Facebook, Marketing Campaign, …) with a per-source deduction %.
2. **Clinics**, each with its own deduction agreement per patient source.
3. An enhanced **Payment module**: cancel payment, change payment amount, and automatic first-visit source/clinic deduction.
4. A **Payment Dashboard** with unpaid-completed, unpaid-pending, fully-paid, per-source, and per-clinic views.

## ⚠️ Before you build

This environment did not have the .NET SDK available, so I was not able to compile the project or
generate an EF Core migration. After pulling this code, run:

```bash
cd SafyaClinic.Infrastructure
dotnet ef migrations add AddClinicSourcePaymentEnhancements -s ../SafyaClinic.Web -p .
dotnet ef database update -s ../SafyaClinic.Web -p .
```

(or run it from `SafyaClinic.Web` if that's where your existing migrations were generated from — check
`SafyaClinic.Infrastructure/Migrations` for the existing pattern). The app also calls
`db.Database.Migrate()` + `DbSeeder.SeedAsync()` on startup, so once the migration exists the seed data
(a "Main Clinic" and six default patient sources) will be created automatically.

Please do a full `dotnet build` and review — I wrote this by hand without a compiler in the loop, so
double-check for typos before deploying.

## What was added

### Domain
- `Entities/Settings/PatientSource.cs` — admin-managed source list; `DefaultDeductionPercentage`, `IsActive`.
- `Entities/Settings/Clinic.cs` — clinic/branch entity.
- `Entities/Settings/ClinicSourceAgreement.cs` — per Clinic+Source deduction % (overrides the source's default for that clinic).
- `Entities/Payment/PaymentAdjustment.cs` — audit trail row created on every cancel / amount-change.
- `Enums/PaymentStatusEnum.cs` — `Active` / `Cancelled`.
- `Patient.cs` — added `PatientSourceId`.
- `Reservation.cs` — added `ClinicId` (now required on every reservation).
- `Payment.cs` — added `ClinicId`, `PatientSourceId`, `IsFirstVisitDeduction`, `DeductionPercentage`,
  `SourceDeductionAmount`, `ClinicNetAmount`, `Status`, `CancelledAt/By/Reason`, `OriginalAmount`,
  `LastModifiedAt/By`.

### Infrastructure
- New EF configurations for all new entities; updated `PatientConfiguration`, `ReservationConfiguration`,
  `PaymentConfiguration` for the new FKs/columns.
- `SafyaDbContext` — new `DbSet`s.
- `UnitOfWork` / `IUnitOfWork` — repos for `Clinics`, `PatientSources`, `ClinicSourceAgreements`, `PaymentAdjustments`.
- `DbSeeder` — seeds a default "Main Clinic" and 6 common patient sources (Walk-in, Vezeeta, Ekshef,
  Instagram, Facebook, Marketing Campaign) so the dropdowns aren't empty on first run.

### Application
- `DTOs/Settings/SettingsDtos.cs` — DTOs for sources, clinics, and clinic↔source agreements.
- `DTOs/Payment/PaymentDtos.cs` — extended with clinic/source attribution, `CancelPaymentRequest`,
  `ChangePaymentAmountRequest`, and the full `PaymentDashboardDto` family.
- `DTOs/Patient/PatientDtos.cs`, `DTOs/Reservation/ReservationDtos.cs` — added source/clinic fields.
- `Services/PatientSourceService.cs` (+ `IPatientSourceService`) — CRUD for sources. Deleting a source
  that already has payment history **deactivates** it instead of hard-deleting, to protect financial
  history; otherwise it's a real delete, satisfying "admin can add or delete any of them at any time."
- `Services/ClinicService.cs` (+ `IClinicService`) — CRUD for clinics + `UpsertAgreementAsync` /
  `RemoveAgreementAsync` for the per-clinic deduction matrix. Same soft-delete-if-in-use protection.
- `Services/PaymentService.cs` — rewritten:
  - **Deduction logic**: on `CollectPaymentAsync`, if this is the patient's first-ever *active* payment
    and the patient has a source, look up the `ClinicSourceAgreement` for (clinic, source); if none
    exists, fall back to the source's `DefaultDeductionPercentage`. Stores the % and computed amounts
    on the payment as a snapshot (so later source/agreement edits don't retroactively change history).
  - **CancelPaymentAsync** — requires a reason, flips `Status`, logs a `PaymentAdjustment`, reverses the
    reservation's paid status / enrollment's `TotalPaid`.
  - **ChangePaymentAmountAsync** — requires a reason, re-derives the source/clinic split proportionally,
    logs a `PaymentAdjustment`.
  - **GetPaymentDashboardAsync** — builds the four dashboard views + per-source/per-clinic totals.
- `Services/PatientService.cs`, `Services/ReservationService.cs` — wired the new `PatientSourceId` /
  `ClinicId` fields through create/update/mapping.
- DI registration for the two new services in `ApplicationServiceExtensions.cs`.

### Web
- **New**: `Controllers/PatientSourcesController.cs` (Admin) — list/create/edit/delete sources.
- **New**: `Controllers/ClinicsController.cs` (Admin) — list/create/edit/delete clinics + manage the
  per-source agreement matrix on the clinic's Details page.
- `Controllers/PaymentsController.cs` — rewritten: `Collect` now requires a clinic and shows the
  deduction outcome in the success message; new `Cancel`, `ChangeAmount`, and `Dashboard` actions.
- `Controllers/PatientsController.cs` — `Create`/`EditBasic` now include a Patient Source dropdown.
- `Controllers/ReservationsController.cs` — `Create`/`Edit` now require a Clinic dropdown.
- Views:
  - New: `Views/PatientSources/{Index,Create,Edit}.cshtml`
  - New: `Views/Clinics/{Index,Create,Edit,Details}.cshtml` (Details hosts the agreement matrix)
  - New: `Views/Payments/{Dashboard,Cancel,ChangeAmount}.cshtml`
  - Updated: `Views/Payments/{Collect,PatientSummary,Report}.cshtml` — clinic/source columns, deduction
    badge, cancel/change-amount row actions, link to the new dashboard.
  - Updated: `Views/Patients/{Create,EditBasic,Details}.cshtml`, `Views/Reservations/{Create,Edit}.cshtml`
  - `Views/Shared/_Layout.cshtml` — sidebar links for **Clinics**, **Patient Sources** (Admin only).

## How the deduction actually works (example)

1. Admin creates a source "Vezeeta" with a 20% default deduction.
2. Admin opens the "Main Clinic" clinic page and sets a clinic-specific agreement: Vezeeta → 15% at
   this clinic (overrides the 20% default just for this clinic).
3. Reception registers a new patient and sets their source to "Vezeeta".
4. Reception books the patient's first reservation at "Main Clinic" and collects a payment of 1,000.
   Because it's their first active payment ever, the system finds the Main Clinic↔Vezeeta agreement
   (15%), deducts 150, and records `ClinicNetAmount = 850`.
5. Any subsequent payment for that same patient — first or later visits — no longer qualifies for the
   "first visit" deduction, so it is collected in full with no deduction.
6. If reception collected the wrong amount, they can use **Change Amount** (with a reason) — the
   deduction split is recalculated proportionally, or **Cancel Payment** (with a reason) to void it —
   both are recorded permanently in the `PaymentAdjustments` audit table.

## UI/UX reskin (Telerik-inspired)

The whole app's look was updated to match telerik.com's design language: a deep violet/purple brand
(`#5B21B6` → `#7C3AED` gradient) with a teal accent, the **Inter** typeface, pill-shaped buttons with a
gradient primary + hover lift, rounded-16px cards with soft shadows, a clean light sidebar (instead of
the old dark one) with grouped section labels and a gradient logo mark, and a redesigned login screen.

Because almost every page is built from shared Bootstrap classes (`.card`, `.btn`, `.table`, `.badge`,
`.form-control`, `.stat-card`, …), the reskin was done almost entirely in two shared files so it
cascades to every screen automatically:

- `wwwroot/css/site.css` — completely rewritten design system (CSS variables, typography, buttons,
  cards, tables, forms, alerts, sidebar, stat-card gradients).
- `Views/Shared/_Layout.cshtml` — added the Inter Google Font, rebuilt the sidebar markup (light theme,
  gradient brand mark, grouped nav sections: Care / Billing / Administration), and restyled the top bar.
- `Views/Auth/Login.cshtml` — redesigned to match the new palette (radial violet gradient background,
  gradient logo badge).

No other view files needed changes for the reskin — they inherit the new look through the shared
classes. If you want, I can also do bespoke passes on individual pages (e.g. a more "Telerik demo site"
style data grid look for the Patients/Reservations tables, or a Telerik-style hero banner on the
Dashboard) — just say the word.

## Known follow-ups / things worth reviewing

- I could not compile this, so please run a full build and fix any typos before deploying.
- Consider whether "first visit" should be scoped per-clinic instead of globally per-patient — I
  implemented it globally (a patient's very first active payment anywhere), matching "first reservation
  only" in the request. Let me know if you actually want it per-clinic and I'll adjust.
- The `PaymentAdjustments` audit table isn't surfaced in the UI yet (no dedicated "payment history" view)
  — the reason text is currently folded into the payment's `Notes` field and the underlying table, but
  there's no screen listing adjustments per payment. Happy to add that if useful.
- `Reservations/Index.cshtml`, `Today.cshtml`, and `Details.cshtml` were **not** updated to display the
  new Clinic column (I focused on Create/Edit + Payments/Patients, which were the ones this feature
  actually depends on) — let me know if you'd like those updated too.
