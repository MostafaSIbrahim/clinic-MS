# 06 — Data Model Summary

Phase 2 deliverable. Sources: EF model (`SafyaClinic.Domain/Entities/**`, `SafyaClinic.Infrastructure/Data/Configurations/*`, `SafyaDbContextModelSnapshot.cs`) and the live dev database `SafyaClinicDb`, read with SELECT-only catalog queries on 2026-10-26 (`.assessment/out/db-*.csv`). The live schema matches the EF model for all 31 tables, every column and all 101 indexes (see `01-…` §15).

Conventions in this database:
- Every table has an `int IDENTITY` primary key named `Id` (`BaseEntity`).
- Audit columns `CreatedAt` / `CreatedBy` / `UpdatedAt` come from `AuditableEntity`, but are not applied uniformly (e.g. `Payments` has its own `CreatedAt` and `LastModifiedAt/By`; lookups have only `CreatedAt`).
- Timestamps are `datetime2` and written as UTC by the application; some newer columns say so in their name (`CheckedInAtUtc`, `FollowUpUpdatedAtUtc`), older ones do not.
- Money is `decimal(18,2)`; percentages `decimal(5,2)`.
- C# enums are stored as `nvarchar(20)` strings (`Category`, `Status`, `QueueStatus`, `PaymentMethod`, `FollowUpStatus`, `DietCompliance`), except `Patients.Gender` and `Patients.BloodType` (`int`).
- Reservation status is a **lookup table** (`ReservationStatuses`) referenced by `StatusId`, while code compares by `StatusName` strings.

## 1. Entity map by area

```
Identity            Users ──< UserRoles >── Roles
                      │
Patients            Patients ──< PatientPhones
                      │  └──< PatientAddresses
                      │  └── PatientSource (optional)     └── User (optional, unused "patient portal" link)
Scheduling          Reservations ── ReservationStatus, TreatmentType, Clinic, Doctor(User), Patient
                      │
Clinical            PatientRecords (0..1 per reservation) ──< Treatments
                      │                                 ──< Prescriptions ──< PrescriptionItems
                      │                                                    ──< PrescriptionAttachments
                      │                                 ──< MedicalAnalyses ──< AnalysisAttachments
                      └── FollowUpReservation (Reservation, optional)
Lab                 AnalysisTypes ──< MedicalAnalyses (patient, doctor, optional record)
Billing             Payments ── Patient, Reservation?, Enrollment?, Clinic?, PatientSource?, Collector(User)
                      └──< PaymentAdjustments
                    Clinics ──< ClinicSourceAgreements >── PatientSources
Nutrition           NutritionPackages ──< PackageItems ── InjectionType? / VitaminType?
                      └──< PatientNutritionEnrollments (patient, doctor) ──< WeeklyFollowUps
                                                                             ──< WeeklyAdministeredItems ── PackageItem
                                                                             ──< WeeklyFollowUpLabResults ── AnalysisType
Audit               AuditLogs (no FKs, never written)
```

## 2. Tables

Row counts are from the dev database (test data only).

