# 10 — Evaluation Findings (Phase 3: Evaluate)

Assessment of `main` at `e564582`, 2026-10-27. **No fixes are implemented in this phase.**

## How this was evaluated

| Method | Scope |
|---|---|
| Static review | All controllers, startup, auth, DI and persistence code; services in the areas cited; all 85 views scanned (`01-…` – `09-…`) |
| Independent check | A separate reviewer re-checked every code-verifiable claim against the source; corrections (line numbers, counts) and five additional observations were folded in |
| Database | Read-only catalog queries on the local dev `SafyaClinicDb` (`01-…` §15) |
| Runtime | App run locally (`dotnet run`, Development) on the owner's laptop and exercised in the Claude desktop browser pane on 2026-10-27, with owner approval. Signed in as Admin, plus five test users created for the review (`Phase3 *`, all deactivated afterwards). A copy-only DB backup script was provided beforehand |
| Accessibility | axe-core 4.10.2 (WCAG 2.0/2.1 A + AA + best practice) on 37 screens at 1280 px; keyboard spot-checks |
| Responsive | Key screens at 375 × 812 (phone) |

Test data created (all prefixed `TEST-Phase3` or `Phase3`): patients #10, #11, #12; reservations #40, #41; medical record #7; prescription #8; payments of 1.00 and 800.00 for patient #10.

**Priority**: Critical = fix before any further feature work or exposure; High = next few iterations; Medium = plan; Low = opportunistic.
**Confidence**: Verified (reproduced at runtime or read directly in code/DB) · Likely · Needs confirmation.

## Summary

| ID | Finding | Area | Priority | Confidence |
|---|---|---|---|---|
| F01 | Uploaded clinical files are downloadable without signing in | Security | **Critical** | Verified (runtime) |
| F02 | Committed credentials: Web Deploy password, JWT key, bootstrap admin password | Security | **Critical** | Verified |
| F03 | Weak password storage, no password policy, no lockout | Security | **High** | Verified (runtime) |
| F04 | Deactivated users and removed roles keep working sessions | Security | **High** | Verified (runtime) |
| F05 | Visit can be completed and marked paid outside the clinical/payment workflow | Correctness / Authz | **High** | Verified (runtime) |
| F06 | Roles are hard-coded strings; the dev DB's "Reception Admin" role grants nothing | Authz / Maintainability | **High** | Verified (runtime) |
| F07 | Vulnerable transitive packages via legacy cookie package | Security | **High** | Verified |
| F08 | Raw exception messages shown to users, and they persist | Security / UX | **High** | Verified (runtime) |
| F09 | Patient safety: allergies and chronic diseases not shown while consulting or prescribing | UX / Clinical | **High** | Verified (runtime) |
| F10 | Lab analysis lifecycle does not match the agreed rule (Requested → Completed only with a result file) | Correctness | **High** | Verified |
| F11 | Duplicate patients and double-booked doctor slots are accepted | Data integrity | **High** | Verified (runtime) |
| F12 | Payment coverage shown as "Covered" while a balance is still due | Correctness | **High** | Verified (runtime) |
| F13 | Money shown with the server's currency symbol ("$") | Correctness / UX | **Medium** | Verified (runtime) |
| F14 | Time handling: UTC shown to staff; "today" filters use server local time | Correctness / UX | **Medium** | Verified (runtime) |
| F15 | Silent failures where service results are ignored | Reliability | **Medium** | Verified |
| F16 | Lookup admin binds domain entities directly; no length validation | Security / Reliability | **Medium** | Verified (runtime) |
| F17 | Attachment handling: unrestricted types/sizes, orphan files, short content-type column | Security / Reliability | **Medium** | Verified |
| F18 | Drug autocomplete returns nothing and caches the empty result for an hour | Integration | **Medium** | Verified (runtime) |
| F19 | Migrations auto-run at startup in every environment; history drift | Operations | **Medium** | Verified |
| F20 | Risky cascade deletes and shadow foreign-key columns | Data model | **Medium** | Verified |
| F21 | Payment dashboard loads all reservations into memory | Performance | **Medium** | Verified |
| F22 | No automated tests in the solution, no CI, no SDK pin | Testability | **Medium** | Verified |
| F23 | Minimal logging, no audit trail, no health checks | Observability | **Medium** | Verified |
| F24 | Layering leaks and very large service classes | Maintainability | **Medium** | Verified |
| F25 | Systemic accessibility failures (contrast, labels, names, headings) | Accessibility | **High** | Verified (axe) |
| F26 | Empty red "error" box on 11 forms; native `confirm()` dialogs | UX / Accessibility | **Medium** | Verified (runtime) |
| F27 | Inconsistent styling: partial tokens, inline styles, DB-driven badge colours, two icon sets | Design system | **Medium** | Verified |
| F28 | Language and localisation not ready for the bilingual requirement | i18n | **Medium** | Verified |
| F29 | Information architecture: duplicated screens, raw enum labels, menu tied to role strings | IA / UX | **Low** | Verified |
| F30 | CDN dependency without SRI or local fallback | Reliability / Security | **Low** | Verified |
| F31 | Repository hygiene: committed publish output, backups, dead code | Maintainability | **Low** | Verified |
| F32 | Any Doctor/Nutritionist can change the status of another doctor's visit | Authz | **Medium** | Verified (code) |

