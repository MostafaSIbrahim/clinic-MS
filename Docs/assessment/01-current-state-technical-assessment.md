# 01 — Current-State Technical Assessment (Phase 1: Discover)

| | |
|---|---|
| Repository | `MostafaSIbrahim/clinic-MS`, branch default, HEAD `4704889` (2026-09-26, "UI/UX modificationsAnd some user roles modifications"), 150 commits |
| Solution | `SafyaClinic/SafyaClinic.sln` |
| Assessed on | 2026-10-26 |
| Method | Static, read-only review of a fresh clone. Every C# file outside `Migrations/` was inventoried; controllers, startup, auth, DI, persistence, file handling, and integration code were read line by line; services were read in the areas cited and pattern-scanned elsewhere. |
| Runtime evidence | Build, tests, package audit and read-only database metadata collected on the developer machine on 2026-10-26 via `.assessment/collect.ps1` — see §15. The app was not run in a browser. |

Confidence labels used throughout: **Verified** (read in source), **Likely** (strong evidence, not executed), **Needs confirmation** (depends on runtime, DB, or intent).

Secrets policy: credential values were detected by length only and never printed or copied.

---

## 1. Solution and projects

| Project | Target | Role | References | Notable packages |
|---|---|---|---|---|
| `SafyaClinic.Domain` | net8.0 | Entities, enums, repository interfaces | — | EF Core Abstractions 8.0.28 |
| `SafyaClinic.Application` | net8.0 | DTOs, service interfaces, business services | Domain | EF Core 8.0.28 + Relational, FluentValidation 11.8.0 (unused), JWT 8.19.1 |
| `SafyaClinic.Infrastructure` | net8.0 | `SafyaDbContext`, configurations, migrations, repositories, DI | Application, Domain | EF Core SqlServer 8.0.28 |
| `SafyaClinic.Web` | net8.0 | MVC controllers, Razor views, static assets, startup | all three | `Microsoft.AspNetCore.Authentication.Cookies` **2.3.11** (legacy 2.x package; already in the shared framework) |
| `tests/PaymentAccessChecks` | net8.0 console | Reflection-based checks for `PaymentsController` | Web | **Not in the .sln** |
| `tests/DeploymentChecks` | net8.0 console | Offline check: compiled migrations, pending model changes, idempotent script | Infrastructure | **Uncommitted, local only**; not in the .sln |

Dependency direction is nominally Clean Architecture, with these deviations (**Verified**):
- Application depends on EF Core directly and calls `ToListAsync`, `ExecuteUpdateAsync`, `ExecuteDeleteAsync` on `IQueryable` returned by `IRepository<T>.Query()` (`Domain/Interfaces/Repositories/IRepository.cs:14`).
- `ApplicationServiceExtensions` (namespace `SafyaClinic.Application.DependencyInjection`) physically lives in `SafyaClinic.Infrastructure/DependencyInjection/`.
- `LookupsController`, `RolesController`, `UsersController` inject `SafyaDbContext` directly from the Web layer, bypassing services.
- `DbSeeder` (Infrastructure) calls `AuthService.HashPassword` (Application).

Repository contents outside the solution: `SafyaClinicPublish/` (29 MB of compiled publish output, including `appsettings.json`), `backup/Views/`, `P-Analysis/`, several `.docx` design/plan documents, `Docs/Performance-baseline.md`, `SafyaClinic/docs/UIUX-*.md|html`, and a root-level `safyaclinic.runasp.net-WebDeploy.publishSettings`.

Size: 18 controllers (158 action methods), 85 Razor views, 33 entity classes, 31 tables, 13 migrations, ~15k lines of non-migration source.

## 2. Entry point, hosting, configuration

`SafyaClinic.Web/Program.cs` (**Verified**):

