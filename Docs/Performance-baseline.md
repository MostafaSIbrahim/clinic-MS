# Performance Baseline Report: Clinic Management System (Clinic-MS)

**Document Version:** 2.0.0-draft  
**Last Updated:** September 2026  
**Status:** In Progress / Pre-Optimization Audit  
**Target Repository:** `https://github.com/MostafaSIbrahim/clinic-MS.git`  
**Purpose:** Establish an evidence-based performance baseline separating static code/architectural analysis from verified runtime measurements.

---

## 1. Executive Summary

This document serves as the ground-truth technical audit for the Clinic-MS application prior to architectural and database performance optimization. 

Rather than relying on speculative load simulations, this baseline establishes:
1. A **Static Architectural & Query Execution Analysis** highlighting structural bottlenecks across core modules.
2. An **Evidence-Based Runtime Baseline Tracker** recording strictly verified test telemetry. Unmeasured endpoints and unverified loads are explicitly marked as **`Not measured yet`** to prevent optimization regressions against fictitious figures.

---

## 2. Application Environment

*Note: Infrastructure specifications below reflect the target test/staging deployment parameters. Actual hardware load profiling must be recorded against these targets.*

* **Runtime Framework:** .NET 8.0 (C#)
* **ORM:** Entity Framework Core 8.x
* **Database Management System:** Microsoft SQL Server 2022
* **Application Architecture:** ASP.NET Core MVC with Generic Repository and Unit of Work patterns
* **Execution & Test Host:** Local Development / Staging Test Environment
  * *OS:* Windows Server / Linux Container
  * *Database Engine:* SQL Server Developer Edition / LocalDB (development baseline)

---

## 3. Static Performance Analysis (Code & Query Review)

Static code inspection reveals systemic architectural constraints stemming from abstraction layers (e.g., generic repositories lacking server-side query expressions or IQueryable passthroughs), leading to in-memory processing.

### 3.1 Patients Module
```
Route: /Patients (Search & Listing)
Data Flow:
Repository.GetAllAsync()
  └── SELECT [All Columns] FROM [Patients] (Full Table Scan)
      └── Loaded into memory as List<Patient>
          └── LINQ in-memory filtering: .Where(p => p.Name.Contains(query) || p.Phone == query)
              └── In-memory sorting & pagination: .Skip((page-1)*size).Take(size)
                  └── N+1 Lookups: Iterating medical history records or reservation counts per row
```
* **Architectural Risk:** Memory footprint scales linearly with total patient records. Server-side SQL filtering and paging (`OFFSET / FETCH`) are entirely bypassed.

---

### 3.2 Reservations Module
```
Route: /Reservations/Index & /Reservations/Create
Data Flow:
ReservationRepository.GetAllAsync()
  └── Fetches all Reservation records into client memory
      └── In-memory status evaluation: reservation.Status == ReservationStatusEnum.Confirmed
          └── Secondary joins executed sequentially:
              ├── Doctor lookup: DoctorRepository.GetByIdAsync(doctor_id)
              └── Patient lookup: PatientRepository.GetByIdAsync(patient_id)
```
* **Architectural Risk:** High read/write contention during peak scheduling hours. The repeated round-trips (`N+1`) compound network I/O and query execution duration per reservation row displayed.

---

### 3.3 Payments Module
```
Route: /Payments/Dashboard
Data Flow:
PaymentRepository.GetAllAsync() + ReservationRepository.GetAllAsync()
  └── Eager or non-projected load of all payment transactions
      └── Client-side calculation: Summing totals, computing outstanding balances
          └── Repeated nested lookups:
              └── Patient and Reservation entities re-queried to assemble ViewModels
```
* **Architectural Risk:** Heavy heap allocations on financial dashboards. Lack of SQL aggregate functions (`SUM()`, `COUNT()`, `GROUP BY`) forces full transaction table scans over the wire.

---

### 3.4 Medical Records Module
```
Route: /MedicalRecords/Details/{patientId}
Data Flow:
PatientRepository.GetByIdAsync(patientId)
  └── Fetch Patient root entity
      └── Explicit or lazy loading of related collections:
          ├── Prescriptions
          ├── Diagnoses
          ├── Attachments / Files metadata
```
* **Architectural Risk:** Loading unprojected entity graphs pulls large text fields and historical audit metadata into EF Core ChangeTracker even when only read-only summaries are needed.

---

### 3.5 Analysis (Lab Tests) Module
```
Route: /Analysis/Search & /Analysis/Index
Data Flow:
AnalysisRepository.GetAllAsync()
  └── Unfiltered retrieval of all diagnostic test catalogs and historical patient test orders
      └── In-memory string comparison on test names, categories, and patient identifiers
```
* **Architectural Risk:** Missing full-text or prefix-indexed database queries causes CPU spikes on the application server under repeated lookups.

---

### 3.6 Nutrition Module
```
Route: /Nutrition/Plans
Data Flow:
NutritionPlanRepository.GetAllAsync()
  └── Loads complete dietary plans, associated meal templates, and item lists
      └── In-memory filtering by patient dietary preferences or macro constraints
```
* **Architectural Risk:** Deep relational nesting causes multiple subqueries or Cartesian products if `Include()` chains are applied without projection.

---

## 4. Known Performance Problems (Anti-Patterns Identified)

* **Full Table Loads:** Generic repository implementation defaults to `GetAllAsync()` without `IQueryable` predicate propagation, executing unconstrained `SELECT *` statements.
* **N+1 Query Iteration:** Foreign key entities (Patients, Doctors, Treatments) are retrieved inside application loops via individual `GetByIdAsync()` calls rather than relational `JOIN` or EF Core eager projections (`.Include()`).
* **In-Memory Filtering:** Predicate expressions (`.Where(...)`) execute on client memory after materialization via `.ToList()` / `.ToListAsync()`.
* **In-Memory Pagination:** `.Skip()` and `.Take()` execute post-materialization, requiring SQL Server to transmit thousands of unneeded rows to the application tier.
* **Missing Projections (`.Select()`):** Domain models are pulled with full column graphs rather than lightweight, typed DTOs, bloating memory and generating tracking overhead.
* **Lack of `AsNoTracking()`:** Read-only queries attach entities to EF Core's ChangeTracker, substantially increasing heap allocations and GC pressure.

---

## 5. Actual Runtime Measurements

> **Policy Notice:** This section strictly contains verified execution results. Hypothetical load tests, unverified staging figures, and estimated response times are explicitly excluded.

### 5.1 Verified Endpoint Telemetry

| Endpoint / Scenario | Dataset Size | Concurrency (VUs) | Requests Run | Average | p50 | p95 | p99 | SQL Queries / Req | Status |
| :--- | :--- | :---: | :---: | :---: | :---: | :---: | :---: | :---: | :--- |
| `/Patients` (Search: "Ahmed") | 1,000 records | 10 | 100 | *Not measured yet* | *Not measured yet* | *Not measured yet* | *Not measured yet* | *Not measured yet* | Pending Run |
| `/Reservations` (Index View) | 2,500 records | 10 | 100 | *Not measured yet* | *Not measured yet* | *Not measured yet* | *Not measured yet* | *Not measured yet* | Pending Run |
| `/Payments/Dashboard` | 5,000 records | 5 | 50 | *Not measured yet* | *Not measured yet* | *Not measured yet* | *Not measured yet* | *Not measured yet* | Pending Run |
| `/MedicalRecords/Details` | 500 records | 10 | 100 | *Not measured yet* | *Not measured yet* | *Not measured yet* | *Not measured yet* | *Not measured yet* | Pending Run |
| `/Analysis` (Search) | 1,000 records | 10 | 100 | *Not measured yet* | *Not measured yet* | *Not measured yet* | *Not measured yet* | *Not measured yet* | Pending Run |
| `/Nutrition` (Plans List) | 300 records | 5 | 50 | *Not measured yet* | *Not measured yet* | *Not measured yet* | *Not measured yet* | *Not measured yet* | Pending Run |

---

## 6. Database Baseline

* **Actual SQL Timings:** *Not measured yet.*
* **Active Indexes:** Primary keys and default foreign key indexes generated by EF Core migrations. *Full index inventory and fragmentation analysis pending.*
* **Execution Plans Captured:** *Not captured yet.* (Will capture SQL Server Execution Plans for top offending `SELECT` queries once warm seed dataset is applied).
* **Connection Pool Behavior:** *Not measured yet.*

---

## 7. Memory / CPU Baseline

* **Application Working Set (Idle):** *Not measured yet.*
* **Application Working Set (Under Load):** *Not measured yet.*
* **GC Pause Time (Gen 0/1/2):** *Not measured yet.*
* **SQL Server Memory & Buffer Pool:** *Not measured yet.*
* **CPU Utilization (% Core):** *Not measured yet.*

---

## 8. Baseline Limitations (What Has Not Been Measured)

1. **No Live Concurrency Testing:** No multi-user stress testing (e.g., k6, Locust, or JMeter) has been executed against an active database instance.
2. **Missing Seed Data Scaling:** Profiling has not yet been executed on enterprise dataset sizes (e.g., 50,000+ patients, 200,000+ reservations).
3. **No Profiler Tracing:** SQL Server Profiler / Extended Events traces for EF Core query generation have not yet been logged.
4. **Network & Web Server Overhead:** Telemetry has not yet factored in TLS termination, reverse proxy buffering, or production network latency.

---

## 9. Optimization Targets

Target metrics establish the minimum acceptable criteria post-refactoring:

| Area | Current Architecture Constraint | Target Solution | Target Metric (Estimated Goal) |
| :--- | :--- | :--- | :--- |
| **Patient Search** | `GetAllAsync()` + memory filter | Push `LIKE / CONTAINS` to SQL + `AsNoTracking()` + Index | p95 < 50 ms @ 10,000 rows |
| **Reservation Scheduling** | Multiple round-trip entity lookups | Single query projection with `.Include()` / DTO select | p95 < 60 ms @ peak |
| **Payment Dashboard** | Client-side aggregation of all records | SQL `SUM()`, `GROUP BY`, and indexed date-window queries | p95 < 70 ms |
| **Medical Records** | Unprojected heavy entity graphs | Read-only summary DTO projection (`AsNoTracking()`) | p95 < 40 ms |
| **Analysis & Nutrition** | Full collection pulls | Server-side pagination (`Skip/Take`) at the database level | p95 < 35 ms |

---

## 10. Definition of Done (Performance Validation Criteria)

Before any optimization PR is merged, the following criteria must be satisfied:

1. **Reproducible Load Script:** An automated test script (k6 or equivalent) with explicit dataset seed scripts committed to repository source control.
2. **Query Verification:** Verified reduction in executed SQL statements per HTTP request (eliminating N+1 patterns).
3. **Paging Pushdown:** Database execution plan confirms execution of SQL `OFFSET / FETCH` (no client-side pagination).
4. **Telemetry Sign-Off:** Actual latency measurements (Average, p50, p95, p99) populated in Section 5 with verified test logs attached.