Positive observations (keep): consistent antiforgery on all 77 POST actions; conditional set-based state transitions plus `sp_getapplock` for queue and payment operations; payment adjustment audit trail; doctors restricted to their own queue *view* (but see F32); payment-report scoping covered by 30 automated checks; skip link and visible focus styles (the skip link is the first Tab stop; inputs show a coloured focus border); Dashboard, Reservation details and Reservations list render without page-level horizontal scroll at 375 px; server responses fast on dev data (≈ 230–630 ms full page loads in the pane, including CDN assets).

---

## Security

### F01 — Uploaded clinical files are downloadable without signing in · Critical · Verified (runtime)
- **Evidence:** `GET /uploads/analysis/ace36a35-….jpeg` with credentials omitted returned **200, image/jpeg, 128,712 bytes** (2026-10-27). Files are written under `wwwroot/uploads` (`FileStorage:BasePath`; `AnalysisController.UploadAttachment`, `MedicalRecordsController.SaveFileAsync`) and `Program.cs:80` serves `wwwroot` statically. The same file is also committed to git twice.
- **Current behaviour:** controller endpoints check roles (Reception gets *Access denied* on `/MedicalRecords/ViewAttachment`), but the static URL bypasses all checks.
- **Problem / impact:** lab results and prescriptions are reachable by anyone with the URL; URLs leak via browser history, logs and shared links. Once real patients are on the system this is a reportable privacy breach.
- **Recommendation:** store uploads outside `wwwroot` (e.g. `App_Data/uploads` or blob storage), serve only through authorised controller actions with `Content-Disposition` and `X-Content-Type-Options: nosniff`, migrate existing files and stored paths, remove the committed file from the repo.

### F02 — Committed credentials · Critical · Verified
- **Evidence:** root `safyaclinic.runasp.net-WebDeploy.publishSettings` (populated `userPWD`); `Jwt:Key` in `SafyaClinic.Web/appsettings.json` and `SafyaClinicPublish/appsettings.json`; hard-coded admin password in `DbSeeder.cs:56`.
- **Current behaviour:** all in git history. The demo password is published intentionally (decision 2026-10-26).
- **Impact:** anyone with repository access can deploy to the hosted site. The JWT key signs a token on every login (`AuthService.cs:53, 113`) that nothing ever consumes; login fails if the key is missing, so key and generator must be removed together.
- **Recommendation:** rotate the Web Deploy password now; delete the publishSettings file and add `*.publishSettings` to `.gitignore`; remove `Jwt` config and the unused generator; move secrets to user-secrets locally and host environment settings remotely; make the seeder read the bootstrap password from configuration and force a change on first login (keeping the demo decision as an explicit demo-only setting).