| Order | Pipeline element | Notes |
|---|---|---|
| startup | `db.Database.Migrate()` then `DbSeeder.SeedAsync(db)` (lines 60–67) | Schema migrations run automatically on every app start, in every environment |
| 1 | `UseExceptionHandler("/Home/Error")` + `UseHsts()` | Non-Development only |
| 2 | `UseHttpsRedirection`, `UseStaticFiles`, `UseRouting`, `UseSession` | |
| 3 | `UseAuthentication`, `UseAuthorization` | |
| 4 | `UseMiddleware<ExceptionHandlingMiddleware>()` (line 85) | Registered **after** auth, although the comment on line 69 says it must come before |
| route | `{controller=Home}/{action=Index}/{id?}` | Conventional routing; two attribute-routed API controllers |

Hosting: in-process ASP.NET Core; local profiles on `http://localhost:5070` / `https://localhost:7131` (`Properties/launchSettings.json`). A Web Deploy profile targets `safyaclinic.runasp.net` (`Properties/PublishProfiles/site83728-WebDeploy.pubxml`) — i.e. a shared-hosting production or staging site exists. **Needs confirmation:** which environment is production and its server time zone.

Configuration (`appsettings.json`, **Verified**, values masked): `ConnectionStrings:SafyaClinicDb` (Windows integrated auth, `TrustServerCertificate=True`), `ClinicInfo:*` (placeholder address/phone used on printouts), `EgyptianDrugIndex:*`, `FileStorage:BasePath = wwwroot/uploads`, `FileStorage:MaxFileSizeBytes` (**never read by code**), `Jwt:Key|Issuer|Audience`, `AllowedHosts: *`. `appsettings.Development.json` sets EF command logging to `Information` and a flag `EnableSensitiveDataLogging` that no code reads. No user-secrets or environment-specific production file.

## 3. Web layer: controllers and routes

All state-changing actions are `[HttpPost]` with `[ValidateAntiForgeryToken]` (**Verified** across all 158 actions). Default authorization is opt-in: there is no fallback policy, so any new controller without `[Authorize]` is anonymous.

| Controller | Class-level authorization | Capability | Key actions (method-level policy) |
|---|---|---|---|
| `AuthController` | none (per action) | Login by phone + password, logout, change password, access denied | |
| `HomeController` | none | Public landing page (`Home/Index`, Arabic/English, standalone), `Error` | |
| `DashboardController` | `[Authorize]` | Today's reservations, pending/unpaid counts, today's revenue | |
| `PatientsController` | `ClinicalStaff` | Search, create, details, edit basic, phones, addresses | `Timeline`, `EditMedical` → `DoctorOrAdmin` |
| `ReservationsController` | `ClinicalStaff` | List, today, board, details, create/edit, status, mark paid, cancel, history report (Arabic, standalone) | `Queue` → Roles `Admin,Reception,Doctor`; `CheckIn` → `ReceptionOrAdmin`; `StartConsultation` → `DoctorOrAdmin` |
| `FollowUpsController` | Roles `Admin,Reception,Doctor` | Pending follow-ups list, book, dismiss | |
| `MedicalRecordsController` | `DoctorOrAdmin` | Records, treatments, prescriptions + items, attachments, print prescription, consultation, complete consultation | `Lock`, `DeleteAttachment` → `AdminOnly`; `UploadPrescriptionAttachment` → `ClinicalStaff` (see §6) |
| `AnalysisController` | `DoctorOrAdmin` | Lab requests (single/batch), status, attachments, print slips | `UploadAttachment` → `ClinicalStaff`; `DeleteAttachment` → `AdminOnly` |
| `NutritionController` | `NutritionTeam` | Packages, enrollments, weekly follow-ups | Package and injection/vitamin type CRUD → `AdminOnly` |
| `PaymentsController` | `[Authorize]` | Patient summary, audit (standalone), report, dashboard + drill-down, collect, cancel, change amount, maintenance jobs | `PaymentStaff`, `PaymentReports`, `AdminOnly` per action |
| `LookupsController` | Roles `Admin` | Analysis/treatment/injection/vitamin types | Uses `SafyaDbContext` directly; binds domain entities |
| `UsersController`, `RolesController`, `ClinicsController`, `PatientSourcesController` | `AdminOnly` | User/role admin, clinics + source agreements, patient sources | |
| `Api/PatientsApiController` | `ClinicalStaff` | `GET api/patients/search` (reservation patient picker) | |
| `Api/MedicinesController` | `ClinicalStaff` | `GET api/medicines/search` (prescription autocomplete) | |

