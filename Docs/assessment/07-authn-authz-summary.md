# 07 — Authentication and Authorization Summary

Phase 2 deliverable. All statements Verified in source unless marked. Per-route effective roles are in `05-route-and-screen-inventory.md`.

## 1. Authentication

| Aspect | Current implementation | Evidence |
|---|---|---|
| Scheme | ASP.NET Core cookie authentication, cookie `SafyaClinic.Auth` | `Program.cs:25–36` |
| Cookie flags | HttpOnly, SameSite=Strict, `Secure` not set explicitly (default `SameAsRequest`), persistent, 8 h expiry, sliding | `Program.cs:31–35`, `AuthController.cs:57–64` |
| Login identifier | Staff phone number (`Users.PhoneNumber`, unique) + password; only `IsActive` users | `AuthService.LoginAsync` |
| Password storage | `base64(salt16) + "." + base64(SHA-256(password ‖ salt))`; single iteration. Comment claims PBKDF2 / Identity v3 compatibility — inaccurate | `AuthService.HashPassword`, `VerifyPassword` |
| Comparison | Constant-time byte comparison | `AuthService.CryptographicEquals` |
| Password policy | None (no length/complexity/confirmation); `ChangePasswordRequest`, `CreateUserRequest` have no validation | `DTOs/Auth/AuthDtos.cs`, `DTOs/Patient/PatientDtos.cs:124` |
| Brute-force protection | None (no lockout, throttling, CAPTCHA, or failed-attempt logging) | `AuthService.LoginAsync` |
| Session revocation | Roles are copied into the cookie at sign-in; deactivation and role removal take effect only at next sign-in. Sliding expiry renews indefinitely while used. No `OnValidatePrincipal` / security stamp | `AuthController.Login`, `UserService.SetUserActiveAsync`, `RemoveRoleAsync` |
| Password change | Requires current password; no re-authentication of other sessions | `AuthService.ChangePasswordAsync` |
| Password reset | Not available: neither self-service reset nor an Admin reset action exists (`UsersController` has Index, Create, ToggleActive, AssignRole, RemoveRole only) | route inventory |
| Bootstrap account | Created by `DbSeeder` when `Users` is empty, with a hard-coded password; the demo guide publishes it intentionally for the public demo (decision 2026-10-26) | `DbSeeder.cs:47–79`; `docs/MonsterASP-Demo-First-Publish.md` |
| JWT | A signed JWT is created on every login (`Jwt:Key` from config) and returned in `LoginResponse.Token`, then discarded; no bearer scheme is registered | `AuthService.GenerateJwtToken` |
| Antiforgery | All 77 POST actions carry `[ValidateAntiForgeryToken]`; no global filter, so new POST actions must remember it | route inventory |
| Session state | ASP.NET session (30 min idle) used for the global error message | `Program.cs:49–54`, `ExceptionHandlingMiddleware` |
| HTTPS | `UseHttpsRedirection` always; HSTS outside Development | `Program.cs:73–79` |

## 2. Roles

| Role | Source | Used by policies | Notes |
|---|---|---|---|
| Admin | seeded | all | Full access |
| Doctor | seeded | DoctorOrAdmin, ClinicalStaff, PaymentReports, inline `Roles` on Queue/FollowUps | Doctors are also identified in code by **role IDs 2 and 5** (`UserService.cs:140`) |
| Nutritionist | seeded | NutritionTeam, ClinicalStaff | Treated as a "doctor" for doctor pickers (role ID 5) |
| Reception | seeded | ReceptionOrAdmin, ClinicalStaff, PaymentStaff, PaymentReports, inline `Roles` | |
| ReceptionAdmin | created through the Roles screen (not seeded) | PaymentStaff, PaymentReports | Payment-only role; no access to patients or reservations |
| Patient | seeded | none | Unused |
| *(any new role)* | Roles screen | none | Grants nothing: policies use fixed role-name strings |

