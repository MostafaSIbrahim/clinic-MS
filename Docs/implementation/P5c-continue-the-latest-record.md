# Slice P5c: Start consultation opens the latest record

A follow-up to **P5**, from your request of 9 Nov. It follows your answers:
- **a record can cover several visits**;
- Start consultation offers **the latest record of the same kind**, from any doctor;
- a **locked** latest record is **shown with only "Create a new record"**.

**Claude implemented this slice at the owner's request.** The owner runs one command, approves the database change, does the manual check, and pushes.

| | |
|---|---|
| Branch | `feature/p5c-continue-record` (from the working copy, after PR #50 is merged) |
| Behaviour change | **1. Start consultation / Open consultation**, for a visit that has no record yet, now behaves as follows. **If the patient has an earlier record of the same kind** (internal medicine or nutrition, from any doctor), it opens that latest record with a blue panel, "This is the patient's latest record", and two buttons: **Continue in this record** and **Create a new record for this visit**. **If that record is locked**, the panel says so and offers only the new record. **With no earlier record**, the new-record form opens, as before. **2. Continue in this record** documents today's visit in that record. Notes, treatments, prescriptions and lab requests go into it. **Complete visit** appears there and completes today's visit. The link is audited as `MedicalRecord.VisitLinked`. **3. The Notes tab lists "Visits in this record"**, with date, time, doctor and status, each linking to the reservation. **4. Complete visit** now works from any record the visit is documented in, including one opened by another doctor. Before, the record's doctor had to be the visit's doctor. It still requires: the visit's doctor (or acting on their behalf), in consultation, and the same patient |
| Unchanged | One visit is documented in one record. Records can still be locked, and a locked record can't be continued or edited. A visit that already has a record opens it directly. Creating a record from a visit works as before, and also records the link |
| Database | **One migration**, `VisitsDocumentedInRecords`. It adds `Reservations.MedicalRecordId` (nullable, with an index and a foreign key to `PatientRecords`, no cascade). It also has **one data step**: every existing record's visit is copied into the new column (`UPDATE … FROM Reservations r JOIN PatientRecords pr ON pr.ReservationId = r.Id`), so past visits stay linked. The script adds that step to the generated migration and shows it. Backup and **YES** approval as usual |
| Tests | +6 (about **427**). They cover: the latest record of the same kind, from any doctor, ignoring other kinds and patients; a locked latest record is still the one offered; continuing links the visit, is audited, makes it the visit in consultation and lists it under the record's visits; a locked record, another kind or another patient's record can't be continued; only the visit's doctor or an admin, and only in consultation; a visit is linked once, and a new record links its visit |
| Not tested automatically | **Complete visit** runs as a direct database update, which the in-memory test database can't execute. Manual check step 6 covers it |

## Files

| Action | Path |
|---|---|
| Edit | `Domain/Entities/Reservation/Reservation.cs` (`MedicalRecordId`, `MedicalRecord`), `Domain/Entities/MedicalRecord/PatientRecord.cs` (`Visits`), `Infrastructure/Data/Configurations/PatientRecordConfiguration.cs` |
| Generated | `Infrastructure/Migrations/*_VisitsDocumentedInRecords.cs` (+ Designer and snapshot), by the script |
| Edit | `Application/Services/PatientRecordService.cs` (`GetLatestRecordAsync`, `ContinueRecordAsync`; a new record links its visit; record visits), `Interfaces/Services/IPatientRecordService.cs`, `DTOs/MedicalRecord/MedicalRecordDtos.cs`, `Common/Auditing/Audit.cs` |
| Edit | `Application/Services/ReservationService.cs` (the context uses the link; `GetVisitInConsultationAsync`; Complete visit by the link), `Interfaces/Services/IReservationService.cs` |
| Edit | `Web/Controllers/MedicalRecordsController.cs` (`Consultation` offers the latest record; `ContinueRecord`; `Details` finds the visit in consultation), `Views/MedicalRecords/Details.cshtml`, `Resources/SharedResource.ar.resx` |
| Add | `tests/.../Application/ContinueRecordTests.cs` |
| Edit | `tests/.../Web/Security/access-matrix.txt` (+1 line) |
| Local only | `.assessment/slice.ps1` gained `-UpSql`, which adds a data step to a generated migration. It's already on your machine |

The access matrix gains `MedicalRecords.ContinueRecord POST: Admin Doctor` (needs *Write medical records*). It's already added to `access-matrix.txt`.

## Rollback

Revert the PR's merge commit, and roll the database back to the previous migration (its `Down` drops the column; links to continued records are lost) or restore the script's backup.

---

## Your steps

After merging **PR #50**, from `D:\MyDevWork\clinic-MS`:

```powershell
.\.assessment\slice.ps1 -Branch feature/p5c-continue-record -Migration VisitsDocumentedInRecords `
  -Expect 'AddColumn MedicalRecordId','CreateIndex IX_Reservations_MedicalRecordId','AddForeignKey FK_Reservations_PatientRecords_MedicalRecordId' `
  -UpSql 'UPDATE r SET r.MedicalRecordId = pr.Id FROM Reservations r INNER JOIN PatientRecords pr ON pr.ReservationId = r.Id' `
  -Require 'r.MedicalRecordId = pr.Id','ReferentialAction.Restrict' `
  -Message 'P5c: Start consultation opens the latest record of the same kind; continue in it or create a new one; a record covers several visits'
```

The script generates the migration, adds the data step ("Added the data step to Up: …"), checks it, and asks for **YES** before backing up and applying.

### Manual check (while the app runs)

Use a **test patient** who already has an **internal medicine** record from an earlier visit.

1. **Reception:** book that patient with Dr X today, confirm, and check in. **Dr X → Start consultation.**
2. The patient's **latest record** opens, with the blue panel and **Continue in this record** and **Create a new record for this visit**.
3. **Continue in this record.** The green "Today's visit is documented in this record" message appears, and the panel goes. **Complete visit** is in the visit bar. **Notes → Visits in this record** lists the earlier visit and today's.
4. **Add a treatment** and **a prescription** (P5a allergy check still applies). They're in the same record.
5. **Queue → Open Consultation** for this visit opens the same record directly.
6. **Complete visit.** It completes today's visit, and Reception is sent to Collect if a balance remains, as before.
7. **New record path:** another patient with an earlier record → Start consultation → **Create a new record for this visit**. The new-record form opens. Save it, and the new record shows **Complete visit**.
8. **Locked:** lock a patient's latest record (Admin → More actions → Lock record), then start their next consultation. The panel says it's locked and offers only **Create a new record for this visit**.
9. **No earlier record:** a brand-new patient → Start consultation opens the new-record form directly.
10. **Other doctor's record:** if the latest record was written by Dr Y, Dr X can still continue in it (any doctor may edit an unlocked record) and complete the visit.
11. **Arabic:** the panel reads هذا أحدث سجل للمريض, and the buttons read المتابعة في هذا السجل and إنشاء سجل جديد لهذه الزيارة.
12. **Audit (optional, read-only):** `SELECT TOP 5 Action, EntityId, Details FROM AuditLogs WHERE Action = 'MedicalRecord.VisitLinked' ORDER BY Id DESC;`

When you've finished, press Enter in the script, then type `YES` to commit and push. Open the PR and send me its number and the CI result.
