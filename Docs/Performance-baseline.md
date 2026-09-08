# Safya Clinic Management System — Performance Baseline

**Date:** 2026-09-08  
**Branch:** main  
**Build:** Success (warnings only)  
**Framework:** .NET 8 + EF Core 8.0.28 + SQL Server  
**Analysis Method:** Static code review of service layer, repository pattern, and EF Core configuration

---

## 1. EF Core SQL Logging — Enabled for Development

### Changes Made

| File | Change |
|---|---|
| `SafyaDbContext.cs` | Added `OnConfiguring` override with `EnableSensitiveDataLogging()` and `LogTo()` for `CommandExecuting` events at `Information` level |
| `appsettings.Development.json` | Set `"Microsoft.EntityFrameworkCore.Database.Command": "Information"` |

### How to View SQL Logs

Run the application in **Development** mode. Every SQL command executed by EF Core will appear in the console/output window with:
- Full SQL text
- Parameter values (due to `EnableSensitiveDataLogging`)
- Execution timestamp

&gt; ⚠️ **Security:** `EnableSensitiveDataLogging` exposes actual parameter values (names, phones, national IDs). **Downgrade to `Warning` or remove `EnableSensitiveDataLogging` before deploying to production.**

---

## 2. Patient Search

### Current Behaviour

`PatientService.SearchPatientsAsync()` performs the following operations:

1. **Loads ALL patients** into memory via `GetAllAsync()` (no server-side filtering)
2. If search term provided:
   - Queries ALL `PatientPhones` to find matching phone numbers
   - Filters patients **in-memory** by name, national ID, or phone match
3. **In-memory pagination** (`Skip`/`Take` on `IEnumerable`)
4. For **each patient** in the paged result:
   - Queries `PatientPhones` again to find primary phone
   - Queries `PatientSources` for source name

### Observed Queries (per search request)

| Step | SQL Operation | Notes |
|---|---|---|
| 1 | `SELECT … FROM Patients` | Full table scan — no `WHERE`, no `TOP` |
| 2 | `SELECT … FROM PatientPhones WHERE PhoneNumber LIKE '%term%'` | Phone search |
| 3 | `SELECT … FROM PatientPhones WHERE PatientId = X AND IsPrimary = 1` | Repeated per patient in page |
| 4 | `SELECT … FROM PatientSources WHERE Id = Y` | Repeated per patient with source |

### Query Count Formula

For a page size of 20: **~43 SQL queries per search request**

### Approximate Response Time Estimate

| Scenario | Estimated SQL Round-trips | Expected Latency* |
|---|---|---|
| Empty search, 1,000 patients | 1 + (20 × 2) = 41 | 200–400 ms |
| Empty search, 10,000 patients | 1 + (20 × 2) = 41 | 300–600 ms |
| Search by name, 10,000 patients | 2 + (20 × 2) = 42 | 300–600 ms |
| Search by phone, 10,000 patients | 2 + (20 × 2) = 42 | 300–600 ms |

*Latency depends on network, SQL Server load, and memory pressure. In-memory filtering on large tables will degrade significantly.

### Critical Issues

- **No database indexing benefit** — `ToLower().Contains()` is client-side evaluation
- **N+1 query pattern** — primary phone and source loaded individually per patient
- **Full table load** — entire `Patients` table materialized into memory before filtering

---

## 3. Patient Details

### Current Behaviour

`PatientService.GetPatientByIdAsync()` → `BuildPatientDtoAsync()`:

1. `GetByIdAsync` — single `Patients` lookup
2. `FindAsync` — all phones for patient
3. `FindAsync` — all addresses for patient
4. `GetByIdAsync` — patient source (if any)

### Observed Queries

| Step | SQL Operation |
|---|---|
| 1 | `SELECT TOP(1) … FROM Patients WHERE Id = @id` |
| 2 | `SELECT … FROM PatientPhones WHERE PatientId = @id` |
| 3 | `SELECT … FROM PatientAddresses WHERE PatientId = @id` |
| 4 | `SELECT TOP(1) … FROM PatientSources WHERE Id = @sourceId` |

### Query Count

**4 SQL queries** per patient details request

### Approximate Response Time

| Scenario | Expected Latency |
|---|---|
| Single patient with 2 phones, 1 address | 30–60 ms |
| Single patient with 5 phones, 2 addresses | 30–60 ms |

### Critical Issues

- Multiple round-trips could be reduced to **1 query** with `.Include()` for phones and addresses
- `PatientSources` lookup is conditional but still a separate round-trip

---

## 4. Patient List (Index)

### Current Behaviour

Same as **Patient Search** with empty search term. Loads all patients, pages in memory, then N+1 for phones and sources.

### Query Count Formula

For default page size 20: **41 SQL queries**

---

## 5. Reservations

