# 05 — Route and Screen Inventory

Phase 2 deliverable. Generated from source on 2026-10-26 at `main` = `e564582` by parsing every controller action and view, then checked by hand. Routing is conventional (`{controller=Home}/{action=Index}/{id?}`) except the two API controllers.

**Effective roles** = intersection of the class-level and action-level requirements (ASP.NET Core requires *all* `[Authorize]` attributes to pass). Policy → role mapping comes from `Program.cs:38–46`. `AF` = `[ValidateAntiForgeryToken]`.

## Summary

- 158 action methods in 17 controllers; 77 POST (all with antiforgery), 81 GET.
- 85 Razor files: 73 full views, 12 partials/infrastructure; 6 standalone documents (`Layout = null`).
- Access distribution by effective roles:

| Effective roles | Actions |
|---|---|
| Admin | 60 |
| Admin, Doctor | 35 |
| Admin, Doctor, Nutritionist, Reception | 24 |
| Admin, Nutritionist | 14 |
| Admin, Reception, ReceptionAdmin | 9 |
| Admin, Doctor, Reception | 5 |
| Anonymous | 4 |
| Any signed-in user | 4 |
| Anonymous (no attribute) | 1 |
| Admin, Doctor, Reception, ReceptionAdmin | 1 |
| Admin, Reception | 1 |

- `Home/Error` has no attribute and is reachable anonymously (intended for error display). `Home/Index`, `Auth/Login`, `Auth/AccessDenied` are explicitly anonymous.
- Unreferenced view: `Reservations/Index-old.cshtml` (dead). `Payments/Unpaid.cshtml` is used via a conditional in `PaymentsController.Dashboard` (`PaymentsController.cs:167`).

## Routes by controller

### AnalysisController

Class-level: `DoctorOrAdmin`

| Verb | URL | Action-level | Effective roles | AF | Renders | Source |
|---|---|---|---|---|---|---|
| GET | `/Analysis/Index` | — | Admin, Doctor |  | `Analysis/Index.cshtml` | `AnalysisController.cs:32` |
| GET | `/Analysis/PatientAnalyses` | — | Admin, Doctor |  | `Analysis/PatientAnalyses.cshtml` | `AnalysisController.cs:56` |
| GET | `/Analysis/Details` | — | Admin, Doctor |  | `Analysis/Details.cshtml` | `AnalysisController.cs:63` |
| GET | `/Analysis/Request` | — | Admin, Doctor |  | `Analysis/Request.cshtml` | `AnalysisController.cs:74` |
| POST | `/Analysis/Request` | — | Admin, Doctor | ✓ | `Analysis/Request.cshtml` | `AnalysisController.cs:89` |
| GET | `/Analysis/PrintRequestSlip` | — | Admin, Doctor |  | `Analysis/PrintRequestSlip.cshtml` | `AnalysisController.cs:118` |
| GET | `/Analysis/PrintPatientRequests` | — | Admin, Doctor |  | redirect | `AnalysisController.cs:144` |
| POST | `/Analysis/UpdateStatus` | — | Admin, Doctor | ✓ | redirect | `AnalysisController.cs:163` |
| POST | `/Analysis/UploadAttachment` | ClinicalStaff | Admin, Doctor | ✓ | redirect | `AnalysisController.cs:172` |
| GET | `/Analysis/ViewAttachment` | — | Admin, Doctor |  | `file` | `AnalysisController.cs:196` |
| GET | `/Analysis/DownloadAttachment` | — | Admin, Doctor |  | `file` | `AnalysisController.cs:206` |
| POST | `/Analysis/DeleteAttachment` | AdminOnly | Admin | ✓ | redirect | `AnalysisController.cs:241` |

### MedicinesController

Class-level: `ClinicalStaff`

| Verb | URL | Action-level | Effective roles | AF | Renders | Source |
|---|---|---|---|---|---|---|
| GET | `/api/medicines/search` | — | Admin, Doctor, Nutritionist, Reception |  | `JSON` | `MedicinesController.cs:23` |

### PatientsApiController

Class-level: `ClinicalStaff`

| Verb | URL | Action-level | Effective roles | AF | Renders | Source |
|---|---|---|---|---|---|---|
| GET | `/api/patients/search` | — | Admin, Doctor, Nutritionist, Reception |  | `JSON` | `PatientsApiController.cs:21` |

### AuthController

Class-level: `—`

| Verb | URL | Action-level | Effective roles | AF | Renders | Source |
|---|---|---|---|---|---|---|
| GET | `/Auth/Login` | Anonymous | Anonymous |  | `Auth/Login.cshtml` | `AuthController.cs:20` |
| POST | `/Auth/Login` | Anonymous | Anonymous | ✓ | `Auth/Login.cshtml` | `AuthController.cs:32` |
| POST | `/Auth/Logout` | Authenticated | Any signed-in user | ✓ | redirect | `AuthController.cs:75` |
| GET | `/Auth/AccessDenied` | Anonymous | Anonymous |  | `Auth/AccessDenied.cshtml` | `AuthController.cs:83` |
| GET | `/Auth/ChangePassword` | Authenticated | Any signed-in user |  | `Auth/ChangePassword.cshtml` | `AuthController.cs:87` |
| POST | `/Auth/ChangePassword` | Authenticated | Any signed-in user | ✓ | `Auth/ChangePassword.cshtml` | `AuthController.cs:92` |

### ClinicsController

Class-level: `AdminOnly`

| Verb | URL | Action-level | Effective roles | AF | Renders | Source |
|---|---|---|---|---|---|---|
| GET | `/Clinics/Index` | — | Admin |  | `Clinics/Index.cshtml` | `ClinicsController.cs:20` |
| GET | `/Clinics/Create` | — | Admin |  | `Clinics/Create.cshtml` | `ClinicsController.cs:27` |
| POST | `/Clinics/Create` | — | Admin | ✓ | `Clinics/Create.cshtml` | `ClinicsController.cs:31` |
| GET | `/Clinics/Edit` | — | Admin |  | `Clinics/Edit.cshtml` | `ClinicsController.cs:40` |
| POST | `/Clinics/Edit` | — | Admin | ✓ | `Clinics/Edit.cshtml` | `ClinicsController.cs:57` |
| POST | `/Clinics/Delete` | — | Admin | ✓ | redirect | `ClinicsController.cs:67` |
| GET | `/Clinics/Details` | — | Admin |  | `Clinics/Details.cshtml` | `ClinicsController.cs:77` |
| POST | `/Clinics/SaveAgreement` | — | Admin | ✓ | redirect | `ClinicsController.cs:90` |
| POST | `/Clinics/RemoveAgreement` | — | Admin | ✓ | redirect | `ClinicsController.cs:100` |