### F03 — Weak password storage, no policy, no lockout · High · Verified (runtime)
- **Evidence:** `AuthService.HashPassword` = one SHA-256 round over password+salt. Runtime: a user with password `1` was created successfully; 15 consecutive wrong passwords for one account, followed by the right one, signed in with no lockout or delay.
- **Impact:** fast offline cracking if the `Users` table leaks; online guessing of staff passwords (logins are phone numbers, which are easy to enumerate).
- **Recommendation:** switch to `Microsoft.AspNetCore.Identity.PasswordHasher<T>` (PBKDF2, versioned) with rehash-on-login for existing hashes; minimum length 10 and a confirm field; lockout after N failures with a cool-down; log failed attempts; add an Admin "reset password" action that forces a change at next sign-in.

### F04 — Deactivated users keep working sessions · High · Verified (runtime)
- **Evidence:** signed in as `Phase3 Reception`, then deactivated that user from Admin → the session still opened patient and payment pages and **registered a new patient** (#12). Roles are cookie claims only (`AuthController.Login`); 8 h sliding cookie; no `OnValidatePrincipal`.
- **Impact:** leavers or demoted staff retain access until they sign out; sliding expiry can keep it alive indefinitely.
- **Recommendation:** add a per-user security stamp (or `UpdatedAt`) claim and validate it in `CookieAuthenticationEvents.OnValidatePrincipal` every few minutes; bump it on deactivate, role/permission change and password change. Also prevent self-deactivation and removal of the last Admin.

### F07 — Vulnerable transitive packages · High · Verified
- **Evidence:** `dotnet list package --vulnerable`: `System.Security.Cryptography.Xml` 8.0.2 (12 High) and `.Pkcs` 8.0.1 (High + Moderate), pulled by `Microsoft.AspNetCore.Authentication.Cookies` **2.3.11** (ASP.NET Core 2.x package) in `SafyaClinic.Web.csproj`.
- **Recommendation:** remove that package reference (cookie auth is in the .NET 8 shared framework); remove unused JWT packages; re-run the vulnerability scan in CI.

### F08 — Raw exception messages shown to users, and they persist · High · Verified (runtime)
- **Evidence:** posting an Analysis Type with a 145-character name redirected to `/Home/Error` showing *"An error occurred while saving the entity changes. See the inner exception for details."* Loading `/Home/Error` again later still showed it. Cause: `ExceptionHandlingMiddleware.cs:26` writes `ex.Message` to session; `HomeController.Error` never clears it; the middleware sits after authentication (`Program.cs:85`).
A second path: `LookupsController.cs:194, 240, 289, 321` put `ex.Message` into `TempData["Error"]`.
- **Impact:** internal exception text leaks (EF's top-level message is generic, but other exception types expose SQL, file-path or stack details); misleading stale errors; failures return HTTP 200 via redirect, so monitoring cannot see them.
- **Recommendation:** use the built-in `UseExceptionHandler` + `ProblemDetails` with a generic message and a correlation ID; log the full exception; return 500; place it first in the pipeline; validate input lengths (F16).

### F16 — Lookup admin binds domain entities directly · Medium · Verified (runtime)
- **Evidence:** `LookupsController.CreateAnalysisType(AnalysisType model)` and siblings bind EF entities from the form and save via `SafyaDbContext`; no DTO, no length validation (runtime error in F08). Success messages go to `TempData["SuccessMessage"]`, which the layout does not render on two of the screens.
- **Impact:** over-posting of any entity property; unhandled DB errors; inconsistent feedback.
- **Recommendation:** route lookups through services with request DTOs and validation; one shared lookup-management pattern (see F29).

### F17 — Attachment handling · Medium · Verified
- **Evidence:** no extension/MIME allow-list or size check (`FileStorage:MaxFileSizeBytes` unused); client content type stored and served inline; `AnalysisAttachments.ContentType` is `nvarchar(50)` while `PrescriptionAttachments.ContentType` is `nvarchar(max)` (Word/Excel MIME types exceed 50); deleting an attachment removes the DB row only (no `File.Delete` in the solution); file written before the DB insert and the insert result ignored (`AnalysisController.UploadAttachment`).
- **Impact:** stored XSS — `ViewAttachment` serves the client-supplied content type inline without `nosniff`, so an uploaded HTML or SVG file runs on the application's own origin (Verified in code; not exploited at runtime); orphaned files; silent upload failures for Office documents (Likely).
- **Recommendation:** one `IFileStorage` service: allow-list (PDF, JPEG, PNG, optionally DOCX), size limit, server-side content sniffing, `nosniff`, download as attachment except images/PDF, transactional "save file → insert row → on failure delete file", soft-delete with cleanup job; align the content-type column.

### F30 — CDN dependency without SRI · Low · Verified
- **Evidence:** Bootstrap, icons, Font Awesome, jQuery and Google Fonts from CDNs in `_Layout.cshtml` (jQuery in `_ValidationScriptsPartial.cshtml`); no `integrity` attributes; local copies exist in `wwwroot/lib` but are unused.
- **Recommendation:** serve locally (LibMan or npm) or add SRI and fallbacks; drop Font Awesome (7 uses).

## Authorization and workflow correctness

### F05 — Visit completed and marked paid outside the workflow · High · Verified (runtime)
- **Evidence (as `Phase3 Reception`):**
  - **Price override (R23):** booked reservation #40 with amount 1.00 instead of the treatment type's 800.00; a 1.00 payment was then collected.
  - **Completion (R22):** after the doctor started the consultation, Reception posted `UpdateStatus` → *Completed*. The visit is completed and paid, but **no medical record exists** for it (`/MedicalRecords/PatientRecords?patientId=10` showed "No medical records yet" at that point).
  - **MarkPaid (R21):** `POST /Reservations/MarkPaid/41` flipped reservation #41 to "Covered" with no payment recorded.
- **Problem:** sensitive actions are open to all `ClinicalStaff`; the owner decided (2026-10-27) these must be governed by admin-managed permission groups.
- **Impact:** revenue leakage, visits without clinical documentation, unreliable reports.
- **Recommendation:** short term — restrict `MarkPaid` and the *Completed* option of `UpdateStatus` to Admin, and price override to Admin, behind named policies. Target — the permission-group model (Phase 4) with permissions such as `Reservations.OverridePrice`, `Reservations.CompleteOnBehalf`, `Payments.MarkPaidWithoutPayment`, all audited (F23). Consider removing `MarkPaid` entirely.

### F06 — Hard-coded roles; "Reception Admin" grants nothing · High · Verified (runtime)
- **Evidence:** policies require the exact string `ReceptionAdmin` (`Program.cs:40–41`), but the role created through the UI in the dev DB is named **"Reception Admin"** (with a space). A test user with only that role saw just *Dashboard* and was denied Payments Dashboard, Payment Report and Patients. Roles are also checked via 58 `User.IsInRole("…")` calls in views and by role **IDs** 2 and 5 in `UserService.cs:140`.
- **Impact:** administrators can create roles that silently do nothing; any rename breaks access; the owner's permission-group requirement cannot be met.
- **Recommendation:** introduce permissions as code constants mapped to admin-managed groups (Phase 4), a single authorization handler, and view helpers that ask "can user do X" instead of role names. Until then, either seed `ReceptionAdmin` or fix the dev data, and block free-text role creation.

### F10 — Lab analysis lifecycle mismatch · High · Verified
- **Evidence:** `AnalysisStatus` has Requested, InProgress, Completed, Cancelled; `AnalysisService.UpdateStatusAsync` (line 272) assigns any status without checking the current state or the presence of an attachment; `AnalysisController.UpdateStatus` ignores the result.
- **Agreed rule (2026-10-27):** Requested → Completed only, and only once a result file is uploaded to the patient record.
- **Recommendation:** enforce the transition in the service (Completed requires ≥ 1 attachment, set `ResultDate`), decide what happens to existing InProgress/Cancelled rows, return and display failures. Enrollment statuses (Active, OnHold, Completed, Cancelled) need explicit transition rules too (`NutritionService.cs:271–284` accepts any).

### F12 — "Covered" while a balance is due · High · Verified (runtime)
- **Evidence:** reservation #41 showed *Covered* (after `MarkPaid`), yet completing its consultation reported "Payment remains outstanding" and the Collect form pre-filled **800.00**. Two sources of truth: the `Reservations.IsPaid` flag and the computed balance (`GetDueAmountAsync`). Cancelled payments also count as coverage by design (`PaymentService.cs:532–536`).
- **Impact:** reception sees contradictory states; dashboards that read `IsPaid` disagree with collection screens.
- **Recommendation:** derive coverage from payments in one place (query or computed view) and stop writing `IsPaid` from other paths; label write-offs distinctly from payments.

### F11 — Duplicate patients and double bookings accepted · High · Verified (runtime)
- **Evidence:** two patients registered with the same primary phone (#10, #11) — the dev data already contains an earlier duplicate pair. Two reservations for the same doctor, date and 15:00 slot (#40, #41) were both accepted. No unique index on phone; the schedule index is non-unique.
- **Agreed rule (2026-10-27):** the primary phone number identifies a patient uniquely.
- **Recommendation:** normalise phone numbers (Egyptian mobile format), unique filtered index on the primary phone, a duplicate check on create/edit with a "go to existing patient" path, a duplicate-merge tool for existing data; overlap check for doctor slots (with an Admin override permission).

### F09 — Allergies not visible while consulting or prescribing · High · Verified (runtime)
- **Evidence:** the patient page shows allergies, but the consultation form (`/MedicalRecords/Consultation`), the record page (`/MedicalRecords/Details/{id}`) and the prescription form show **no allergies, chronic diseases or age**; the consultation form (`Create.cshtml` bound to `CreatePatientRecordRequest`, IDs only) does not even show the patient's name. `PrescriptionDetails` shows name, age and sex but not allergies.
- **Impact:** a doctor can prescribe without seeing a documented allergy. This is the most important workflow gap for patient safety.
- **Recommendation:** a persistent patient header (name, age, sex, allergies highlighted, chronic diseases, last visit) on all clinical screens; allergy warning on the prescription form.

### F32 — Visit status changes are not tied to the assigned doctor · Medium · Verified (code)
- **Evidence:** `ReservationService.UpdateStatusCoreAsync` (≈ lines 783–872) receives `currentUserId` but never compares it with `Reservation.DoctorId`; `ReservationsController.UpdateStatus` is open to all `ClinicalStaff`. Any Doctor or Nutritionist can complete, cancel or mark no-show another doctor's visit. (`StartConsultation` and `CompleteConsultation` *do* check the assigned doctor.)
- **Problem:** inconsistent with the doctor-scoped queue and consultation actions. Cross-doctor *record editing* is intended (decision 2026-10-26); whether cross-doctor *status changes* are intended is not decided.
- **Recommendation:** treat it as a permission (e.g. `Reservations.ChangeStatusForOtherDoctors`) in the Phase 4 model; until then restrict to the assigned doctor, Reception and Admin.

### F33 — Lookup delete checks the wrong table · Medium · Verified (code, found 2026-10-29)
- **Evidence:** `LookupsController.DeleteTreatmentType` checks `Treatments.Id == id`, `DeleteInjectionType` checks `TreatmentTypes.Id == id`, `DeleteVitaminType` checks `Treatments.Id == id`. None of these is the real reference (`Reservations.TreatmentTypeId`, `PackageItems.InjectionId`, `PackageItems.VitaminId`).
- **Problem:** deleting a type that is in use usually fails with a raw foreign-key error; an unused type can be wrongly refused and deactivated instead of deleted.
- **Recommendation:** check the real references; in-use types are deactivated (as the screen already says). Fixed in slice S4.

### F34 — Analysis Types form never saves the name · Medium · Verified (runtime, found 2026-10-29)
- **Evidence:** `Views/Lookups/AnalysisTypes.cshtml` posts the field as `name="Name"`, but `AnalysisType` has `TypeName`; `CreateAnalysisType` binds the entity directly, so `TypeName` stays `""` and the row is saved with an empty name, whatever is typed. (F08's runtime repro posted `TypeName` directly, which is why it reached the database limit.)
- **Problem:** every analysis type added from the screen is nameless; doctors pick from an unlabeled list.
- **Recommendation:** send `TypeName` (one attribute); validate on a DTO in M5. The lab module is frozen until its revamp (decision D9), so the owner decides whether this one-line fix goes in earlier.

### F35 — "Activate" user never worked · Medium · Verified (runtime, found 2026-10-30)
- **Evidence:** `Views/Users/Index.cshtml` posts `<input type="hidden" name="isActive" value="@(!u.IsActive)" />`. Razor omits a boolean attribute when it is `false` and renders `value="value"` when it is `true`; `"value"` does not bind to `bool`, so `isActive` is always `false`.
- **Problem:** a deactivated user could never be reactivated from the screen (the old code failed silently).
- **Recommendation:** post a string (`"true"` / `"false"`). Fixed in slice S6.

### Upload policy attribute is misleading (note, part of F06)
`[Authorize(Policy = "ClinicalStaff")]` on `AnalysisController.UploadAttachment` and `MedicalRecordsController.UploadPrescriptionAttachment` suggests Reception and Nutritionists can upload, but the class-level `DoctorOrAdmin` also applies, so they cannot. Decide who should upload results (the agreed lab rule makes this important) and express it as a permission.

## Data, time and money

### F13 — Currency shown with the server's symbol · Medium · Verified (runtime)
- **Evidence:** amounts render as `$801.00`, `$7,000.00` etc.; 48 `ToString("C")` / `:C` usages in 19 views use the server culture (en-US). A few screens hard-code "EGP".
- **Recommendation:** fix the culture for money formatting (EGP, `ar-EG`/`en-EG` formats) through one display helper; this is also a prerequisite for localisation (F28).

### F14 — Time handling · Medium · Verified (runtime)
- **Evidence:** staff screens print UTC: "Checked in: 27 Oct 2026 10:06 UTC" for a 13:06 Cairo check-in, "Event timestamps in UTC" on the timeline, payment history and user last-login in UTC without a label. "Today" filters use `DateTime.Today` (21 uses) against UTC timestamps, e.g. dashboard revenue: payments between 00:00 and 03:00 Cairo time are counted on the previous day. Only `PaymentsController.Report` converts correctly.
- **Recommendation:** a single clinic time-zone setting (`Africa/Cairo`) and an `IClinicClock` used for "today" ranges and all display conversions; keep UTC in storage.

### F19 — Startup migrations and history drift · Medium · Verified
- **Evidence:** `Program.cs:60–64` runs `Database.Migrate()` and the seeder on every start in every environment; the dev DB history contains `20260725134425_PatientSourceAndPaymentOptions`, which exists in no code or git history.
- **Impact:** an accidental bad migration is applied as soon as the site restarts; multiple instances race; history drift makes environments differ.
- **Recommendation:** apply migrations as an explicit deployment step (idempotent script already produced by `DeploymentChecks`); keep auto-migrate only for Development behind a flag; reconcile the history row.

### F20 — Cascade deletes and shadow FKs · Medium · Verified
- **Evidence:** `Users → Payments (CollectedBy)` and `Users → Enrollments (DoctorId)` are CASCADE; packages → enrollments → follow-ups cascade but are blocked by `WeeklyAdministeredItems` (NO ACTION); shadow columns `AnalysisTypeId1`, `PackageItemId1`, `Prescriptions.PatientRecordId`; leftover defaults (`Payments.Status = ''`, `PrescriptionDate = 0001-01-01`).
- **Recommendation:** one migration to set financial and clinical FKs to RESTRICT, remove duplicate relationship mappings (`SafyaDbContext.OnModelCreating` lines 81–89) and the shadow columns, and fix defaults — test on a DB copy first.

### F21 — Payment dashboard loads all reservations · Medium · Verified
- **Evidence:** `PaymentService.GetPaymentDashboardAsync` filters payments by date but materialises **every reservation in the database** with per-row payment sums for Admin (`unpaidOnly = false`), then aggregates in memory (lines 660–760).
The Reception path (`unpaidOnly = true`) also has no date filter: it loads every in-consultation or finished reservation ever made.
- **Impact:** response time and memory grow with history (fine at 41 reservations; problematic after a year or two).
- **Recommendation:** filter reservations by the same date range or by open balance, aggregate in SQL, paginate unpaid lists. Measure on a seeded large dataset (≥ 50k reservations) before and after.

### F18 — Drug autocomplete returns nothing · Medium · Verified (runtime)
- **Evidence:** `/api/medicines/search?query=augmentin` and `…=panad` returned `[]` (2026-10-27); typing in the prescription form showed no suggestions. `EgyptianDrugService` caches empty results for 60 minutes; provider is the DwaPrices **sample** endpoint with no key.
- **Recommendation:** do not cache failures; surface "suggestions unavailable"; confirm provider terms or fall back to a local drug list (seed JSON) maintained by Admin; consider storing a drug code with each prescription item.

## Reliability, observability, testability, maintainability

### F15 — Silent failures · Medium · Verified
- **Evidence:** `ServiceResult` discarded in `AnalysisController.UpdateStatus`, `UploadAttachment` and `DeleteAttachment`, `MedicalRecordsController.UploadPrescriptionAttachment` and `DeleteAttachment`, `ReservationsController.MarkPaid`, `UsersController.ToggleActive` (no confirmation either); missing record → redirect to Patients with no message (`/MedicalRecords/Details/999999`).
- **Recommendation:** always map results to user feedback; a shared helper in `BaseController`; analyser rule or code review checklist.

### F22 — No tests in the solution, no CI · Medium · Verified
- **Evidence:** two console-based checks and one Node test, none in the `.sln`; no xUnit/NUnit; no pipeline (`.github/workflows` exists only locally, empty, not in git); no `global.json`; dotnet-ef 10 against EF Core 8.
- **Recommendation:** add `SafyaClinic.Tests` (xUnit) with service tests on SQL Server in a container or LocalDB, port the 30 payment checks, add Playwright smoke tests for the visit workflow; a GitHub Actions build + test + vulnerability scan; pin the SDK.

### F23 — Logging, audit, health · Medium · Verified
- **Evidence:** `ILogger` used only in the exception middleware and drug index; `AuditLogs` table never written; no health endpoint; no structured logging.
- **Recommendation:** structured logging (Serilog or built-in JSON console) with request/user correlation; write audit entries for sign-in, user/permission changes, record edits/locks, payment actions and the permission-gated actions in F05; `/health` with DB check.

### F24 — Layering and size · Medium · Verified
- **Evidence:** Application composes EF queries directly; three controllers use `SafyaDbContext`; services of 876–1,303 lines; business rules duplicated between controllers and services (e.g. redirect-to-collect logic in two controllers); magic strings for statuses (`"Completed"` etc., 23 in `ReservationService`).
- **Recommendation:** no big rewrite. Extract per-use-case handlers or smaller services when touching an area; status constants/enums in one place; keep EF in Application (pragmatic) but remove `DbContext` from controllers.

### F31 — Repository hygiene · Low · Verified
- **Evidence:** 29 MB `SafyaClinicPublish/` output, `backup/Views`, `Reservations/Index-old.cshtml`, `site-back.css`, unused `HttpEgyptianDrugSource`, unused `ReservationStatusEnum`, commented `SeedData`, unused FluentValidation, duplicate `.gitatributes`, a committed Office lock file `~$fyaClinkc-Domain.docx`, tracked `.vscode/`.
- **Recommendation:** remove in one housekeeping PR; ignore publish output.

## Accessibility, UX and design system

### F25 — Systemic accessibility failures · High · Verified (axe, 37 screens)

| Rule (impact) | Screens | Nodes | Example / cause |
|---|---|---|---|
| color-contrast (serious) | 37/37 | 185 | Sidebar section labels `#9ca3af` on white = 2.53:1 (`site.css` `.nav-section-label`); status badges with white text on DB colours: *Pending* `#ffc107` = **1.63:1**, *Confirmed* `#17a2b8` = 3.04:1 |
| list (serious) | 37/37 | 37 | Sidebar `<ul class="nav">` contains `<li role="presentation">` section labels |
| page-has-heading-one (moderate) | 18/37 | 18 | Detail/create pages use `h4`/`h6` only (Reservation details, Analysis, Nutrition, Users, Clinics…); topbar title is an `h6` on every page |
| button-name (critical) | 5 | 14 | Icon-only buttons (Users role dropdown, Clinics/PatientSources actions, prescription row delete) |
| select-name / label (critical) | 4 | 7 | Queue clinic/doctor filters, analysis result form (status, date, notes, file input), nutrition follow-up |
| link-name (serious) | 3 | 17 | Icon-only links in Analysis, Clinics, Patient Sources lists |
| heading-order, empty-table-header, scrollable-region-focusable | 4–6 | 11 | Skipped levels; empty action `<th>`; Board scroll area not keyboard-focusable |

- **Impact:** fails WCAG 2.1 AA; low-contrast status badges also hurt sighted users in bright reception areas.
- **Recommendation:** fix at the token/component level, not per page: contrast-checked status tokens instead of DB hex colours, sidebar markup, page-title `h1` in the shell, `aria-label` on icon buttons via a shared icon-button partial, labelled filter components; run axe in CI.

### F26 — Empty error box on forms; native dialogs · Medium · Verified (runtime)
- **Evidence:** forms with `<div asp-validation-summary="All" class="alert alert-danger …">` render a visible empty 34 px red box with `role="alert"` when there are no errors (seen on Reservation create and Consultation). 11 views use this combination: MedicalRecords Create/Edit, Analysis Request, Users Create, Reservations Create/Edit, Patients Create/EditBasic/EditMedical, Nutrition Enroll/RecordFollowUp (in the last two the page heading sits inside the alert). `ModelOnly` summaries render nothing when valid and are not affected. 16 `confirm()` dialogs (e.g. Complete Consultation) — native, unstyled, not localisable, and they block automation.
- **Recommendation:** a validation-summary partial that renders nothing when valid; a shared confirmation modal component.

### F27 — Inconsistent styling · Medium · Verified
- **Evidence:** 25 custom properties in `site.css` but 59 inline `style=""`, 5 `<style>` blocks, about 30 distinct hex colours in views (≈ 60 including `site.css`), 22 `!important`; badge colours from `ReservationStatuses.ColorCode`; Bootstrap Icons (347) and Font Awesome (7); six standalone print/report pages with their own CSS.
- **Recommendation:** Phase 6 design tokens and components; map statuses to semantic tokens in code; one icon set; shared print stylesheet.

### F28 — Not ready for the bilingual requirement · Medium · Verified
- **Evidence:** `<html lang="en" dir="ltr">` everywhere; no `IStringLocalizer`/resource files; Arabic hard-coded in five views (three lookup pages, landing page, history report using `bootstrap.rtl`); two views not UTF-8 (`Payments/Unpaid`, `Payments/Report`); enum names displayed raw ("InternalMedicine", "APositive").
- **Agreed rule (2026-10-27):** full English UI, switchable to Arabic labels; data in English, notes in either language.
- **Recommendation:** ASP.NET Core request localisation with a user-level language preference, `.resx` resources for labels and enum display names, `dir="rtl"` + Bootstrap RTL when Arabic, logical CSS properties; `dir="auto"` on notes fields.

### F29 — Information architecture · Low · Verified
- **Evidence:** injection and vitamin types are managed in two places (`Lookups/*` with modals and Arabic titles; `Nutrition/*` pages); a ReceptionAdmin-only user would see Billing but no Care section; menu visibility duplicates server rules with role strings; Collect Payment asks for the clinic again although the reservation already has one; on phones, list tables scroll horizontally so row actions are off-screen.
- **Recommendation:** one Settings area for lookups; navigation built from the same permission model as the server; pre-fill known values; card or stacked layouts for key lists on small screens.

---

## Coverage limits

- Not tested at runtime: nutrition enrollment/follow-up creation, analysis request and upload, print layouts, the public landing page, the hosted sites, load/performance at volume, screen readers.
- axe finds roughly a third of WCAG issues; a manual keyboard and screen-reader pass is still needed for key flows.
- Findings about the hosted test/demo sites (time zone, static file serving) are inferred from code and local behaviour.

## Clean-up after this phase

Test users `Phase3 *` are deactivated; their password is stored only in `.assessment/test-users.txt` (git-ignored). Test patients, reservations, record, prescription and payments remain in the dev DB. To remove them, restore the backup from `.assessment/backup-db.ps1` if one was taken before the review, or keep them as fixtures.