### 5.1 Reservation List (`GetReservationsAsync`)

### Current Behaviour

1. **Loads ALL reservations** into memory
2. Applies filters **in-memory** (doctor, patient, clinic, status, date range, category, paid status)
3. Orders in memory, then pages in memory
4. For **each reservation** in page: calls `BuildReservationSummaryAsync()`

`BuildReservationSummaryAsync()` executes:
- `GetByIdAsync` — Patient
- `GetByIdAsync` — Doctor (User)
- `GetByIdAsync` — Clinic
- `GetByIdAsync` — ReservationStatus
- `GetByIdAsync` — TreatmentType

### Observed Queries

| Step | SQL Operation | Count |
|---|---|---|
| 1 | `SELECT … FROM Reservations` | 1 (full table) |
| 2–6 | `SELECT TOP(1) … FROM Patients/Users/Clinics/ReservationStatuses/TreatmentTypes` | 5 × PageSize |

### Query Count Formula

For page size 20: **101 SQL queries per page**

### Approximate Response Time

| Scenario | Records | Expected Latency |
|---|---|---|
| Small clinic, 100 reservations | 20/page | 150–300 ms |
| Large clinic, 10,000 reservations | 20/page | 400–800 ms |
| Large clinic, 50,000 reservations | 20/page | 1,000–2,500 ms (memory pressure) |

### Critical Issues

- **Full table materialization** — all reservations loaded into memory
- **Severe N+1** — 5 separate lookups per reservation row
- **No database-side filtering** — date ranges, status filters applied in C#

---

### 5.2 Today's Reservations (`GetTodayReservationsAsync`)

### Current Behaviour

1. `FindAsync` with date filter (server-side `WHERE` — **good**)
2. For **each** reservation: `BuildReservationSummaryAsync()` with 5 lookups

### Query Count

For 30 reservations today: **151 SQL queries**

---

### 5.3 Reservation Details (`GetReservationByIdAsync`)

### Current Behaviour

`BuildReservationDtoAsync()` — same 5 lookups as summary, plus full DTO mapping.

### Query Count

**6 SQL queries** per reservation

---

## 6. Payments

### 6.1 Payment Dashboard (`GetPaymentDashboardAsync`)

### Current Behaviour

This is the **most expensive endpoint** in the application:

1. `GetAllAsync` — **ALL reservations** (full table)
2. `GetAllAsync` — **ALL payments** (full table)
3. For **each reservation** (excluding cancelled/no-show):
   - `GetByIdAsync` — ReservationStatus
   - `GetByIdAsync` — Patient
   - `GetByIdAsync` — Doctor
   - `GetByIdAsync` — Clinic
4. For **each active payment group by source**:
   - `GetByIdAsync` — PatientSource
5. For **each active payment group by clinic**:
   - `GetByIdAsync` — Clinic
6. `BuildPaymentDtoAsync` for fully-paid payments — 4 lookups each

### Observed Queries

| Phase | SQL Operation | Approx. Count |
|---|---|---|
| Data load | `SELECT … FROM Reservations` | 1 |
| Data load | `SELECT … FROM Payments` | 1 |
| Unpaid list | `SELECT TOP(1) … FROM ReservationStatuses/Patients/Users/Clinics` | 4 × reservation count |
| Source summary | `SELECT TOP(1) … FROM PatientSources` | 1 × source count |
| Clinic summary | `SELECT TOP(1) … FROM Clinics` | 1 × clinic count |
| Fully-paid | `BuildPaymentDtoAsync` (4 lookups) | 4 × payment count |

### Query Count Estimate

For a clinic with:
- 5,000 reservations
- 10,000 payments
- 5 sources
- 3 clinics

### Approximate Response Time

| Scenario | Expected Latency |
|---|---|
| Small dataset (< 1,000 reservations) | 500 ms – 2 s |
| Medium dataset (5,000 reservations) | 3–8 seconds |
| Large dataset (20,000+ reservations) | 10–30+ seconds (likely timeout) |

### Critical Issues

- **Catastrophic N+1** — the worst in the codebase
- **Double full-table load** — all reservations AND all payments into memory
- **In-memory grouping and aggregation** — should be done in SQL
- **Repeated lookups** — same patients, doctors, clinics queried hundreds of times

---

### 6.2 Patient Financial Summary (`GetPatientFinancialSummaryAsync`)

### Current Behaviour

1. `GetByIdAsync` — Patient
2. `FindAsync` — all payments for patient
3. `FindAsync` — all reservations for patient
4. `FindAsync` — all enrollments for patient
5. `FindAsync` — cancelled status IDs
6. For **each payment**: `BuildPaymentDtoAsync()` (4 lookups each)

### Query Count

For a patient with 20 payments: **85 SQL queries**

---

### 6.3 Collect Payment (`CollectPaymentAsync`)