### DashboardController

Class-level: `Authenticated`

| Verb | URL | Action-level | Effective roles | AF | Renders | Source |
|---|---|---|---|---|---|---|
| GET | `/Dashboard/Index` | — | Any signed-in user |  | `Dashboard/Index.cshtml` | `DashboardController.cs:22` |

### FollowUpsController

Class-level: `Roles:Admin,Reception,Doctor`

| Verb | URL | Action-level | Effective roles | AF | Renders | Source |
|---|---|---|---|---|---|---|
| GET | `/FollowUps/Index` | — | Admin, Doctor, Reception |  | `FollowUps/Index.cshtml` | `FollowUpsController.cs:33` |
| GET | `/FollowUps/Book` | — | Admin, Doctor, Reception |  | `FollowUps/Book.cshtml` | `FollowUpsController.cs:95` |
| POST | `/FollowUps/Book` | — | Admin, Doctor, Reception | ✓ | `FollowUps/Book.cshtml` | `FollowUpsController.cs:117` |
| POST | `/FollowUps/Dismiss` | — | Admin, Doctor, Reception | ✓ | redirect | `FollowUpsController.cs:243` |

### HomeController

Class-level: `—`

| Verb | URL | Action-level | Effective roles | AF | Renders | Source |
|---|---|---|---|---|---|---|
| GET | `/Home/Index` | Anonymous | Anonymous |  | `Home/Index.cshtml` | `HomeController.cs:9` |
| GET | `/Home/Error` | — | Anonymous (no attribute) |  | `Shared/Error.cshtml` | `HomeController.cs:17` |

### LookupsController

Class-level: `Roles:Admin`

| Verb | URL | Action-level | Effective roles | AF | Renders | Source |
|---|---|---|---|---|---|---|
| GET | `/Lookups/Index` | — | Admin |  | `Lookups/Index.cshtml` | `LookupsController.cs:24` |
| GET | `/Lookups/AnalysisTypes` | — | Admin |  | `Lookups/AnalysisTypes.cshtml` | `LookupsController.cs:30` |
| POST | `/Lookups/CreateAnalysisType` | — | Admin | ✓ | `Lookups/AnalysisTypes.cshtml` | `LookupsController.cs:38` |
| GET | `/Lookups/TreatmentTypes` | — | Admin |  | `Lookups/TreatmentTypes.cshtml` | `LookupsController.cs:51` |
| GET | `/Lookups/TreatmentTypes` | — | Admin |  | `Lookups/TreatmentTypes.cshtml` | `LookupsController.cs:56` |
| POST | `/Lookups/CreateTreatmentType` | — | Admin | ✓ | `Lookups/TreatmentTypes.cshtml` | `LookupsController.cs:68` |
| GET | `/Lookups/InjectionTypes` | — | Admin |  | `Lookups/InjectionTypes.cshtml` | `LookupsController.cs:81` |
| POST | `/Lookups/CreateInjectionType` | — | Admin | ✓ | `Lookups/InjectionTypes.cshtml` | `LookupsController.cs:89` |
| GET | `/Lookups/VitaminTypes` | — | Admin |  | `Lookups/VitaminTypes.cshtml` | `LookupsController.cs:102` |
| POST | `/Lookups/CreateVitaminType` | — | Admin | ✓ | `Lookups/VitaminTypes.cshtml` | `LookupsController.cs:110` |
| POST | `/Lookups/SaveTreatmentType` | — | Admin | ✓ | redirect | `LookupsController.cs:124` |
| POST | `/Lookups/DeleteTreatmentType` | — | Admin | ✓ | redirect | `LookupsController.cs:212` |
| POST | `/Lookups/SaveInjectionType` | — | Admin | ✓ | redirect | `LookupsController.cs:248` |
| POST | `/Lookups/DeleteInjectionType` | — | Admin | ✓ | redirect | `LookupsController.cs:295` |
| POST | `/Lookups/SaveVitaminType` | — | Admin | ✓ | redirect | `LookupsController.cs:328` |
| POST | `/Lookups/DeleteVitaminType` | — | Admin | ✓ | redirect | `LookupsController.cs:374` |

### MedicalRecordsController

Class-level: `DoctorOrAdmin`

