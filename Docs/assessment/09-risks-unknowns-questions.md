# 09 — Known Risks, Unknowns, and Questions

Phase 2 deliverable. This is a **register**, not an evaluation: items are listed with evidence and confidence so Phase 3 can assess impact, assign priority (Critical/High/Medium/Low) and recommend fixes. IDs continue the `O#` series from `01-…` §12; new items found in Phase 2 start at R21.

Confidence: **Verified** (read in code/DB), **Likely** (strong evidence, not executed), **Needs confirmation**.

## 1. Risk register

### Security and privacy

| ID | Risk | Evidence | Confidence |
|---|---|---|---|
| O1 | Web Deploy password committed (root `.publishSettings`) and in history | `safyaclinic.runasp.net-WebDeploy.publishSettings` | Verified |
| O2 | JWT signing key committed in two `appsettings.json` files | `SafyaClinic.Web/appsettings.json`, `SafyaClinicPublish/appsettings.json` | Verified |
| O3 | Hard-coded bootstrap admin password (also published for the demo by decision) | `DbSeeder.cs:56`; demo guide | Verified |
| O4 | Clinical attachments served anonymously as static files; no type/size validation; served inline with client-supplied content type | `Program.cs:80`; `AnalysisController.cs:172–247`; `MedicalRecordsController.cs:236–345` | Verified (anonymous reachability to be confirmed in browser) |
| O5 | Single-round SHA-256 password hashing; no password policy; no lockout | `AuthService.cs` | Verified |
| O6 | Raw exception messages displayed to users; stale message persists in session | `ExceptionHandlingMiddleware.cs:117`, `HomeController.cs:19` | Verified |
| O10 | Deactivation / role removal not enforced on live sessions; no self or last-admin guard | `UserService.cs` | Verified |
| O18 | Vulnerable transitive packages (12 High) via legacy cookies package | `dotnet list package --vulnerable` | Verified |
| R21 | `POST /Reservations/MarkPaid` sets `IsPaid` without creating a payment; no UI uses it; open to any ClinicalStaff | `ReservationsController.cs:363`, `ReservationService.MarkAsPaidAsync` | Verified |
| R22 | `UpdateStatus` lets any ClinicalStaff (Reception, Nutritionist) mark an in-consultation reservation *Completed*, bypassing doctor-only completion | `ReservationService.UpdateStatusCoreAsync` 755–872 | Verified |
| R23 | Reservation price can be overridden from the form by any role that creates reservations | `ReservationService.cs:61` | Verified (whether intended: Needs confirmation) |
| R24 | CDN assets without SRI | `_Layout.cshtml:25–30` | Verified |
| R25 | Razor-generated option lists embedded in JS template literals (HTML-encoded but not JS-encoded) | `Nutrition/CreatePackage.cshtml:112–113` | Likely (Admin-only input) |
| R26 | No audit of sign-ins, admin actions or clinical edits; `AuditLogs` unused | grep | Verified |

### Correctness and data integrity

| ID | Risk | Evidence | Confidence |
|---|---|---|---|
| O8 | "Today" computed in server local time while timestamps are UTC (dashboard revenue, queues, board) | `DashboardController.cs:33`, `ReservationService.cs:352, 458, 502, 564`; only `PaymentsController.Report` converts to Cairo | Verified |
| O11 | Shadow FK columns from duplicated mappings | DB columns `AnalysisTypeId1`, `PatientRecordId`, `PackageItemId1` | Verified |
| O14 | Service failures silently ignored where controllers discard `ServiceResult` | `AnalysisController.UpdateStatus/UploadAttachment`, `ReservationsController.MarkPaid`, `UsersController.ToggleActive` | Verified |
| O15 | Lookup success messages written to an unrendered TempData key | `LookupsController.cs:44, 74` | Likely |
| O17 | Migration history drift (DB has a migration absent from code/git) | `__EFMigrationsHistory` | Verified |
| O19 | Cascade deletes from `Users` to `Payments`/enrollments, and from packages/enrollments to follow-ups | DB FKs | Verified (latent) |
| O20 | Leftover column defaults (`''`, `0001-01-01`, `1`, `0`) | DB default constraints | Verified |
| R27 | `AnalysisAttachments.ContentType` is `nvarchar(50)`; long MIME types (Office documents) would fail to save, and the failure is swallowed (O14) | DB schema; `AnalysisController.UploadAttachment` | Likely |
| R28 | Deleting an attachment leaves the file on disk; uploads saved before the DB row, so failed inserts also orphan files | `AnalysisService.DeleteAttachmentAsync`; no `File.Delete` | Verified |
| R29 | Enrollment / follow-up cascade blocked once items were administered (`WeeklyAdministeredItems.FollowUpId` NO ACTION) | DB FKs; `NutritionService.DeleteFollowUpAsync` | Likely |
| R30 | Free status transitions for lab analyses and enrollments (any → any) | `AnalysisService.cs:272`, `NutritionService.cs:271–280` | Verified |
| R31 | No double-booking guard for doctor time slots | `ReservationService.CreateReservationAsync`; non-unique schedule index | Likely |
| R32 | Duplicate patients possible (National ID optional; phones not unique/indexed) | `PatientService.cs:29`; DB indexes | Verified |
| R33 | Reservation insert and its zero-cost payment are two separate saves (not atomic) | `ReservationService.cs:91–96` | Verified |
| R34 | "Doctor" lookups depend on role IDs 2 and 5 | `UserService.cs:140` | Verified |
| R35 | Drug-index failures cached as empty for 60 minutes | `EgyptianDrugService` | Verified |