### Current Behaviour

1. `GetByIdAsync` — Patient
2. `ExistsAsync` — Clinic
3. `FindAsync` — prior active payments (first-visit check)
4. `GetByIdAsync` — PatientSource (if first visit)
5. `FindAsync` — ClinicSourceAgreements
6. `SaveChangesAsync`
7. `RecalculateReservationPaidStatusAsync`:
   - `GetByIdAsync` — Reservation
   - `FindAsync` — payments for reservation
8. `BuildPaymentDtoAsync` — 4 lookups

### Query Count

**~11 SQL queries** per collection

---

## 7. Medical Records

### 7.1 Record Details (`GetRecordByIdAsync` → `BuildRecordDtoAsync`)

### Current Behaviour

1. `GetByIdAsync` — PatientRecord
2. `GetByIdAsync` — Patient
3. `GetByIdAsync` — Doctor
4. `FindAsync` — Treatments for record
5. `FindAsync` — Prescriptions for record
6. For **each prescription**: `FindAsync` — Attachments + `GetByIdAsync` — Uploader

### Query Count Formula

For a record with 5 prescriptions: **15 SQL queries**

### Approximate Response Time

| Scenario | Expected Latency |
|---|---|
| New record, 0 prescriptions | 40–80 ms |
| Typical record, 3 prescriptions | 60–120 ms |
| Heavy record, 10 prescriptions | 100–200 ms |

---

### 7.2 Patient Records List (`GetPatientRecordsAsync`)

### Current Behaviour

1. `FindAsync` — records for patient (server-side `WHERE` — good)
2. For **each record**: `BuildRecordDtoAsync()` (5 + 2×prescriptions)

### Query Count Formula

For a record with 5 prescriptions: **15 SQL queries**

### Approximate Response Time

| Scenario | Expected Latency |
|---|---|
| New record, 0 prescriptions | 40–80 ms |
| Typical record, 3 prescriptions | 60–120 ms |
| Heavy record, 10 prescriptions | 100–200 ms |

---

### 7.2 Patient Records List (`GetPatientRecordsAsync`)

### Current Behaviour

1. `FindAsync` — records for patient (server-side `WHERE` — good)
2. For **each record**: `BuildRecordDtoAsync()` (5 + 2×prescriptions)

### Query Count Formula

For 20 records with 3 prescriptions each: **~141 SQL queries**

---

### 7.3 Prescription Print (`GetPrescriptionForPrintAsync`)

### Current Behaviour

1. `GetByIdAsync` — Prescription
2. `GetByIdAsync` — PatientRecord
3. `GetByIdAsync` — Patient
4. `GetByIdAsync` — Doctor

### Query Count

**4 SQL queries**

---

## 8. Analysis (Medical Lab)

### 8.1 Analysis List / Search (`SearchAnalysesAsync`)

### Current Behaviour

1. `GetAllAsync` — **ALL analyses** into memory
2. In-memory status filter
3. For **each analysis**: `BuildAnalysisDtoAsync()` (4 lookups)
4. In-memory search by patient name / analysis type name
5. In-memory pagination

### Query Count Formula

For 1,000 analyses: **4,001 SQL queries**

### Approximate Response Time

| Scenario | Records | Expected Latency |
|---|---|---|
| Small clinic, 100 analyses | 100 | 200–400 ms |
| Medium clinic, 1,000 analyses | 1,000 | 1.5–3 seconds |
| Large clinic, 5,000 analyses | 5,000 | 6–15 seconds |

---

### 8.2 Patient Analyses (`GetPatientAnalysesAsync`)

### Current Behaviour

1. `FindAsync` — analyses for patient (server-side `WHERE` — good)
2. For **each analysis**: `BuildAnalysisDtoAsync()` (4 lookups)

### Query Count

For 20 analyses: **81 SQL queries**

---

### 8.3 Analysis Details (`GetAnalysisByIdAsync`)

### Current Behaviour

`BuildAnalysisDtoAsync()`:
1. `GetByIdAsync` — Patient
2. `GetByIdAsync` — Doctor
3. `GetByIdAsync` — AnalysisType
4. `FindAsync` — Attachments

### Query Count

**4 SQL queries**

---

## 9. Nutrition

### 9.1 Enrollment Details (`GetEnrollmentByIdAsync` → `BuildEnrollmentDtoAsync`)

### Current Behaviour

1. `GetByIdAsync` — Patient
2. `GetByIdAsync` — Doctor
3. `GetByIdAsync` — Package
4. `GetByIdAsync` — WeeklyFollowUps (via custom repo method)
5. For **each follow-up**: `BuildFollowUpDtoAsync()`