The full route and screen inventory is a Phase 2 deliverable (`05-route-and-screen-inventory.md`).

## 4. Authentication and authorization

**Authentication (Verified)** — cookie scheme `SafyaClinic.Auth`, HttpOnly, SameSite=Strict, 8-hour sliding expiry, persistent (`Program.cs:25–36`, `AuthController.cs:57–64`). Users sign in with **phone number + password** (`AuthService.LoginAsync`). Roles are copied into the cookie at login.

- Password hashing is a **single SHA-256 over password+16-byte salt** (`AuthService.cs` `HashPassword`/`VerifyPassword`). The code comment claims PBKDF2/Identity v3 compatibility; it is not.
- No lockout, throttling, or failed-login logging.
- No password policy: `ChangePasswordRequest` and `CreateUserRequest` have no validation attributes, and FluentValidation has zero validators in the codebase.
- A JWT is generated on every login (`GenerateJwtToken`) but never used; JWT bearer auth is not registered.
- Deactivating a user (`UserService.SetUserActiveAsync`) or removing a role does not affect an already-issued cookie; with sliding expiry, an active session can persist indefinitely.
- The seeder creates the initial admin with a **hard-coded password in source** (`DbSeeder.cs:56`) when the `Users` table is empty.

**Authorization (Verified)** — seven named policies in `Program.cs:38–46`, plus inline `Roles = "..."` on three controllers and ~50 `User.IsInRole("...")` string checks in views and `BaseController`.

| Policy | Roles |
|---|---|
| `AdminOnly` | Admin |
| `DoctorOrAdmin` | Admin, Doctor |
| `ReceptionOrAdmin` | Admin, Reception |
| `NutritionTeam` | Admin, Nutritionist |
| `ClinicalStaff` | Admin, Doctor, Nutritionist, Reception |
| `PaymentStaff` | Admin, Reception, ReceptionAdmin |
| `PaymentReports` | Admin, Reception, ReceptionAdmin, Doctor |

Roles seeded (`DbSeeder.SeedRolesAsync`): Admin, Doctor, Reception, Patient, Nutritionist. `ReceptionAdmin` is referenced by policies and views but not seeded (presumably created through `RolesController`). **Gap against the stated user groups:** Clinic Management, Lab Doctor, Lab Tech and Lab Reception have no corresponding role, policy, or screen. Admins can create new roles in the UI, but because access is decided by hard-coded role names, a new role grants nothing. The `Patient` role exists with no patient-facing feature. `UserService.GetDoctorsAsync` identifies doctors by magic role IDs `2` and `5`.

Method-level policies are additive to class-level ones in ASP.NET Core, so `[Authorize(Policy="ClinicalStaff")]` on `AnalysisController.UploadAttachment` and `MedicalRecordsController.UploadPrescriptionAttachment` does **not** widen access beyond the class's `DoctorOrAdmin`. **Needs confirmation:** whether Reception/Nutritionist were meant to upload.

Resource-level checks exist in places: doctors are pinned to their own queue (`ReservationsController.Queue`, lines 87–88) and record creation/consultation checks `DoctorId` against the current user (`PatientRecordService.cs:59–91, 256, 308`). `UpdateRecordAsync`, treatment, and prescription-item edits take no user parameter, so any doctor can edit any unlocked record. **Confirmed as intended behaviour** by the product owner (2026-10-26).

## 5. Application services and business logic

Ten scoped services registered in `ApplicationServiceExtensions`. Largest files (complexity hotspots): `PaymentService.cs` 1,303 lines, `ReservationService.cs` 1,262, `PatientRecordService.cs` 923, `NutritionService.cs` 876, `PatientService.cs` 529.