### Maintainability, operability, testability

| ID | Risk | Evidence | Confidence |
|---|---|---|---|
| O7 | Migrations auto-applied at startup in all environments | `Program.cs:60–64` | Verified |
| O9 | Hard-coded role strings across policies, controllers and ~50 view checks; new roles inert | `07-…` §3 | Verified |
| O12 | No unit/integration/UI test projects; tests outside the solution; no CI | `01-…` §10 | Verified |
| O16 | Medicine search depends on a free sample endpoint with unknown SLA | `appsettings.json` | Verified |
| R36 | Very large service classes (≈ 900–1,300 lines) mixing queries, rules and mapping | `PaymentService`, `ReservationService`, `PatientRecordService`, `NutritionService` | Verified |
| R37 | Layering leaks: Application uses EF Core directly; Web controllers use `SafyaDbContext`; Lookups bind domain entities from forms (over-posting) | `03-…` §1 | Verified |
| R38 | Duplicated screens/logic: injection & vitamin type management in both `Lookups` and `Nutrition`; upload/download code in two controllers | `05-…` | Verified |
| R39 | Dead/legacy artefacts: `Index-old.cshtml`, `site-back.css`, `backup/Views`, committed `SafyaClinicPublish/`, unused JWT, `HttpEgyptianDrugSource`, `ReservationStatusEnum`, commented `SeedData`, FluentValidation without validators | various | Verified |
| R40 | Minimal logging (only middleware + drug index); no structured logs, health checks or metrics | grep `ILogger<` | Verified |
| R41 | No SDK pin (`global.json`); dotnet-ef 10 tool vs EF Core 8 runtime | `01-…` §15 | Verified |
| R42 | Two views not UTF-8 encoded (`Payments/Unpaid`, `Payments/Report`) | encoding check | Verified |

### UX, accessibility, information architecture (detail in Phase 3)

| ID | Observation | Evidence | Confidence |
|---|---|---|---|
| R43 | Mixed English/Arabic UI with `lang="en" dir="ltr"` everywhere; Arabic titles on some admin screens and the history report | 5 views | Verified |
| R44 | Partial token system in `site.css` (25 custom properties) but 59 inline styles, 5 `<style>` blocks, 61 distinct hex colours in views; two icon libraries | `01-…` §9 | Verified |
| R45 | Status colours stored in the database (`ReservationStatuses.ColorCode`) and applied as inline styles — not themeable, contrast not controlled | `_StatusBadge.cshtml` | Verified |
| R46 | 16 native `confirm()` dialogs; 35 tables but only 22 `.table-responsive` wrappers | view scan | Verified |
| R47 | A ReceptionAdmin-only user sees Billing but no Care navigation; menu visibility duplicated from policies | `_Layout.cshtml:71–159` | Verified |

## 2. Unknowns

| # | Unknown | Why it matters | How to resolve |
|---|---|---|---|
| U1 | Time zone of the MonsterASP servers | Determines the size of the O8 date-boundary error on hosted sites | Check host settings or log `TimeZoneInfo.Local` once |
| U2 | Whether `/uploads/...` is actually served on the hosted sites (IIS static file config, publish exclusions) | Severity of O4 | Browser test against a test upload |
| U3 | Real-world data volumes (patients/day, reservations/day, attachment sizes) | Performance priorities; dev DB has < 40 rows per table | Owner estimate |
| U4 | Usage of Arabic vs English by each role | Drives localisation and RTL decisions in Phase 4 | Owner / user interviews |
| U5 | Devices used at reception and in consultation rooms (desktop, tablet, screen sizes) and internet reliability | Responsive priorities; CDN dependency | Owner |
| U6 | DwaPrices terms of use and availability | Whether to keep the integration | Provider |
| U7 | Behaviour of pages in the browser (no runtime UI session yet) | Phase 3 UX/accessibility evidence | Run the app locally and review key workflows with screenshots |
| U8 | Whether the GitHub repository is or was public | Urgency of O1/O2 rotation | Owner |

## 3. Questions for the owner (non-blocking for Phase 3)

1. **R21 MarkPaid** — should marking a reservation paid without a payment record be possible at all? If yes, for whom?
2. **R22 Completion** — should Reception be able to mark a consultation *Completed* (e.g., when a doctor forgets), or only doctors/admin?
3. **R23 Price override** — who may change a reservation's price at booking: Reception, Admin only, or nobody (use the treatment type price)?
4. **R30 Status transitions** — what are the allowed transitions for lab analyses (Requested → InProgress → Completed/Cancelled?) and enrollments (Active ↔ OnHold → Completed/Cancelled?)
5. **R32 Duplicates** — should the primary phone number identify a patient uniquely, or only warn on duplicates?
6. **U4/U5** — which language and devices does each role use day to day?
7. **Phase 3 runtime review** — may I run the app locally (`dotnet run`) against `SafyaClinicDb` and click through workflows in the browser? This *writes* test data (reservations, payments) to the dev database, so it needs your explicit approval; alternatively I can use a copy of the database.

## 4. Decisions recorded

| Date | Decision |
|---|---|
| 2026-10-26 | All environments hold dev/test data only; `safyaclinic.runasp.net` is not production |
| 2026-10-26 | Lab and management roles deferred to the enhancements phase |
| 2026-10-26 | Any doctor may edit another doctor's unlocked medical record |
| 2026-10-26 | Public demo intentionally publishes the seeded admin login; a safer demo-access model is a future enhancement |