| Verb | URL | Action-level | Effective roles | AF | Renders | Source |
|---|---|---|---|---|---|---|
| GET | `/MedicalRecords/PatientRecords` | — | Admin, Doctor |  | `MedicalRecords/PatientRecords.cshtml` | `MedicalRecordsController.cs:35` |
| GET | `/MedicalRecords/Details` | — | Admin, Doctor |  | `MedicalRecords/Details.cshtml` | `MedicalRecordsController.cs:42` |
| GET | `/MedicalRecords/Create` | — | Admin, Doctor |  | `MedicalRecords/Create.cshtml` | `MedicalRecordsController.cs:84` |
| POST | `/MedicalRecords/Create` | — | Admin, Doctor | ✓ | `MedicalRecords/Create.cshtml` | `MedicalRecordsController.cs:115` |
| GET | `/MedicalRecords/Edit` | — | Admin, Doctor |  | `MedicalRecords/Edit.cshtml` | `MedicalRecordsController.cs:130` |
| POST | `/MedicalRecords/Edit` | — | Admin, Doctor | ✓ | `MedicalRecords/Edit.cshtml` | `MedicalRecordsController.cs:150` |
| POST | `/MedicalRecords/Lock` | AdminOnly | Admin | ✓ | redirect | `MedicalRecordsController.cs:161` |
| POST | `/MedicalRecords/AddTreatment` | — | Admin, Doctor | ✓ | redirect | `MedicalRecordsController.cs:171` |
| POST | `/MedicalRecords/RemoveTreatment` | — | Admin, Doctor | ✓ | redirect | `MedicalRecordsController.cs:180` |
| POST | `/MedicalRecords/CreatePrescription` | — | Admin, Doctor | ✓ | `MedicalRecords/CreatePrescription.cshtml` | `MedicalRecordsController.cs:190` |
| GET | `/MedicalRecords/CreatePrescription` | — | Admin, Doctor |  | `MedicalRecords/CreatePrescription.cshtml` | `MedicalRecordsController.cs:198` |
| GET | `/MedicalRecords/PrescriptionDetails` | — | Admin, Doctor |  | `MedicalRecords/PrescriptionDetails.cshtml` | `MedicalRecordsController.cs:204` |
| POST | `/MedicalRecords/AddPrescriptionItem` | — | Admin, Doctor | ✓ | redirect | `MedicalRecordsController.cs:213` |
| POST | `/MedicalRecords/RemovePrescriptionItem` | — | Admin, Doctor | ✓ | redirect | `MedicalRecordsController.cs:222` |
| POST | `/MedicalRecords/MarkPrescriptionPrinted` | — | Admin, Doctor | ✓ | redirect | `MedicalRecordsController.cs:230` |
| POST | `/MedicalRecords/UploadPrescriptionAttachment` | ClinicalStaff | Admin, Doctor | ✓ | redirect | `MedicalRecordsController.cs:240` |
| POST | `/MedicalRecords/DeleteAttachment` | AdminOnly | Admin | ✓ | redirect | `MedicalRecordsController.cs:260` |
| GET | `/MedicalRecords/ViewAttachment` | — | Admin, Doctor |  | `file` | `MedicalRecordsController.cs:268` |
| GET | `/MedicalRecords/DownloadAttachment` | — | Admin, Doctor |  | `file` | `MedicalRecordsController.cs:276` |
| GET | `/MedicalRecords/PrintPrescription` | — | Admin, Doctor |  | `MedicalRecords/PrintPrescription.cshtml` | `MedicalRecordsController.cs:310` |
| GET | `/MedicalRecords/Consultation` | — | Admin, Doctor |  | `MedicalRecords/Create.cshtml` | `MedicalRecordsController.cs:346` |
| POST | `/MedicalRecords/CompleteConsultation` | DoctorOrAdmin | Admin, Doctor | ✓ | redirect | `MedicalRecordsController.cs:381` |

### NutritionController

Class-level: `NutritionTeam`

| Verb | URL | Action-level | Effective roles | AF | Renders | Source |
|---|---|---|---|---|---|---|
| GET | `/Nutrition/Packages` | — | Admin, Nutritionist |  | `Nutrition/Packages.cshtml` | `NutritionController.cs:26` |
| GET | `/Nutrition/PackageDetails` | — | Admin, Nutritionist |  | `Nutrition/PackageDetails.cshtml` | `NutritionController.cs:32` |
| GET | `/Nutrition/CreatePackage` | AdminOnly | Admin |  | `Nutrition/CreatePackage.cshtml` | `NutritionController.cs:41` |
| POST | `/Nutrition/CreatePackage` | AdminOnly | Admin | ✓ | `Nutrition/CreatePackage.cshtml` | `NutritionController.cs:50` |
| POST | `/Nutrition/DeactivatePackage` | AdminOnly | Admin | ✓ | redirect | `NutritionController.cs:78` |
| GET | `/Nutrition/PatientEnrollments` | — | Admin, Nutritionist |  | `Nutrition/PatientEnrollments.cshtml` | `NutritionController.cs:86` |
| GET | `/Nutrition/EnrollmentDetails` | — | Admin, Nutritionist |  | `Nutrition/EnrollmentDetails.cshtml` | `NutritionController.cs:93` |
| GET | `/Nutrition/Enroll` | — | Admin, Nutritionist |  | `Nutrition/Enroll.cshtml` | `NutritionController.cs:101` |
| POST | `/Nutrition/Enroll` | — | Admin, Nutritionist | ✓ | `Nutrition/Enroll.cshtml` | `NutritionController.cs:116` |
| POST | `/Nutrition/UpdateEnrollmentStatus` | — | Admin, Nutritionist | ✓ | redirect | `NutritionController.cs:135` |
| GET | `/Nutrition/FollowUpDetails` | — | Admin, Nutritionist |  | `Nutrition/FollowUpDetails.cshtml` | `NutritionController.cs:143` |
| GET | `/Nutrition/RecordFollowUp` | — | Admin, Nutritionist |  | `Nutrition/RecordFollowUp.cshtml` | `NutritionController.cs:151` |
| POST | `/Nutrition/RecordFollowUp` | — | Admin, Nutritionist | ✓ | `Nutrition/RecordFollowUp.cshtml` | `NutritionController.cs:156` |
| POST | `/Nutrition/CompleteFollowUp` | — | Admin, Nutritionist | ✓ | redirect | `NutritionController.cs:175` |
| GET | `/Nutrition/EditFollowUp` | — | Admin, Nutritionist |  | `Nutrition/RecordFollowUp.cshtml` | `NutritionController.cs:183` |
| POST | `/Nutrition/EditFollowUp` | — | Admin, Nutritionist | ✓ | `Nutrition/RecordFollowUp.cshtml` | `NutritionController.cs:221` |
| POST | `/Nutrition/DeleteFollowUp` | — | Admin, Nutritionist | ✓ | redirect | `NutritionController.cs:235` |
| GET | `/Nutrition/InjectionTypes` | AdminOnly | Admin |  | `Nutrition/InjectionTypes.cshtml` | `NutritionController.cs:247` |
| GET | `/Nutrition/CreateInjectionType` | AdminOnly | Admin |  | `Nutrition/CreateInjectionType.cshtml` | `NutritionController.cs:255` |
| POST | `/Nutrition/CreateInjectionType` | AdminOnly | Admin | ✓ | `Nutrition/CreateInjectionType.cshtml` | `NutritionController.cs:260` |
| GET | `/Nutrition/EditInjectionType` | AdminOnly | Admin |  | `Nutrition/EditInjectionType.cshtml` | `NutritionController.cs:270` |
| POST | `/Nutrition/EditInjectionType` | AdminOnly | Admin | ✓ | `Nutrition/EditInjectionType.cshtml` | `NutritionController.cs:290` |
| POST | `/Nutrition/DeleteInjectionType` | AdminOnly | Admin | ✓ | redirect | `NutritionController.cs:301` |
| GET | `/Nutrition/VitaminTypes` | AdminOnly | Admin |  | `Nutrition/VitaminTypes.cshtml` | `NutritionController.cs:311` |
| GET | `/Nutrition/CreateVitaminType` | AdminOnly | Admin |  | `Nutrition/CreateVitaminType.cshtml` | `NutritionController.cs:319` |
| POST | `/Nutrition/CreateVitaminType` | AdminOnly | Admin | ✓ | `Nutrition/CreateVitaminType.cshtml` | `NutritionController.cs:324` |
| GET | `/Nutrition/EditVitaminType` | AdminOnly | Admin |  | `Nutrition/EditVitaminType.cshtml` | `NutritionController.cs:334` |
| POST | `/Nutrition/EditVitaminType` | AdminOnly | Admin | ✓ | `Nutrition/EditVitaminType.cshtml` | `NutritionController.cs:354` |
| POST | `/Nutrition/DeleteVitaminType` | AdminOnly | Admin | ✓ | redirect | `NutritionController.cs:365` |

