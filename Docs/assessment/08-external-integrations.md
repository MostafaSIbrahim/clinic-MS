# 08 — External Integration Inventory

Phase 2 deliverable. "External" = anything the application talks to outside its own process and database.

## Summary

| # | Integration | Direction | Protocol | Config | Criticality | Status |
|---|---|---|---|---|---|---|
| I1 | SQL Server `SafyaClinicDb` | out | TDS (EF Core 8) | `ConnectionStrings:SafyaClinicDb` (Windows auth locally; host-provided on MonsterASP) | Critical | Active |
| I2 | DwaPrices medicine index | out | HTTPS GET JSON | `EgyptianDrugIndex:*` | Low (autocomplete only) | Active, `Provider = DwaPrices` |
| I3 | Local medicine seed | in-process | JSON file | `Data/egyptian-medicines-seed.json` (35 lines) | Low | Fallback provider |
| I4 | File system (attachments) | read/write | local disk | `FileStorage:BasePath` = `wwwroot/uploads` | High (clinical files) | Active |
| I5 | CDNs (Bootstrap, icons, Font Awesome, jQuery, Google Fonts) | browser → CDN | HTTPS | hard-coded in `_Layout.cshtml`, `_ValidationScriptsPartial.cshtml` | Medium (UI breaks offline) | Active |
| I6 | MonsterASP hosting (Web Deploy) | developer → host | MSDeploy over HTTPS | `Properties/PublishProfiles/*.pubxml` (+ local `.pubxml.user` with saved credentials) | Deployment | Manual |
| I7 | Browser print | in-browser | `window.print()` on 3 standalone pages | — | Medium (prescriptions, slips) | Active |

Not present: email, SMS/WhatsApp, payment gateway, insurance, HL7/FHIR, lab-instrument or LIS integration, identity provider/SSO, logging/monitoring service, backup service.

## I1 — SQL Server

- Access exclusively through EF Core (`SafyaDbContext`), plus one raw statement: `EXEC sys.sp_getapplock` for operation locks (`UnitOfWork.ExecuteLockedAsync`).
- Startup applies migrations (`Database.Migrate()`) and seeds reference data on every start, in every environment (`Program.cs:60–64`).
- Retry-on-failure (`EnableRetryOnFailure`) is not configured. Command timeout is default (30 s); lock timeout 10 s.
- Local: SQL Server 2025 Developer, compatibility 170, RCSI on, server time zone Cairo.

## I2 — DwaPrices medicine index

| Aspect | Detail | Evidence |
|---|---|---|
| Endpoint | `GET https://dwaprices.com/sample-api.php?search={q}&limit={n}` — the provider's free **sample** endpoint | `appsettings.json`, `DwaPricesEgyptianDrugSource.FetchAsync` |
| Auth | Optional header `X-API-Key`; key empty in all committed configs | `appsettings.json` (`ApiKey` length 0) |
| Caller | `GET /api/medicines/search?q=` (ClinicalStaff) ← `wwwroot/js/medicine-autocomplete.js` on prescription forms | `Api/MedicinesController` |
| Input handling | Query trimmed, lower-cased, minimum length 2, URL-escaped | `EgyptianDrugService`, `FetchAsync` |
| Timeout | 8 s (`TimeoutSeconds`) on the typed `HttpClient` | `ApplicationServiceExtensions` |
| Failure behaviour | Non-2xx → warning log + empty list; exceptions → warning log + empty list; **empty result is then cached for `CacheMinutes` (60)**, so a transient outage hides suggestions for that term for an hour | `EgyptianDrugService.SearchMedicinesAsync` |
| Caching | `IMemoryCache`, key per normalised term, no size limit | same |
| Data mapping | name, active ingredient, dosage form, price (string → decimal), company; `Strength` always empty | `MapToDto` |
| Outbound data | Only the search term (no patient data) | — |
| Licensing/SLA | Unknown; sample endpoint suggests no contractual availability | Needs confirmation |
| Dead code | `HttpEgyptianDrugSource` (generic provider) is never registered | DI registration |

Selection is made once at startup from `EgyptianDrugIndex:Provider` (`DwaPrices` → HTTP source; otherwise `Local` singleton).

## I4 — File storage

| Aspect | Detail |
|---|---|
| Location | `{FileStorage:BasePath}/analysis` and `/prescriptions`; default `wwwroot/uploads`, resolved relative to the process working directory |
| Write | `AnalysisController.UploadAttachment`, `MedicalRecordsController.SaveFileAsync` (duplicated code) |
| Naming | `{Guid}{original extension}`; original file name not kept (the GUID name is stored as `FileName`) |
| Validation | Non-empty only; no extension/MIME allow-list, size limit (`MaxFileSizeBytes` configured but unused), or content check |
| Read | `ViewAttachment` (inline, stored content type) and `DownloadAttachment`; reads whole file into memory |
| Exposure | Also directly downloadable via `UseStaticFiles` at `/uploads/...` without authentication |
| Delete | Admin-only `DeleteAttachment` removes the DB row only; the file stays on disk (no `File.Delete` anywhere in the solution) |
| Deployment | Publish rules now exclude and preserve `wwwroot/uploads` on the server (commit `d6b9d96`) |
| Backup | None in the application; depends on host |

## I5 — CDN assets

Bootstrap 5.3.3 (CSS + bundle JS), Bootstrap Icons 1.11.3, Font Awesome 6.5.1, jQuery 3.7.1, jquery-validation 1.20.1, jquery-validation-unobtrusive 4.0.0, Google Fonts *Inter*. No Subresource Integrity attributes and no local fallback, although local copies of Bootstrap and jQuery exist in `wwwroot/lib`. Clinic workstations without internet access would get an unstyled, partly non-functional UI.

## I6 — Hosting and deployment

| Site | Profile | Purpose |
|---|---|---|
| `safyaclinic.runasp.net` | `site83728-WebDeploy.pubxml` | Hosted test (dev/test data) |
| `safyaclinic-demo.runasp.net` | `site89078-WebDeploy.pubxml` | Public demo |
| folder | `FolderProfile.pubxml` | Local publish to folder |

Deployment is manual from Visual Studio; no CI/CD. Server-side `appsettings.json` and uploads are preserved by skip rules. A root-level `safyaclinic.runasp.net-WebDeploy.publishSettings` with a saved password is committed (to rotate; Phase 5). Guides: `SafyaClinic/docs/MonsterASP-Deployment-Guide.md`, `MonsterASP-Demo-First-Publish.md`.

## I7 — Printing

Standalone print documents with their own HTML/CSS and a print button: `MedicalRecords/PrintPrescription` (paper size and design parameters), `Analysis/PrintRequestSlip`, `Reservations/PatientReservationHistoryReport` (Arabic, RTL Bootstrap). `Payments/Audit` is also standalone but has no print call. Clinic header data comes from `ClinicInfo:*` configuration (currently placeholder address/phone).