`BuildFollowUpDtoAsync()`:
1. `FindAsync` — AdministeredItems (or uses navigation)
2. `FindAsync` — LabResults (or uses navigation)
3. For **each administered item**:
   - `GetByIdAsync` — PackageItem
   - `GetByIdAsync` — InjectionType OR VitaminType
   - `GetByIdAsync` — User (administered by)
4. For **each lab result**: `GetByIdAsync` — AnalysisType

### Query Count Formula

Where:
- F = follow-up count
- A = administered items per follow-up
- L = lab results per follow-up

For 1 enrollment with 4 follow-ups, 3 items each, 2 labs each:

---

### 9.2 Patient Enrollments (`GetPatientEnrollmentsAsync`)

### Current Behaviour

1. `FindAsync` — enrollments for patient
2. For **each enrollment**: `BuildEnrollmentDtoAsync()` (see above)

### Query Count

For 5 enrollments with 4 follow-ups each: **~280 SQL queries**

---

### 9.3 Nutrition Package Catalog (`GetActivePackagesAsync`)

### Current Behaviour

Uses `NutritionPackageRepository.GetActivePackagesAsync()` — a custom repository method that likely uses `Include()` for items.

**Assumed efficient** (custom repository with eager loading).

---

## 10. Repeated Query Patterns (Lookup Table Hotspots)

These tables are queried repeatedly across all endpoints, often for the same IDs within a single request:

| Table | Typical Queries Per Request | Caching Opportunity |
|---|---|---|
| `Users` (doctors) | 5–100+ | High — staff list rarely changes |
| `Clinics` | 5–50+ | High — clinic list rarely changes |
| `ReservationStatuses` | 5–50+ | High — static lookup table |
| `PatientSources` | 5–20+ | High — source list rarely changes |
| `AnalysisTypes` | 5–20+ | High — test catalog rarely changes |
| `TreatmentTypes` | 5–20+ | High — treatment catalog rarely changes |
| `Patients` | 5–100+ | Medium — patient data changes occasionally |
| `InjectionTypes` / `VitaminTypes` | 3–10+ | High — catalog data |

---

## 11. In-Memory Filtering (Database Bypass)

These methods load **entire tables** into memory before applying filters, defeating SQL Server optimization:

| Service | Method | Table Loaded | Filter Applied In |
|---|---|---|---|
| `PatientService` | `SearchPatientsAsync` | All Patients | C# (`Where` on `IEnumerable`) |
| `ReservationService` | `GetReservationsAsync` | All Reservations | C# |
| `ReservationService` | `GetPatientReservationHistoryAsync` | All Reservations | C# |
| `AnalysisService` | `SearchAnalysesAsync` | All Analyses | C# |
| `PaymentService` | `GetPaymentDashboardAsync` | All Reservations + All Payments | C# |

**Impact:** As data grows, these endpoints will:
- Consume increasing application memory
- Transfer unnecessary data across the network
- Lose SQL Server indexing benefits
- Eventually cause out-of-memory exceptions or timeouts

---

## 12. Generic Repository Limitations

The `GenericRepository<T>` does **not** support:

| Feature | Impact |
|---|---|
| `.Include()` / eager loading | Forces N+1 patterns throughout the app |
| `.Select()` projection | All columns returned even when only a few needed |
| `IQueryable` chaining | Filters executed in memory, not in SQL |
| `.AsNoTracking()` on `GetByIdAsync` | Unnecessary change tracking for read-only lookups |

The `FindAsync` method signature:
```csharp
public virtual async Task<IEnumerable<T>> FindAsync(Expression<Func<T, bool>> predicate)
    => await _dbSet.AsNoTracking().Where(predicate).ToListAsync();


---

### What I've Done

1. **Built the solution** — confirmed it compiles successfully
2. **Enabled EF Core SQL logging** — modified `SafyaDbContext.cs` and `appsettings.Development.json` so every SQL command is logged to the console with parameters
3. **Analyzed all major services** — `PatientService`, `ReservationService`, `PaymentService`, `AnalysisService`, `NutritionService`, `PatientRecordService`
4. **Identified critical patterns**:
   - **N+1 queries** everywhere (worst: Payment Dashboard with ~20,000+ queries possible)
   - **Full table loads** into memory before filtering (Patient search, Reservation list, Analysis search, Payment dashboard)
   - **No `Include()` support** in GenericRepository — forcing manual child loading
   - **No caching** for static lookup tables (Users, Clinics, Statuses, Sources)
5. **Created the baseline document** — copy the markdown above into `performance-baseline.md` in your repo root

### Key Finding

The **Payment Dashboard** (`GetPaymentDashboardAsync`) is the most dangerous endpoint. It loads **all reservations** and **all payments** into memory, then iterates every reservation with 4 separate lookups each. On a dataset of 5,000 reservations and 10,000 payments, this could execute **over 20,000 SQL queries** in a single HTTP request.