| Service | Capability |
|---|---|
| `AuthService` | Login, change password, current user |
| `UserService` | Create users, list, doctors, activate/deactivate, assign/remove roles |
| `PatientService` | Patient CRUD, phones, addresses, dashboard, timeline; duplicate check on National ID only |
| `ReservationService` | Reservations, board, queue (check-in → in consultation → finished), consultation context/completion, follow-up booking, status changes, cancellation |
| `PatientRecordService` | Medical records, lock, treatments, prescriptions/items/attachments, print data, pending follow-ups |
| `AnalysisService` | Lab requests (single/batch), status, attachments, types |
| `PaymentService` | Collection, cancel, change amount with adjustments trail, due amounts, source/clinic deduction %, dashboard, line drill-down, revenue, audit, zero-cost backfill, paid-status recalculation |
| `NutritionService` | Packages and items, enrollments, weekly follow-ups with administered items and lab results, injection/vitamin type CRUD |
| `ClinicService`, `PatientSourceService` | Clinics, source agreements, patient sources |
| `EgyptianDrugService` | Medicine search via pluggable `IEgyptianDrugSource` |

Results flow through a `ServiceResult` / `ServiceResult<T>` envelope (`DTOs/Common/ServiceResult.cs`). Several controller actions discard the result (e.g. `AnalysisController.UpdateStatus`, `UploadAttachment`, `ReservationsController.MarkPaid`, `UsersController.ToggleActive`), so failures are silent.

Concurrency: critical operations use SQL Server application locks via `sp_getapplock` inside a transaction (`UnitOfWork.ExecuteLockedAsync`, used by payment collection, reservation status changes, follow-up booking, and record creation). There is no `rowversion`/concurrency token on any entity. No doctor time-slot overlap check was found in `ReservationService` (**Likely**).

Status modelling: reservation status is a lookup table (`ReservationStatuses`, seeded IDs from 1) and is matched by name strings (`"Pending"`, `"Completed"`, … 23 occurrences in `ReservationService.cs`, plus `StatusId = 1` at line 71). `ReservationStatusEnum` exists but is unused. Queue state is a separate enum `PatientQueueStatus` on the reservation.

Background jobs: none (no hosted services). Admin-triggered batch jobs: `RecalculateAllPaidStatuses`, `BackfillZeroCostPayments`.

## 6. File storage

**Verified** in `AnalysisController.UploadAttachment` and `MedicalRecordsController.SaveFileAsync` (duplicated code):
- Files are written to `FileStorage:BasePath` = `wwwroot/uploads/{analysis|prescriptions}`, a path relative to the process working directory. Because `UseStaticFiles()` serves `wwwroot`, stored medical attachments are reachable **without authentication** at `/uploads/...` by anyone who has the URL (GUID file names).
- The extension comes from the client filename and the content type from the client header; there is no allow-list, size limit, or content sniffing. Files are later served inline by `ViewAttachment` with the stored content type.
- The absolute server path is persisted in the database.
- One uploaded analysis image is committed to git in both `SafyaClinic.Web/wwwroot/uploads/analysis/` and `SafyaClinicPublish/wwwroot/uploads/analysis/` (not opened; may be patient data).

## 7. Persistence and data model

EF Core 8 code-first, SQL Server, single `SafyaDbContext` with 31 `DbSet`s and 27 `IEntityTypeConfiguration` classes applied via `ApplyConfigurationsFromAssembly`. Access goes through `UnitOfWork` exposing one `GenericRepository<T>` per entity plus two custom repositories (`NutritionPackageRepository`, `WeeklyFollowUpRepository`). `IRepository<T>` is also registered directly in `Program.cs:22`.

Tables by area: Identity (`Users`, `Roles`, `UserRoles`); Patients (`Patients`, `PatientPhones`, `PatientAddresses`); Scheduling (`Reservations`, `ReservationStatuses`); Clinical (`PatientRecords`, `Treatments`, `TreatmentTypes`, `Prescriptions`, `PrescriptionItems`, `PrescriptionAttachments`); Lab (`AnalysisTypes`, `MedicalAnalyses`, `AnalysisAttachments`); Billing (`Payments`, `PaymentAdjustments`, `PatientSources`, `Clinics`, `ClinicSourceAgreements`); Nutrition (`InjectionTypes`, `VitaminTypes`, `NutritionPackages`, `PackageItems`, `PatientNutritionEnrollments`, `WeeklyFollowUps`, `WeeklyFollowUpLabResults`, `WeeklyAdministeredItems`); `AuditLogs` (**table exists, never written**).