### PatientSourcesController

Class-level: `AdminOnly`

| Verb | URL | Action-level | Effective roles | AF | Renders | Source |
|---|---|---|---|---|---|---|
| GET | `/PatientSources/Index` | — | Admin |  | `PatientSources/Index.cshtml` | `PatientSourcesController.cs:16` |
| GET | `/PatientSources/Create` | — | Admin |  | `PatientSources/Create.cshtml` | `PatientSourcesController.cs:23` |
| POST | `/PatientSources/Create` | — | Admin | ✓ | `PatientSources/Create.cshtml` | `PatientSourcesController.cs:27` |
| GET | `/PatientSources/Edit` | — | Admin |  | `PatientSources/Edit.cshtml` | `PatientSourcesController.cs:36` |
| POST | `/PatientSources/Edit` | — | Admin | ✓ | `PatientSources/Edit.cshtml` | `PatientSourcesController.cs:53` |
| POST | `/PatientSources/Delete` | — | Admin | ✓ | redirect | `PatientSourcesController.cs:63` |

### PatientsController

Class-level: `ClinicalStaff`

| Verb | URL | Action-level | Effective roles | AF | Renders | Source |
|---|---|---|---|---|---|---|
| GET | `/Patients/Index` | — | Admin, Doctor, Nutritionist, Reception |  | `Patients/Index.cshtml` | `PatientsController.cs:23` |
| GET | `/Patients/Details` | — | Admin, Doctor, Nutritionist, Reception |  | `Patients/Details.cshtml` | `PatientsController.cs:43` |
| GET | `/Patients/Timeline` | DoctorOrAdmin | Admin, Doctor |  | `Patients/Timeline.cshtml` | `PatientsController.cs:58` |
| GET | `/Patients/Create` | — | Admin, Doctor, Nutritionist, Reception |  | `Patients/Create.cshtml` | `PatientsController.cs:87` |
| POST | `/Patients/Create` | — | Admin, Doctor, Nutritionist, Reception | ✓ | `Patients/Create.cshtml`, `Patients/CreateSuccessPopup.cshtml` | `PatientsController.cs:96` |
| GET | `/Patients/EditBasic` | — | Admin, Doctor, Nutritionist, Reception |  | `Patients/EditBasic.cshtml` | `PatientsController.cs:128` |
| POST | `/Patients/EditBasic` | — | Admin, Doctor, Nutritionist, Reception | ✓ | `Patients/EditBasic.cshtml` | `PatientsController.cs:145` |
| GET | `/Patients/EditMedical` | DoctorOrAdmin | Admin, Doctor |  | `Patients/EditMedical.cshtml` | `PatientsController.cs:161` |
| POST | `/Patients/EditMedical` | DoctorOrAdmin | Admin, Doctor | ✓ | `Patients/EditMedical.cshtml` | `PatientsController.cs:182` |
| POST | `/Patients/AddPhone` | — | Admin, Doctor, Nutritionist, Reception | ✓ | `JSON` | `PatientsController.cs:194` |
| POST | `/Patients/RemovePhone` | — | Admin, Doctor, Nutritionist, Reception | ✓ | redirect | `PatientsController.cs:253` |
| POST | `/Patients/AddAddress` | — | Admin, Doctor, Nutritionist, Reception | ✓ | `JSON` | `PatientsController.cs:268` |
| POST | `/Patients/RemoveAddress` | — | Admin, Doctor, Nutritionist, Reception | ✓ | redirect | `PatientsController.cs:318` |

### PaymentsController

Class-level: `Authenticated`

