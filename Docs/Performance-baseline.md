# Performance Baseline Report: Clinic Management System (Clinic-MS)

**Document Version:** 1.0.0  
**Test Date:** September 2026  
**Environment:** Staging / Pre-Production (`staging-clinic-app-01`)  
**Target Stack:** ASP.NET Core MVC / Web API, EF Core, Microsoft SQL Server  
**Test Tools:** k6 v0.51.0, Prometheus, SQL Server Dynamic Management Views (DMVs)  

---

## 1. Executive Summary

This document establishes the initial performance baseline for the **Clinic-MS** (Clinic Management System) application. Benchmarks evaluate core operational flows, including patient registration, appointment/reservation scheduling, doctor availability lookups, and clinical visit treatment tracking under realistic concurrent clinic operations.

### Key Benchmark Metrics
* **Target Throughput:** 1,200 RPS across clinical workflows at nominal concurrency (500 Virtual Users).
* **Aggregate p95 Response Time:** **38.4 ms** across common administrative and medical workflows (SLA target: < 80 ms).
* **Reservation Booking Latency:** **p95 of 58.2 ms** under high concurrency reservation slots allocation.
* **Error Rate:** **0.00%** HTTP 5xx failures under standard peak loads.

---

## 2. Test Topology & Host Sizing

| Tier | Component | Sizing & Allocation | Key Notes |
| :--- | :--- | :--- | :--- |
| **Application Tier** | ASP.NET Core Application Server | 4 vCPU, 8 GB RAM | Kestrel reverse-proxied behind NGINX |
| **Database Tier** | Microsoft SQL Server Standard 2022 | 8 vCPU, 32 GB RAM, SSD Storage | `MAXDOP=4`, connection pooling enabled (max pool: 128) |
| **Session & Cache** | In-Memory / Distributed Memory Cache | 2 GB Memory Allocation | Doctor slot caching & lookup tables |

---

## 3. Workload Profiles & Test Scenarios

```
Scenarios:
1. Smoke Check:       25 VUs  | 5 mins  | Verify route sanity and database connection pool warming
2. Daily Clinic Peak: 500 VUs | 30 mins | Emulates regular operating clinic reception and doctor entry
3. Slot Rush Stress:  1,800 VUs | 15 mins | Heavy concurrency on doctor calendar slots & reservation bookings
4. Endurance / Soak:  350 VUs | 4 hours | Detects EF Core DbContext memory bloat & connection pool leaks
```

---

## 4. Benchmark Latency & Throughput Telemetry (500 VUs Steady-State)

| Module / Endpoint | Method | Throughput (RPS) | Latency Avg | Latency p50 | Latency p95 | Latency p99 | Success Rate |
| :--- | :---: | :---: | :---: | :---: | :---: | :---: | :---: |
| `/Account/Login` | POST | 45.2 | 24.1 ms | 19.5 ms | 41.2 ms | 68.0 ms | 100.00% |
| `/Patients` (Listing & Search) | GET | 210.4 | 14.2 ms | 11.0 ms | 24.8 ms | 43.1 ms | 100.00% |
| `/Patients/Create` | POST | 38.6 | 32.4 ms | 26.1 ms | 51.0 ms | 82.3 ms | 99.99% |
| `/Doctors/Availability` | GET | 345.1 | 8.6 ms | 6.2 ms | 15.4 ms | 28.9 ms | 100.00% |
| `/Reservations/Create` | POST | 118.0 | 36.8 ms | 29.4 ms | 58.2 ms | 94.6 ms | 99.98% |
| `/Reservations/Details/{id}` | GET | 265.8 | 10.1 ms | 7.9 ms | 18.2 ms | 31.5 ms | 100.00% |
| `/Treatments/Assign` | POST | 72.3 | 41.5 ms | 34.0 ms | 64.7 ms | 108.2 ms | 100.00% |
| **System Aggregated** | **ALL** | **1,095.4** | **18.7 ms** | **13.5 ms** | **38.4 ms** | **65.2 ms** | **99.99%** |

---

## 5. Database Performance Baselines (SQL Server)

* **Average Query Duration:** 4.1 ms across indexed lookups.
* **Top Query Wait Types:** `PAGEIOLATCH_SH` (4.8%), `ASYNC_NETWORK_IO` (2.1%), `SOS_SCHEDULER_YIELD` (1.2%).
* **Buffer Pool Hit Ratio:** **99.6%** during steady-state peak.
* **Connection Pool Acquisition Time:** Avg **0.6 ms** (p99: **3.8 ms**).
* **Deadlock Count:** **0** occurrences during 30-minute peak test.

---

## 6. Stress Test & Knee-Point Analysis

Ramping concurrency up to 2,000 Virtual Users identified the system degradation boundaries:

```
VUs     RPS       p50 (ms)   p95 (ms)   p99 (ms)   HTTP 5xx   Status
--------------------------------------------------------------------------------
250      620       9.8        22.4       42.0       0.00%      Nominal
500    1,095      13.5        38.4       65.2       0.00%      Baseline Standard
1,000  1,680      24.2        68.0      124.5       0.00%      Healthy
1,400  2,050      48.1       132.6      248.0       0.02%      Warning (Thread pool ramp)
1,750  2,210     112.4       340.0      710.2       0.45%      Degraded (SQL Pool Queuing)
2,000  2,150     285.0       980.5    2,140.0       3.12%      Knee Point (Connection Exhaustion)
```

### Saturation Bottleneck
At **1,750+ VUs**, the ADO.NET connection pool reaches max capacity (`Max Pool Size = 128`), introducing connection wait latency on write-heavy reservation transactions.

---

## 7. Soak Test Stability (4 Hours @ 350 VUs)

* **GC Heap Behavior:** .NET Server GC cycles operated stably with Gen 2 collections occurring once every 22 minutes on average.
* **Memory Footprint:** Application working set remained bounded between **610 MB** and **740 MB** without upward drift.
* **Unclosed Connections:** Zero orphaned SQL connections verified via `sys.dm_exec_connections`.

---

## 8. Continuous Integration Quality Gates

```yaml
performance_gates:
  aggregate_p95_latency:
    target: "<= 50ms"
    failure_threshold: "> 80ms"
  reservation_booking_p95:
    target: "<= 60ms"
    failure_threshold: "> 100ms"
  error_rate_5xx:
    target: "0.00%"
    failure_threshold: "> 0.10%"
  sql_buffer_cache_hit_ratio:
    target: ">= 99.0%"
    failure_threshold: "< 95.0%"
```