Schema observations from `SafyaDbContextModelSnapshot.cs` (**Verified**):
- Three duplicate relationships created shadow FK columns: `MedicalAnalyses.AnalysisTypeId1`, `WeeklyAdministeredItems.PackageItemId1`, `Prescriptions.PatientRecordId` (alongside `RecordId`). Cause: relationships configured both in `OnModelCreating` and in configuration classes / navigation conventions.
- Unique indexes: Users.PhoneNumber, Users.Email (filtered), Patients.NationalId (filtered), PatientRecords.ReservationId (filtered), role/status/clinic/source names, `(ClinicId, PatientSourceId)`, `(UserId, RoleId)`.
- Composite indexes for queue, doctor schedule, payments `(Status, PaymentDate)`, follow-ups.
- No index on patient phone numbers; patient search uses `Contains` on names and National ID (`PatientService.cs:313–315`).
- Delete behaviours: 20 Cascade, 18 Restrict, 3 NoAction, 2 SetNull — cascade targets on clinical data to be reviewed in Phase 3.
- No concurrency tokens.

Migrations: 13, from `20260620221656_InitialMigrationCreateDBwuthDbSeed` to `20260919210058_AddPatientFollowUpTracking`; two hand-named ones (`20260810120000_…`, `20260906120000_…`). Runtime seeding (`DbSeeder`) runs alongside a commented-out `HasData` seed (`SafyaDbContext.SeedData`, dead code).

Time handling: entities store `DateTime.UtcNow` (84 uses), but "today" filters use server-local `DateTime.Today` (21 uses), e.g. `DashboardController.cs:33` revenue for today and `ReservationService.cs:352, 458, 502, 564`. Only `PaymentsController.Report` converts correctly with `Africa/Cairo` (`PaymentsController.cs:54–107`). Views such as `Payments/PatientSummary.cshtml:158` print UTC times unconverted. The development SQL Server reports time zone `(UTC+02:00) Cairo` (§15), and the web server runs on the same laptop. Locally, "today" boundaries are therefore Cairo-local while stored timestamps are UTC, so the dashboard's "today's revenue" window is shifted by the UTC offset (**Verified** from code plus server time zone; not yet observed in the UI). The hosted demo's time zone is unknown.

## 8. External integrations

| Integration | Where | Behaviour |
|---|---|---|
| DwaPrices medicine index | `DwaPricesEgyptianDrugSource`, selected when `EgyptianDrugIndex:Provider = DwaPrices` (current config) | HTTPS GET to `dwaprices.com/sample-api.php` (a free sample endpoint), 8 s timeout, optional API key header (currently empty). Failures degrade to no suggestions, but the empty result is **cached for 60 minutes** (`EgyptianDrugService.cs`). Unbounded `IMemoryCache`. |
| Local medicine seed | `LocalEgyptianDrugSource` + `Data/egyptian-medicines-seed.json` | Fallback provider |
| `HttpEgyptianDrugSource` | Application | Not registered anywhere (dead code) |
| CDNs | `_Layout.cshtml`, `_ValidationScriptsPartial.cshtml` | Bootstrap 5.3.3, Bootstrap Icons 1.11.3, Font Awesome 6.5.1, jQuery 3.7.1 + validation, Google Fonts (Inter) — no Subresource Integrity |
| Hosting | Web Deploy to `site83728.siteasp.net` | Manual publish from Visual Studio |

No email, SMS, payment gateway, or lab-system integration exists.

## 9. Frontend