| Verb | URL | Action-level | Effective roles | AF | Renders | Source |
|---|---|---|---|---|---|---|
| GET | `/Payments/PatientSummary` | PaymentStaff | Admin, Reception, ReceptionAdmin |  | `Payments/PatientSummary.cshtml` | `PaymentsController.cs:26` |
| GET | `/Payments/Audit` | PaymentStaff | Admin, Reception, ReceptionAdmin |  | `Payments/Audit.cshtml` | `PaymentsController.cs:35` |
| GET | `/Payments/Report` | PaymentReports | Admin, Doctor, Reception, ReceptionAdmin |  | `Payments/Report.cshtml` | `PaymentsController.cs:50` |
| POST | `/Payments/RecalculateAllPaidStatuses` | AdminOnly | Admin | ✓ | redirect | `PaymentsController.cs:117` |
| POST | `/Payments/BackfillZeroCostPayments` | AdminOnly | Admin | ✓ | redirect | `PaymentsController.cs:141` |
| GET | `/Payments/Dashboard` | PaymentStaff | Admin, Reception, ReceptionAdmin |  | `Payments/Dashboard.cshtml (Admin)`, `Payments/Unpaid.cshtml (others)` | `PaymentsController.cs:160` |
| GET | `/Payments/DashboardLineDetails` | AdminOnly | Admin |  | `Shared/_DashboardLineDetails.cshtml` | `PaymentsController.cs:177` |
| GET | `/Payments/Collect` | PaymentStaff | Admin, Reception, ReceptionAdmin |  | `Payments/Collect.cshtml` | `PaymentsController.cs:190` |
| POST | `/Payments/Collect` | PaymentStaff | Admin, Reception, ReceptionAdmin | ✓ | `Payments/Collect.cshtml` | `PaymentsController.cs:254` |
| GET | `/Payments/Cancel` | PaymentStaff | Admin, Reception, ReceptionAdmin |  | `Payments/Cancel.cshtml` | `PaymentsController.cs:291` |
| POST | `/Payments/Cancel` | PaymentStaff | Admin, Reception, ReceptionAdmin | ✓ | `Payments/Cancel.cshtml` | `PaymentsController.cs:301` |
| GET | `/Payments/ChangeAmount` | PaymentStaff | Admin, Reception, ReceptionAdmin |  | `Payments/ChangeAmount.cshtml` | `PaymentsController.cs:316` |
| POST | `/Payments/ChangeAmount` | PaymentStaff | Admin, Reception, ReceptionAdmin | ✓ | `Payments/ChangeAmount.cshtml` | `PaymentsController.cs:327` |

### ReservationsController

Class-level: `ClinicalStaff`

| Verb | URL | Action-level | Effective roles | AF | Renders | Source |
|---|---|---|---|---|---|---|
| GET | `/Reservations/Index` | — | Admin, Doctor, Nutritionist, Reception |  | `Reservations/Index.cshtml` | `ReservationsController.cs:35` |
| GET | `/Reservations/Today` | — | Admin, Doctor, Nutritionist, Reception |  | `Reservations/Today.cshtml` | `ReservationsController.cs:65` |
| GET | `/Reservations/Queue` | Roles:Admin,Reception,Doctor | Admin, Doctor, Reception |  | `Reservations/Queue.cshtml` | `ReservationsController.cs:73` |
| GET | `/Reservations/Board` | — | Admin, Doctor, Nutritionist, Reception |  | `Reservations/Board.cshtml` | `ReservationsController.cs:163` |
| GET | `/Reservations/Details` | — | Admin, Doctor, Nutritionist, Reception |  | `Reservations/Details.cshtml` | `ReservationsController.cs:194` |
| GET | `/Reservations/Create` | — | Admin, Doctor, Nutritionist, Reception |  | `Reservations/Create.cshtml` | `ReservationsController.cs:212` |
| POST | `/Reservations/Create` | — | Admin, Doctor, Nutritionist, Reception | ✓ | `Reservations/Create.cshtml` | `ReservationsController.cs:243` |
| GET | `/Reservations/Edit` | — | Admin, Doctor, Nutritionist, Reception |  | `Reservations/Edit.cshtml` | `ReservationsController.cs:277` |
| POST | `/Reservations/Edit` | — | Admin, Doctor, Nutritionist, Reception | ✓ | `Reservations/Edit.cshtml` | `ReservationsController.cs:307` |
| POST | `/Reservations/UpdateStatus` | — | Admin, Doctor, Nutritionist, Reception | ✓ | redirect | `ReservationsController.cs:334` |
| POST | `/Reservations/MarkPaid` | — | Admin, Doctor, Nutritionist, Reception | ✓ | redirect | `ReservationsController.cs:363` |
| POST | `/Reservations/Cancel` | — | Admin, Doctor, Nutritionist, Reception | ✓ | redirect | `ReservationsController.cs:371` |
| GET | `/Reservations/PatientReservationHistoryReport` | — | Admin, Doctor, Nutritionist, Reception |  | `Reservations/PatientReservationHistoryReport.cshtml` | `ReservationsController.cs:389` |
| POST | `/Reservations/CheckIn` | ReceptionOrAdmin | Admin, Reception | ✓ | redirect | `ReservationsController.cs:418` |
| POST | `/Reservations/StartConsultation` | DoctorOrAdmin | Admin, Doctor | ✓ | redirect | `ReservationsController.cs:436` |

### RolesController

Class-level: `AdminOnly`

| Verb | URL | Action-level | Effective roles | AF | Renders | Source |
|---|---|---|---|---|---|---|
| GET | `/Roles/Index` | — | Admin |  | `Roles/Index.cshtml` | `RolesController.cs:19` |
| POST | `/Roles/Create` | — | Admin | ✓ | `Roles/Index.cshtml` | `RolesController.cs:25` |

### UsersController

Class-level: `AdminOnly`

| Verb | URL | Action-level | Effective roles | AF | Renders | Source |
|---|---|---|---|---|---|---|
| GET | `/Users/Index` | — | Admin |  | `Users/Index.cshtml` | `UsersController.cs:30` |
| GET | `/Users/Create` | — | Admin |  | `Users/Create.cshtml` | `UsersController.cs:52` |
| POST | `/Users/Create` | — | Admin | ✓ | `Users/Create.cshtml` | `UsersController.cs:60` |
| POST | `/Users/ToggleActive` | — | Admin | ✓ | redirect | `UsersController.cs:74` |
| POST | `/Users/AssignRole` | — | Admin | ✓ | redirect | `UsersController.cs:82` |
| POST | `/Users/RemoveRole` | — | Admin | ✓ | redirect | `UsersController.cs:93` |

## Screen inventory

Primary users are inferred from the effective roles of the routes that render each screen. Lines = file length.

### Analysis

| View | Title | Model | Layout | Reached from | Users | Lines |
|---|---|---|---|---|---|---|
| `Details.cshtml` | Analysis Details | `MedicalAnalysisDto` | _Layout | GET /Analysis/Details | Admin, Doctor | 115 |
| `Index.cshtml` | Analyses | `PagedResult<MedicalAnalysisDto>` | _Layout | GET /Analysis/Index | Admin, Doctor | 113 |
| `PatientAnalyses.cshtml` | Medical Analyses | `IEnumerable<MedicalAnalysisDto>` | _Layout | GET /Analysis/PatientAnalyses | Admin, Doctor | 78 |
| `PrintRequestSlip.cshtml` | Analysis Request Slip | `IEnumerable<MedicalAnalysisDto>` | standalone | GET /Analysis/PrintRequestSlip | Admin, Doctor | 79 |
| `Request.cshtml` | Request Analysis | `RequestAnalysisBatchRequest` | _Layout | GET /Analysis/Request; POST /Analysis/Request | Admin, Doctor | 71 |