| Area | Table | Rows | Purpose | Key columns / notes |
|---|---|---|---|---|
| Identity | `Users` | 3 | Staff accounts | `PhoneNumber` nvarchar(20) **unique** (login id), `Email` unique (filtered), `PasswordHash`, `Specialization`, `LicenseNumber`, `IsActive`, `LastLoginAt` |
| Identity | `Roles` | 6 | Role names | `RoleName` unique |
| Identity | `UserRoles` | 4 | User↔role | unique `(UserId, RoleId)`, `AssignedBy` |
| Patients | `Patients` | 9 | Patient master | names, `DateOfBirth`, `Gender` int, `BloodType` int, `NationalId` unique (filtered), `HeightCm`, `Weight`, `Allergies`, `ChronicDiseases`, `PatientSourceId`, `UserId` |
| Patients | `PatientPhones` | 9 | Phones | `PhoneNumber`, `PhoneType`, `IsPrimary`; **no index on PhoneNumber** |
| Patients | `PatientAddresses` | 9 | Addresses | `City`, `Governorate`, `IsPrimary` |
| Scheduling | `Reservations` | 39 | Appointments + live queue | `ReservationDate` (date part) + `ReservationTime` `time`, `DurationMinutes`, `StatusId`, `TreatmentTypeId`, `Category`, `ClinicId`, `IsPaid`, `TotalAmount`, `QueueStatus`, `CheckedInAtUtc`, `ConsultationStartedAtUtc`, `QueueEndedAtUtc` |
| Scheduling | `ReservationStatuses` | 5 | Pending, Confirmed, Completed, Cancelled, NoShow | `StatusName` unique, `ColorCode` (#hex) used for badges |
| Clinical | `PatientRecords` | 6 | Encounter / medical record | `ReservationId` **unique (filtered)** → at most one record per reservation; diagnosis fields; `IsLocked`; follow-up tracking (`FollowUpDate`, `FollowUpStatus`, `FollowUpReservationId`, dismissal reason) |
| Clinical | `Treatments` | 1 | Procedures on a record | `Description`, `Cost`, `PerformedDate` |
| Clinical | `TreatmentTypes` | 8 | Service catalogue (priced) | `Category`, `DefaultCost`, `DurationMinutes`, `IsActive`; drives reservation price |
| Clinical | `Prescriptions` | 7 | Prescription header | `RecordId`, `IsPrinted`, `PrescriptionDate`; plus **shadow `PatientRecordId`** |
| Clinical | `PrescriptionItems` | 8 | Medication lines | free-text `MedicationName`, `Dosage`, `Frequency`, `Duration`, route, instructions (no drug master FK) |
| Clinical | `PrescriptionAttachments` | 1 | Uploaded files | `FilePath` (absolute server path), `ContentType` nvarchar(max) |
| Lab | `AnalysisTypes` | 14 | Lab test catalogue | `DefaultCost`, `PreparationInstructions` |
| Lab | `MedicalAnalyses` | 19 | Lab requests/results | `Status` string, `RequestDate`, `ResultDate`, `ResultNotes`, `IsUrgent`, optional `RecordId`; plus **shadow `AnalysisTypeId1`** |
| Lab | `AnalysisAttachments` | 3 | Result files | `ContentType` **nvarchar(50)** (see §5) |
| Billing | `Payments` | 38 | Money collected | `Amount`, `PaymentMethod`, `PaymentDate` (UTC), source/clinic deduction snapshot (`DeductionPercentage`, `SourceDeductionAmount`, `ClinicNetAmount`, `IsFirstVisitDeduction`), `Status` (Active/Cancelled), cancellation and last-modified fields, `OriginalAmount` |
| Billing | `PaymentAdjustments` | 3 | Payment audit trail | `ActionType`, `OldAmount`, `NewAmount`, `Reason`, `PerformedBy/At` |
| Billing | `PatientSources` | 6 | Referral channel | `DefaultDeductionPercentage` |
| Billing | `Clinics` | 3 | Clinic locations | `Name` unique |
| Billing | `ClinicSourceAgreements` | 7 | Per-clinic deduction override | unique `(ClinicId, PatientSourceId)` |
| Nutrition | `NutritionPackages` | 5 | Programme definitions | `DurationWeeks`, `SessionsPerWeek`, `BasePrice`, `MaxDiscountPercent` |
| Nutrition | `PackageItems` | 20 | Planned injections/vitamins per week | `InjectionId?`, `VitaminId?`, `Quantity`, `WeekNumber` |
| Nutrition | `InjectionTypes`, `VitaminTypes` | 7, 5 | Catalogues | |
| Nutrition | `PatientNutritionEnrollments` | 12 | Patient on a package | `BasePrice`, `DiscountPercent`, `Status`, `TotalPaid`, `StartDate`, `EndDate`, `DoctorId` |
| Nutrition | `WeeklyFollowUps` | 25 | Weekly visit measurements | weight, height, **`BMI` computed column** (`CASE WHEN HeightCm > 0 …`), body composition, BP, compliance, notes, `IsCompleted` |
| Nutrition | `WeeklyAdministeredItems` | 0 | What was actually given | `PackageItemId`, `ActualQuantity`, `AdministeredBy`; plus **shadow `PackageItemId1`** |
| Nutrition | `WeeklyFollowUpLabResults` | 0 | Lab values per follow-up | `AnalysisTypeId`, `ResultValue` nvarchar(max) |
| Audit | `AuditLogs` | 0 | Generic change log | **No code writes to it** |

## 3. Relationships and delete behaviour

51 FK columns, all enabled and trusted. Delete rules from the live database:

| Rule | Relationships |
|---|---|
| **CASCADE** (20) | Analysis → AnalysisAttachments; Clinics → ClinicSourceAgreements; PatientSources → ClinicSourceAgreements; NutritionPackages → PackageItems; NutritionPackages → PatientNutritionEnrollments; Patients → PatientAddresses, PatientPhones, PatientNutritionEnrollments; **Users → Payments (`CollectedBy`)**; **Users → PatientNutritionEnrollments (`DoctorId`)**; Payments → PaymentAdjustments; Prescriptions → PrescriptionAttachments, PrescriptionItems; PatientRecords → Prescriptions (`RecordId`), Treatments; Roles/Users → UserRoles; AnalysisTypes → WeeklyFollowUpLabResults; WeeklyFollowUps → WeeklyFollowUpLabResults; Enrollments → WeeklyFollowUps |
| SET NULL (2) | Patients.PatientSourceId; Patients.UserId |
| NO ACTION (29) | Everything else, incl. Reservations → Patients/Users/Statuses/TreatmentTypes/Clinics, PatientRecords → Patients/Reservations/Users, Payments → Patients/Reservations/Enrollments, WeeklyAdministeredItems → WeeklyFollowUps |

Consequences to evaluate in Phase 3:
- Deleting a `User` would cascade-delete every payment they collected and every enrollment they own. No user-delete action exists today (users are deactivated), so this is latent.
- Deleting a nutrition package or enrollment cascades to follow-ups, but `WeeklyAdministeredItems.FollowUpId` is NO ACTION, so the cascade fails once any item has been administered.
- Deleting an `AnalysisType` cascades into nutrition lab results while being blocked by `MedicalAnalyses` (NO ACTION) — inconsistent.

## 4. Indexes

101 non-PK indexes. Every FK column has an index (EF convention). Additional ones:

| Purpose | Index |
|---|---|
| Uniqueness | `Users.PhoneNumber`; `Users.Email` (filtered); `Patients.NationalId` (filtered); `PatientRecords.ReservationId` (filtered); `Roles.RoleName`; `ReservationStatuses.StatusName`; `Clinics.Name`; `PatientSources.Name`; `ClinicSourceAgreements(ClinicId, PatientSourceId)`; `UserRoles(UserId, RoleId)` |
| Doctor schedule | `Reservations(DoctorId, ReservationDate, ReservationTime, Id)` — not unique, so no double-booking protection at DB level |
| Live queue | `Reservations(ClinicId, DoctorId, QueueStatus, CheckedInAtUtc)` |
| Follow-up work list | `PatientRecords(FollowUpStatus, FollowUpDate, DoctorId)` |
| Revenue / reports | `Payments(Status, PaymentDate)` |
| Lists/filters | `Reservations.ReservationDate`, `Reservations.Category`, `MedicalAnalyses.Status`, `Enrollments.Status`, `WeeklyFollowUps.FollowUpDate`, `NutritionPackages.IsActive` |

Not indexed: `PatientPhones.PhoneNumber` (reception searches by phone), patient names (searched with `LIKE '%term%'`, which cannot use an index anyway).

## 5. Data-quality and modelling observations

| # | Observation | Evidence |
|---|---|---|
| D1 | Three shadow FK columns from duplicated relationship mappings: `MedicalAnalyses.AnalysisTypeId1`, `Prescriptions.PatientRecordId`, `WeeklyAdministeredItems.PackageItemId1` | live columns + FKs; `SafyaDbContext.OnModelCreating` lines 81–89 duplicate configuration-class mappings |
| D2 | `AnalysisAttachments.ContentType` is `nvarchar(50)`, but uploads store the browser-supplied MIME type; Office types such as `application/vnd.openxmlformats-officedocument.wordprocessingml.document` (71 chars) would exceed it. `PrescriptionAttachments.ContentType` is `nvarchar(max)` — inconsistent | `db-04-columns.csv`; `AnalysisController.UploadAttachment`. Failure mode: Likely (not reproduced) |
| D3 | Attachment `FilePath` stores absolute server paths | `AnalysisController.cs:180–189`, `MedicalRecordsController.SaveFileAsync` |
| D4 | Reservation status modelled twice: lookup table with IDs **and** an unused `ReservationStatusEnum`; code matches by name strings and hard-codes `StatusId = 1` | `ReservationService.cs:71`; `Domain/Enums/ReservationStatusEnum.cs` |
| D5 | Leftover defaults: `Payments.Status` default `''`, `Prescriptions.PrescriptionDate` default `0001-01-01`, `Reservations.TreatmentTypeId` default `1`, `Reservations.ClinicId` default `0` | `db-08-check-default-constraints.csv` |
| D6 | No CHECK constraints (e.g. amounts ≥ 0, percentage 0–100, `EndDate ≥ StartDate`); validation lives only in services | `db-08` contains defaults only |
| D7 | No concurrency tokens (`rowversion`); concurrency handled by `sp_getapplock` in selected service operations | snapshot; `UnitOfWork.ExecuteLockedAsync` |
| D8 | Patient identity: `NationalId` optional; phone numbers not unique or indexed, so duplicate patients are not prevented | `PatientService.cs:29, 387` |
| D9 | `Patients.UserId` and role `Patient` exist for a patient portal that does not exist | entity + `DbSeeder` |
| D10 | `AuditLogs` table unused; payments have their own trail (`PaymentAdjustments`), clinical changes have none | grep: no writes |
| D11 | Migration history drift: DB has `20260725134425_PatientSourceAndPaymentOptions`, absent from code and git | `db-02-migrations.csv` |
| D12 | `PrescriptionItems.MedicationName` is free text; the DwaPrices autocomplete only assists typing, no drug code is stored | entity + `medicine-autocomplete.js` |

## 6. Seed / reference data

`DbSeeder.SeedAsync` runs at every start and inserts only when each table is empty: roles (Admin, Doctor, Reception, Patient, Nutritionist), an initial admin user, 14 analysis types, 7 treatment types, 6 injection types, 6 vitamin types, 5 reservation statuses, 1 clinic, 6 patient sources (with default deduction %). Migration `20260906120000_SeedNutritionFollowUpTreatmentType` also seeds the zero-cost nutrition "Follow-up" treatment type. The dev database has 6 roles (one added through the UI, presumably `ReceptionAdmin`).