- **Server-rendered ASP.NET Core MVC + Razor**, Bootstrap 5.3.3 from CDN, vanilla JS plus jQuery only for unobtrusive validation. No bundler, npm, or build step. `wwwroot/lib` holds 7.9 MB of local Bootstrap/jQuery copies used by only one view (`PatientReservationHistoryReport` → `bootstrap.rtl`).
- Shell: `_Layout.cshtml` — collapsible offcanvas sidebar (role-filtered sections Care / Billing / Administration), top bar with page title and date, skip link, TempData alert area. Six views are standalone HTML documents (`Layout = null`): landing page, print prescription, print lab slip, payment audit, reservation history report, create-success popup.
- CSS: `site.css` (674 lines) defines 25 custom properties (brand palette, radius, shadows, sidebar width, semantic colours), 22 `!important`, 10 media queries, `:focus-visible` styles and a `prefers-reduced-motion` block. `branding.css` for the logo. `site-back.css` is unreferenced. Views contain 59 inline `style=""` attributes, 5 `<style>` blocks, and 61 distinct hard-coded hex colours. Reservation status colours come from the database (`ReservationStatuses.ColorCode`) through `_StatusBadge` inline styles.
- Icons: Bootstrap Icons (347 uses) and Font Awesome (7 uses) are both loaded.
- Language: `<html lang="en" dir="ltr">`; Arabic text appears in 5 views (lookups, landing page, history report). `Payments/Unpaid.cshtml` and `Payments/Report.cshtml` are not UTF-8 encoded.
- JS: `site.js`, `medicine-autocomplete.js`, `reservation-patient-picker.js`, `contact-modal-forms.js`; inline scripts in 21 views; 16 `confirm()` dialogs; dynamic HTML via `innerHTML` in several views (escaping present in the autocomplete; Razor-generated option lists embedded in JS template literals in `Nutrition/CreatePackage.cshtml:112–113`).
- Tables: 35 tables in 32 views, 22 `.table-responsive` wrappers.
- Prior UI work is documented in `SafyaClinic/docs/UIUX-Implementation-Guide.md`, `UIUX-Fixing-Guide-2026-09-24.md`, `UIUX-Next-Step-Reservation-Workflow-2026-09-24.md`.

## 10. Tests, CI/CD, observability, error handling

- **Tests:** `tests/PaymentAccessChecks` (30/30 checks pass), `tests/DeploymentChecks` (passes: 13 compiled migrations, no pending model changes), `tests/reservation-patient-picker.test.cjs` (5/5 pass). All are hand-rolled console/Node runners outside the solution; there is no xUnit/NUnit/MSTest project and no service, data-access or UI test.
- **CI/CD:** none (no `.github/`, pipelines, or Dockerfile). Deployment is manual Web Deploy; compiled output is committed in `SafyaClinicPublish/`.
- **Logging:** default ASP.NET Core console logging. `ILogger` is used only in `ExceptionHandlingMiddleware` and the drug-index classes; no structured logging, correlation IDs, health checks, or metrics.
- **Error handling:** `ExceptionHandlingMiddleware` logs, stores `ex.Message` in session, and redirects to `/Home/Error`, which displays that raw message to the user (`HomeController.Error`, `Views/Shared/Error.cshtml`). The session value is never cleared, so later error pages can show a stale message. 19 `catch` blocks across Application/Web.
- **Audit:** payment changes are traceable (`PaymentAdjustments`, cancellation fields, `Payments/Audit`); clinical and administrative changes are not audited (`AuditLogs` unused).

## 11. Documentation drift

`Safya Clinic Management System.docx` lists AutoMapper and FluentValidation in the stack; AutoMapper is not referenced and FluentValidation has no validators. `Docs/Performance-baseline.md` states the generic repository lacks `IQueryable` passthrough; `IRepository<T>.Query()` provides it. `DBScheme.docx` describes a hand-written SQL schema (`RoleId`, `UserId` keys) that differs from the EF-generated schema (`Id` keys). The code, not these documents, should be treated as the source of truth.

## 12. Observations to carry into Phase 3

Listed for traceability only; these are not yet evaluated or prioritised.