### Auth

| View | Title | Model | Layout | Reached from | Users | Lines |
|---|---|---|---|---|---|---|
| `AccessDenied.cshtml` | Access Denied | `—` | _Layout | GET /Auth/AccessDenied | Anonymous | 16 |
| `ChangePassword.cshtml` | Change Password | `ChangePasswordRequest` | _Layout | GET /Auth/ChangePassword; POST /Auth/ChangePassword | Any signed-in user | 36 |
| `Login.cshtml` | Login | `LoginRequest` | _Layout | GET /Auth/Login; POST /Auth/Login | Anonymous | 56 |

### Clinics

| View | Title | Model | Layout | Reached from | Users | Lines |
|---|---|---|---|---|---|---|
| `Create.cshtml` | New Clinic | `CreateClinicRequest` | _Layout | GET /Clinics/Create; POST /Clinics/Create | Admin | 35 |
| `Details.cshtml` | Model.Name | `ClinicDto` | _Layout | GET /Clinics/Details | Admin | 80 |
| `Edit.cshtml` | Edit Clinic | `UpdateClinicRequest` | _Layout | GET /Clinics/Edit; POST /Clinics/Edit | Admin | 39 |
| `Index.cshtml` | Clinics | `IEnumerable<ClinicDto>` | _Layout | GET /Clinics/Index | Admin | 58 |

### Dashboard

| View | Title | Model | Layout | Reached from | Users | Lines |
|---|---|---|---|---|---|---|
| `Index.cshtml` | Dashboard | `DashboardViewModel` | _Layout | GET /Dashboard/Index | Any signed-in user | 128 |

### FollowUps

| View | Title | Model | Layout | Reached from | Users | Lines |
|---|---|---|---|---|---|---|
| `Book.cshtml` | Book Follow-up | `BookFollowUpViewModel` | _Layout | GET /FollowUps/Book; POST /FollowUps/Book | Admin, Doctor, Reception | 74 |
| `Index.cshtml` | Patient Follow-ups | `PatientFollowUpsViewModel` | _Layout | GET /FollowUps/Index | Admin, Doctor, Reception | 279 |

### Home

| View | Title | Model | Layout | Reached from | Users | Lines |
|---|---|---|---|---|---|---|
| `Index.cshtml` | @clinicInfo.Name - @clinicInfo.Tagline | `—` | standalone | GET /Home/Index | Anonymous | 337 |

### Lookups

| View | Title | Model | Layout | Reached from | Users | Lines |
|---|---|---|---|---|---|---|
| `AnalysisTypes.cshtml` | Manage Analysis Types | `AnalysisTyp` | _Layout | GET /Lookups/AnalysisTypes; POST /Lookups/CreateAnalysisType | Admin | 65 |
| `Index.cshtml` | System Lookups Management | `—` | _Layout | GET /Lookups/Index | Admin | 76 |
| `InjectionTypes.cshtml` | أنواع الحقن | `InjectionT` | _Layout | GET /Lookups/InjectionTypes; POST /Lookups/CreateInjectionType | Admin | 248 |
| `TreatmentTypes.cshtml` | Treatment Types | `Treatm` | _Layout | GET /Lookups/TreatmentTypes; POST /Lookups/CreateTreatmentType | Admin | 270 |
| `VitaminTypes.cshtml` | أنواع الفيتامينات | `VitaminTyp` | _Layout | GET /Lookups/VitaminTypes; POST /Lookups/CreateVitaminType | Admin | 247 |

### MedicalRecords

| View | Title | Model | Layout | Reached from | Users | Lines |
|---|---|---|---|---|---|---|
| `Create.cshtml` | New Medical Record | `CreatePatientRecordRequest` | _Layout | GET /MedicalRecords/Consultation; GET /MedicalRecords/Create; POST /MedicalRecords/Create | Admin, Doctor | 86 |
| `CreatePrescription.cshtml` | New Prescription | `CreatePrescriptionRequest` | _Layout | GET /MedicalRecords/CreatePrescription; POST /MedicalRecords/CreatePrescription | Admin, Doctor | 89 |
| `Details.cshtml` | Medical Record | `PatientRecordDto` | _Layout | GET /MedicalRecords/Details | Admin, Doctor | 304 |
| `Edit.cshtml` | Edit Medical Record | `UpdatePatientRecordRequest` | _Layout | GET /MedicalRecords/Edit; POST /MedicalRecords/Edit | Admin, Doctor | 77 |
| `PatientRecords.cshtml` | Medical Records | `IEnumerable<PatientRecordDto>` | _Layout | GET /MedicalRecords/PatientRecords | Admin, Doctor | 52 |
| `PrescriptionDetails.cshtml` | $"Prescription #{Model.Id} | `PrescriptionDetailDto` | _Layout | GET /MedicalRecords/PrescriptionDetails | Admin, Doctor | 158 |
| `PrintPrescription.cshtml` | Print Prescription | `PrescriptionPrintDto` | standalone | GET /MedicalRecords/PrintPrescription | Admin, Doctor | 226 |

### Nutrition