Deferred (enhancements): Clinic Management, Lab Doctor, Lab Tech, Lab Reception.

## 3. Authorization mechanisms

1. **Named policies** (`Program.cs:38–46`): `AdminOnly`, `DoctorOrAdmin`, `ReceptionOrAdmin`, `NutritionTeam`, `ClinicalStaff`, `PaymentStaff`, `PaymentReports` — all `RequireRole`.
2. **Inline role lists**: `[Authorize(Roles = "Admin")]` on `LookupsController`; `"Admin,Reception,Doctor"` on `FollowUpsController` and `ReservationsController.Queue`.
3. **Class + method stacking**: requirements are AND-ed. Two method-level `ClinicalStaff` attributes inside `DoctorOrAdmin` controllers therefore do not widen access (`AnalysisController.UploadAttachment`, `MedicalRecordsController.UploadPrescriptionAttachment`).
4. **In-code checks** via `BaseController.IsAdmin/IsDoctor/IsNutritionist/IsReception`: own-queue restriction (`ReservationsController.Queue` lines 85–88), payment report scoping (`PaymentsController.Report`), redirect-to-collect after completion.
5. **Service-level ownership**: record creation and consultation require the reservation's doctor unless Admin (`PatientRecordService.cs:59–91, 256, 308`); `StartConsultationAsync` receives `currentUserId` and `isAdmin`.
6. **View-level visibility**: ~50 `User.IsInRole("…")` checks in views (`_Layout`, `Reservations/Details`, `Patients/Details`, `Dashboard/Index`, `Payments/Report`, …). These only hide UI.
7. **No fallback policy**: an action without any attribute is anonymous (today only `HomeController.Error`).

## 4. Data-level access rules

| Data | Rule today | Intended? |
|---|---|---|
| Medical records | Any Doctor/Admin can read and edit any unlocked record | **Yes** (owner decision 2026-10-26) |
| Doctor queue | Doctors see only their own; Admin/Reception see all | Yes |
| Patients | All ClinicalStaff see all patients; medical background and timeline restricted to Doctor/Admin | Assumed |
| Payments report | Reception/ReceptionAdmin: today only; Doctor: own; Admin: all | Yes (covered by 30 automated checks) |
| Attachments (analysis, prescription) | Controller-level role check, then any attachment by ID. **Also served without authentication as static files** from `wwwroot/uploads/...` | No — to be evaluated in Phase 3 |
| Reservation status changes | Any ClinicalStaff, any reservation, including setting *Completed* | Needs confirmation |
| `POST /Reservations/MarkPaid` | Any ClinicalStaff; sets `IsPaid` without a payment | Needs confirmation (no UI uses it) |

## 5. Gaps to evaluate in Phase 3

| # | Gap | Evidence |
|---|---|---|
| A1 | Weak password hashing; no password policy; no lockout or throttling | §1 |
| A2 | Deactivated users and removed roles keep access until the cookie lapses | §1 |
| A3 | No self-protection: an Admin can deactivate themself or remove the last Admin role | `UserService.SetUserActiveAsync`, `RemoveRoleAsync` |
| A4 | Role model is hard-coded; roles created in the UI are inert; doctor detection uses role IDs | §2 |
| A5 | Uploaded medical files reachable anonymously via static file middleware | `Program.cs:80`, `FileStorage:BasePath` |
| A6 | Status/payment-state endpoints broader than the workflow implies (`UpdateStatus` → Completed, `MarkPaid`) | `ReservationsController.cs:334, 363` |
| A7 | Unused JWT generation and signing key in configuration | `AuthService.GenerateJwtToken`, `appsettings.json` |
| A8 | No audit of sign-ins, failed sign-ins, user/role changes, or clinical edits | `AuditLogs` unused |
| A9 | Raw exception messages shown to users | `ExceptionHandlingMiddleware.cs:117` |
| A10 | Authorization rules scattered across policies, inline role strings, controller code and ~50 view checks; no central permission map | §3 |