| # | Observation | Evidence | Confidence |
|---|---|---|---|
| O1 | Web Deploy password committed in repo root and history | `safyaclinic.runasp.net-WebDeploy.publishSettings` (`userPWD` populated) | Verified |
| O2 | JWT signing key committed (twice) | `SafyaClinic.Web/appsettings.json`, `SafyaClinicPublish/appsettings.json` | Verified |
| O3 | Default admin password hard-coded in seeder | `DbSeeder.cs:56` | Verified |
| O4 | Medical attachments publicly served from `wwwroot`; no type/size validation | §6 | Verified (public reachability: Likely, confirm in browser) |
| O5 | Weak password hashing, no lockout, no password policy | §4 | Verified |
| O6 | Raw exception messages shown to users | `ExceptionHandlingMiddleware.cs:117`, `HomeController.cs:19` | Verified |
| O7 | Migrations auto-applied on startup in all environments | `Program.cs:60–64` | Verified |
| O8 | UTC storage vs local-day filters | §7 | Verified (server TZ Cairo) |
| O9 | Hard-coded role strings; roles created in the UI grant nothing. Lab/management roles **deferred to the enhancements phase** by decision | §4 | Verified |
| O10 | Deactivation/role removal not enforced on live sessions; no self/last-admin guard | `UserService.cs` `SetUserActiveAsync`, `RemoveRoleAsync` | Verified |
| O11 | Shadow FK columns from duplicate mappings | §7 | Verified |
| O12 | No automated tests for services; no CI | §10 | Verified |
| O13 | An uploaded attachment is committed to git (dev/test data per owner) | §6 | Verified; low sensitivity |
| O14 | Silent failures where `ServiceResult` is discarded | §5 | Verified |
| O15 | Lookups success messages written to `TempData["SuccessMessage"]` but `AnalysisTypes`/`TreatmentTypes` views and the layout read `TempData["Success"]` | `LookupsController.cs:44, 74` | Likely |
| O16 | Drug index failures cached as empty for 60 min; production uses a sample endpoint | §8 | Verified |
| O17 | Migration history drift: DB contains applied migration `20260725134425_PatientSourceAndPaymentOptions`, absent from the code and from all git history | §15 | Verified |
| O18 | Vulnerable transitive packages (High) caused by the legacy `Microsoft.AspNetCore.Authentication.Cookies` 2.3.11 reference | §15 | Verified |
| O19 | Cascade deletes from `Users` to `Payments` (via `CollectedBy`) and `PatientNutritionEnrollments` (via `DoctorId`); from `Patients` to enrollments; from `NutritionPackages` to enrollments → follow-ups → lab results | §15 | Verified (latent: no user/patient delete action exists today) |
| O20 | Odd column defaults left by migrations: `Payments.Status` `''`, `Prescriptions.PrescriptionDate` `0001-01-01`, `Reservations.TreatmentTypeId` `1` | §15 | Verified |

## 13. Questions — answered 2026-10-26

| # | Question | Answer |
|---|---|---|
| 1 | Read-only database access | Granted; collected via script (§15) |
| 2 | Build and tests on the developer machine | Granted; done (§15) |
| 3 | Environments / data sensitivity | All environments are dev/test; no real patient data. `safyaclinic.runasp.net` is not production |
| 4 | Lab and management roles | New idea; **deferred to the enhancements phase** |
| 5 | Cross-doctor edits of unlocked records | **Intended** — any doctor may edit any unlocked record |

**Decision recorded 2026-10-26 — public demo access.** The demo deployment guide (`SafyaClinic/docs/MonsterASP-Demo-First-Publish.md`) intentionally states the seeded admin password because the public demo is meant to include admin access. Accepted for now. Follow-up for the roadmap (enhancements): design a safer public-demo model that still showcases admin features — e.g. a dedicated demo-admin account that cannot change its own password or manage users, plus a scheduled demo-data reset.

Still open, non-blocking: the hosted demo's time zone; whether the GitHub repository is or was public.

## 14. Next phase

Phase 2 will produce, in `docs/assessment/`: `02-solution-map.md`, `03-project-responsibility-and-dependency-map.md`, `04-business-capability-map.md`, `05-route-and-screen-inventory.md`, `06-data-model-summary.md`, `07-authn-authz-summary.md`, `08-external-integrations.md`, `09-risks-unknowns-questions.md`. The data model summary will use the verified database metadata from §15.

## 15. Runtime and database verification (2026-10-26)

Collected by `.assessment/collect.ps1` on the developer laptop (Windows, SQL Server 2025 Developer Edition). Read-only: builds wrote only `bin/` and `obj/`; database queries were SELECTs over catalog views, `__EFMigrationsHistory`, and row counts.