| View | Title | Model | Layout | Reached from | Users | Lines |
|---|---|---|---|---|---|---|
| `CreateInjectionType.cshtml` | Add Injection Type | `CreateInjectionTypeDto` | _Layout | GET /Nutrition/CreateInjectionType; POST /Nutrition/CreateInjectionType | Admin | 39 |
| `CreatePackage.cshtml` | Create Nutrition Package | `CreateNutritionPackageDto` | _Layout | GET /Nutrition/CreatePackage; POST /Nutrition/CreatePackage | Admin | 142 |
| `CreateVitaminType.cshtml` | Add Vitamin Type | `CreateVitaminTypeDto` | _Layout | GET /Nutrition/CreateVitaminType; POST /Nutrition/CreateVitaminType | Admin | 39 |
| `EditInjectionType.cshtml` | Edit Injection Type | `UpdateInjectionTypeDto` | _Layout | GET /Nutrition/EditInjectionType; POST /Nutrition/EditInjectionType | Admin | 53 |
| `EditVitaminType.cshtml` | Edit Vitamin Type | `UpdateVitaminTypeDto` | _Layout | GET /Nutrition/EditVitaminType; POST /Nutrition/EditVitaminType | Admin | 53 |
| `Enroll.cshtml` | Enroll Patient | `CreateEnrollmentDto` | _Layout | GET /Nutrition/Enroll; POST /Nutrition/Enroll | Admin, Nutritionist | 110 |
| `EnrollmentDetails.cshtml` | Enrollment Details | `PatientEnrollmentDto` | _Layout | GET /Nutrition/EnrollmentDetails | Admin, Nutritionist | 168 |
| `FollowUpDetails.cshtml` | $"Week {Model.WeekNumber} Follow-Up | `WeeklyFollowUpDto` | _Layout | GET /Nutrition/FollowUpDetails | Admin, Nutritionist | 119 |
| `InjectionTypes.cshtml` | Injection Types | `IEnumerable<InjectionTypeDto>` | _Layout | GET /Nutrition/InjectionTypes | Admin | 67 |
| `PackageDetails.cshtml` | Model.PackageName | `NutritionPackageDto` | _Layout | GET /Nutrition/PackageDetails | Admin, Nutritionist | 75 |
| `Packages.cshtml` | Nutrition Packages | `IEnumerable<NutritionPackageDto>` | _Layout | GET /Nutrition/Packages | Admin, Nutritionist | 50 |
| `PatientEnrollments.cshtml` | Nutrition Enrollments | `IEnumerable<PatientEnrollmentDto>` | _Layout | GET /Nutrition/PatientEnrollments | Admin, Nutritionist | 59 |
| `RecordFollowUp.cshtml` | Record Weekly Follow-Up | `RecordFollowUpDto` | _Layout | GET /Nutrition/EditFollowUp; GET /Nutrition/RecordFollowUp; POST /Nutrition/EditFollowUp; POST /Nutrition/RecordFollowUp | Admin, Nutritionist | 174 |
| `VitaminTypes.cshtml` | Vitamin Types | `IEnumerable<VitaminTypeDto>` | _Layout | GET /Nutrition/VitaminTypes | Admin | 67 |

### PatientSources

| View | Title | Model | Layout | Reached from | Users | Lines |
|---|---|---|---|---|---|---|
| `Create.cshtml` | New Patient Source | `CreatePatientSourceRequest` | _Layout | GET /PatientSources/Create; POST /PatientSources/Create | Admin | 38 |
| `Edit.cshtml` | Edit Patient Source | `UpdatePatientSourceRequest` | _Layout | GET /PatientSources/Edit; POST /PatientSources/Edit | Admin | 39 |
| `Index.cshtml` | Patient Sources | `IEnumerable<PatientSourceDto>` | _Layout | GET /PatientSources/Index | Admin | 55 |

### Patients

| View | Title | Model | Layout | Reached from | Users | Lines |
|---|---|---|---|---|---|---|
| `Create.cshtml` | Register Patient | `CreatePatientRequest` | _Layout | GET /Patients/Create; POST /Patients/Create | Admin, Doctor, Nutritionist, Reception | 157 |
| `CreateSuccessPopup.cshtml` | Patient Registered | `—` | standalone | POST /Patients/Create | Admin, Doctor, Nutritionist, Reception | 41 |
| `Details.cshtml` | patient.FullName | `PatientDashboardDto` | _Layout | GET /Patients/Details | Admin, Doctor, Nutritionist, Reception | 454 |
| `EditBasic.cshtml` | Edit Basic Info | `UpdatePatientBasicRequest` | _Layout | GET /Patients/EditBasic; POST /Patients/EditBasic | Admin, Doctor, Nutritionist, Reception | 58 |
| `EditMedical.cshtml` | Edit Medical Info | `UpdatePatientMedicalRequest` | _Layout | GET /Patients/EditMedical; POST /Patients/EditMedical | Admin, Doctor | 76 |
| `Index.cshtml` | Patients | `PagedResult<PatientSummaryDto>` | _Layout | GET /Patients/Index | Admin, Doctor, Nutritionist, Reception | 102 |
| `Timeline.cshtml` | Patient Timeline | `PatientTimelineDto` | _Layout | GET /Patients/Timeline | Admin, Doctor | 124 |

### Payments

| View | Title | Model | Layout | Reached from | Users | Lines |
|---|---|---|---|---|---|---|
| `Audit.cshtml` |  | `PaymentAuditDto` | standalone | GET /Payments/Audit | Admin, Reception, ReceptionAdmin | 137 |
| `Cancel.cshtml` | Cancel Payment | `CancelPaymentRequest` | _Layout | GET /Payments/Cancel; POST /Payments/Cancel | Admin, Reception, ReceptionAdmin | 34 |
| `ChangeAmount.cshtml` | Change Payment Amount | `ChangePaymentAmountRequest` | _Layout | GET /Payments/ChangeAmount; POST /Payments/ChangeAmount | Admin, Reception, ReceptionAdmin | 38 |
| `Collect.cshtml` | Collect Payment | `CollectPaymentRequest` | _Layout | GET /Payments/Collect; POST /Payments/Collect | Admin, Reception, ReceptionAdmin | 98 |
| `Dashboard.cshtml` | Payment Dashboard | `PaymentDashboardDto` | _Layout | GET /Payments/Dashboard | Admin, Reception, ReceptionAdmin | 298 |
| `PatientSummary.cshtml` | Payment Summary | `PatientFinancialSummaryDto` | _Layout | GET /Payments/PatientSummary | Admin, Reception, ReceptionAdmin | 326 |
| `Report.cshtml` | Payment Report | `IEnumerable<PaymentDto>` | _Layout | GET /Payments/Report | Admin, Doctor, Reception, ReceptionAdmin | 103 |
| `Unpaid.cshtml` | Unpaid Payments | `PaymentDashboardDto` | _Layout | GET /Payments/Dashboard | Admin, Reception, ReceptionAdmin | 17 |
| `_UnpaidSections.cshtml` |  | `PaymentDashboardDto` | partial | partial | — | 86 |