**Local working tree.** Same commit as `origin/main` (`4704889`) plus uncommitted work: publish-protection rules in `SafyaClinic.Web.csproj` (never publish `appsettings*.json` or `wwwroot/uploads`), two new publish profiles, MonsterASP deployment guides, `docs/DeploymentMigrations.sql`, `docs/demo-appsettings.template.json`, and `tests/DeploymentChecks`. An empty `.github/workflows/` folder exists locally.

**Toolchain.** SDKs 10.0.301 and 10.0.401 only (no 8.0 SDK; the net8.0 projects build with SDK 10). Runtimes 8.0.31, 9.0.20, 10.0.x. No `global.json`, so the SDK is not pinned. The local tool manifest (`dotnet-tools.json`, dotnet-ef **10.0.10** against EF Core runtime 8.0.28) could not be listed because Windows flagged the file as downloaded from the internet (Mark of the Web).

**Build.** Solution builds with 0 errors and 3 nullable warnings: `PatientRecordService.cs(268,32)` and `(790,32)` CS8629, `ReservationService.cs(378,17)` CS8602.

**Tests.** PaymentAccessChecks 30/30 pass; DeploymentChecks passes (13 compiled migrations, latest `20260919210058_AddPatientFollowUpTracking`, no model changes without a migration); Node picker tests 5/5 pass.

**Packages.** `SafyaClinic.Web` has vulnerable transitive packages: `System.Security.Cryptography.Xml` 8.0.2 (12 High advisories) and `System.Security.Cryptography.Pkcs` 8.0.1 (1 High, 1 Moderate). Chain from `obj/project.assets.json`: `Microsoft.AspNetCore.Authentication.Cookies` 2.3.11 → `Microsoft.AspNetCore.Authentication` 2.3.10 → `Microsoft.AspNetCore.DataProtection` 2.3.9 → `System.Security.Cryptography.Xml` → `Pkcs`. Cookie authentication is already in the .NET 8 shared framework, so this reference is unnecessary. Outdated: EF Core and Microsoft.Extensions 8.x (newest major 10.0.14), JWT packages 8.19.1 → 8.23.0, FluentValidation 11.8 → 12.2.

**Database `SafyaClinicDb`.** Compatibility level 170, collation `SQL_Latin1_General_CP1_CI_AS`, READ_COMMITTED_SNAPSHOT **on**, recovery model FULL, server time zone `(UTC+02:00) Cairo`. Only principal is the Windows owner (`dbo`); the app connects with integrated security.

- **Schema matches the EF model exactly**: all 31 tables, every column, and all 101 non-PK indexes agree with `SafyaDbContextModelSnapshot.cs`. No views, procedures, functions, triggers, or synonyms. 51 FK columns, none disabled or untrusted.
- **Migration history drift**: 14 rows in `__EFMigrationsHistory` vs 13 compiled migrations. Extra row: `20260725134425_PatientSourceAndPaymentOptions` (never in git). `20260806195047_AddClinicSourcePaymentEnhancements` was recorded with ProductVersion 8.0.0, all others 8.0.28. Because columns and indexes still match, the deleted migration's changes were evidently folded into the next one; a fresh database built from code gets the same schema.
- **Shadow FK columns confirmed present**: `MedicalAnalyses.AnalysisTypeId1`, `Prescriptions.PatientRecordId`, `WeeklyAdministeredItems.PackageItemId1`.
- **Enums stored as strings**: `Payments.Status`, `Payments.PaymentMethod`, `Reservations.Category`, `Reservations.QueueStatus` are `nvarchar(20)`; `Patients.Gender` and `BloodType` are `int`.
- **Types**: money columns are `decimal`; no `float`, `money`, or legacy `datetime`. 14 `nvarchar(max)` columns, mostly free-text notes and the unused `AuditLogs`.
- **Cascade deletes**: 20 cascade FKs; the risky ones are in O19.
- **Data volume**: tiny test data (largest: Reservations 39, Payments 38, WeeklyFollowUps 25, MedicalAnalyses 19; 3 users, 6 roles, 9 patients). Index-usage DMVs were essentially empty after a recent server restart, so **no performance conclusions can be drawn from this database**. Phase 3 performance findings will rely on static analysis plus targeted measurement against seeded volume.