### Reservations

| View | Title | Model | Layout | Reached from | Users | Lines |
|---|---|---|---|---|---|---|
| `Board.cshtml` | Appointment Board | `AppointmentBoardDto` | _Layout | GET /Reservations/Board | Admin, Doctor, Nutritionist, Reception | 175 |
| `Create.cshtml` | New Reservation | `CreateReservationRequest` | _Layout | GET /Reservations/Create; POST /Reservations/Create | Admin, Doctor, Nutritionist, Reception | 149 |
| `Details.cshtml` | Reservation Details | `ReservationDto` | _Layout | GET /Reservations/Details | Admin, Doctor, Nutritionist, Reception | 244 |
| `Edit.cshtml` | Edit Reservation | `UpdateReservationRequest` | _Layout | GET /Reservations/Edit; POST /Reservations/Edit | Admin, Doctor, Nutritionist, Reception | 110 |
| `Index-old.cshtml` | Reservations | `PagedResult<ReservationSummaryDto>` | _Layout | **unreferenced** | — | 95 |
| `Index.cshtml` | Reservations | `PagedResult<ReservationSummaryDto>` | _Layout | GET /Reservations/Index | Admin, Doctor, Nutritionist, Reception | 116 |
| `PatientReservationHistoryReport.cshtml` | تقرير سجل الحجوزات - @(patient?.FullName ?? "مريض") | `IEnumerable<ReservationDto>` | standalone | GET /Reservations/PatientReservationHistoryReport | Admin, Doctor, Nutritionist, Reception | 163 |
| `Queue.cshtml` | Doctor Queues | `DoctorQueueViewModel` | _Layout | GET /Reservations/Queue | Admin, Doctor, Reception | 204 |
| `Today.cshtml` | Today's Reservations | `IEnumerable<ReservationSummaryDto>` | _Layout | GET /Reservations/Today | Admin, Doctor, Nutritionist, Reception | 42 |
| `_BoardAppointment.cshtml` |  | `ReservationSummaryDto` | partial | partial | — | 34 |

### Roles

| View | Title | Model | Layout | Reached from | Users | Lines |
|---|---|---|---|---|---|---|
| `Index.cshtml` | Roles | `RoleLookupViewModel` | _Layout | GET /Roles/Index; POST /Roles/Create | Admin | 64 |

### Shared

| View | Title | Model | Layout | Reached from | Users | Lines |
|---|---|---|---|---|---|---|
| `Error.cshtml` | Error | `—` | _Layout | GET /Home/Error | Anonymous (no attribute) | 13 |
| `_AttachmentPreviewModal.cshtml` |  | `—` | partial | partial | — | 57 |
| `_ClinicLogo.cshtml` |  | `string` | partial | partial | — | 6 |
| `_DashboardLineDetails.cshtml` |  | `PaymentLineDetailReport` | partial | GET /Payments/DashboardLineDetails | Admin | 73 |
| `_Layout.cshtml` | @(ViewData["Title"] != null ? $"{ViewData["Title"]} — " : "")Safya Cli | `—` | partial | partial | — | 270 |
| `_Pagination.cshtml` |  | `PagedResult<object>` | partial | partial | — | 52 |
| `_PaymentStatusToggle.cshtml` |  | `(int ReservationId, int PatientId, bool IsPaid)` | partial | partial | — | 11 |
| `_StatusBadge.cshtml` |  | `(string Label, string Color)` | partial | partial | — | 3 |
| `_ValidationScriptsPartial.cshtml` |  | `—` | partial | partial | — | 4 |

### Users

| View | Title | Model | Layout | Reached from | Users | Lines |
|---|---|---|---|---|---|---|
| `Create.cshtml` | Add User | `CreateUserRequest` | _Layout | GET /Users/Create; POST /Users/Create | Admin | 70 |
| `Index.cshtml` | Users | `PagedResult<UserDto>` | _Layout | GET /Users/Index | Admin | 121 |

## Navigation (sidebar) vs routes

From `Views/Shared/_Layout.cshtml`. Menu visibility is computed in the view with `User.IsInRole` and mirrors, but is not derived from, the server policies.

| Section | Item | Target | Shown to |
|---|---|---|---|
| — | Dashboard | `/Dashboard` | all signed-in |
| Care | Patients | `/Patients` | Admin, Doctor, Nutritionist, Reception |
| Care | Appointment Board | `/Reservations/Board` | Admin, Doctor, Nutritionist, Reception |
| Care | Reservations | `/Reservations/Index` | Admin, Doctor, Nutritionist, Reception |
| Care | Doctor Queues / My Queue | `/Reservations/Queue` | Admin, Reception, Doctor |
| Care | Follow-ups | `/FollowUps` | Admin, Reception, Doctor |
| Care | Analysis | `/Analysis` | Admin, Doctor |
| Care | Nutrition | `/Nutrition/Packages` | Admin, Nutritionist |
| Billing | Payment Dashboard / Unpaid Payments | `/Payments/Dashboard` | Admin, Reception, ReceptionAdmin |
| Billing | Payment Reports | `/Payments/Report` | Admin, Reception, ReceptionAdmin, Doctor |
| Administration | System Lookups ▸ All Lookups, Users, Roles, Analysis/Treatment/Injection/Vitamin Types, Patient Sources, Clinics | various | Admin |

Observations (detail in Phase 3):
- A `ReceptionAdmin`-only user sees no **Care** section (the Care block checks Admin/Doctor/Nutritionist/Reception) but does see Billing.
- Injection and Vitamin types have two management screens each: `Lookups/InjectionTypes|VitaminTypes` (Admin, modal-based, Arabic titles, bound to domain entities) and `Nutrition/InjectionTypes|VitaminTypes` + Create/Edit pages (Admin, DTO-based). Only the Lookups ones are in the menu.
- Screens without a menu entry (reached by links/buttons): Patient details/timeline, medical records, prescriptions, analysis request/details, enrollments, follow-up booking, payments collect/cancel/change/summary/audit, print documents, `Reservations/Today`.
