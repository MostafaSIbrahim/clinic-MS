# Slice F3b-2 — Audit trail: medical records and payments

Roadmap item **F3**, second half, part 2 (Group 2). Completes the audit part of **F23** / R26. Builds on F3b-1 (table and writer).

| | |
|---|---|
| Branch | `chore/f3b2-audit-records-payments` (from an up-to-date `main`, after PR #15 is merged) |
| Behaviour change | **No visible change.** New audit rows for: **medical record edited** (with the *names* of the fields that changed — not the clinical text), **medical record locked**, **payment collected** (amount, method, reservation/enrollment), **payment cancelled** (amount), **payment amount changed** (old → new). Write-off is added when write-off payments exist (pilot, D5) |
| Database | None (the table exists since F3b-1) |
| Size | S |

## Design

| Decision | Choice |
|---|---|
| Clinical privacy | A record edit logs `{"changed":["Diagnosis","Notes"]}` — which fields, never their content. The record itself is the clinical source of truth |
| Payment details | Amounts, method, linked reservation/enrollment. The cancellation/change *reason* stays in `PaymentAdjustments` (already shown on the payment audit page), not duplicated |
| Who | Payment methods already receive the acting user; record methods take it from the request (`IAuditContext`) |
| Same save | Payments: the audit row is saved in the same `SaveChangesAsync` as the payment (collection also runs inside its existing database lock). Records: edit and lock use a direct SQL update, so the audit row is saved right after a successful update |

## Affected files

| Action | Path |
|---|---|
| Edit | `SafyaClinic/SafyaClinic.Application/Common/Auditing/Audit.cs` (5 action names) |
| Add | `SafyaClinic/SafyaClinic.Application/Services/MedicalRecordChanges.cs` |
| Edit | `SafyaClinic/SafyaClinic.Application/Services/PatientRecordService.cs` |
| Edit | `SafyaClinic/SafyaClinic.Application/Services/PaymentService.cs` |
| Add | `SafyaClinic/tests/SafyaClinic.Tests/Application/AuditRecordsAndPaymentsTests.cs` |
| Add | `Docs/implementation/F3b2-audit-trail-records-and-payments.md` |

## Rollback

Revert the PR's merge commit. No data changes.

---

## Steps

PowerShell, from `D:\MyDevWork\clinic-MS`.

### Step 0 — Branch

```powershell
git switch main
git pull
git switch -c chore/f3b2-audit-records-payments
```

### Step 1 — Action names

In `SafyaClinic/SafyaClinic.Application/Common/Auditing/Audit.cs`, inside `AuditActions`, after the `RoleRemoved` line add:

```csharp
    public const string RecordEdited = "MedicalRecord.Edited";
    public const string RecordLocked = "MedicalRecord.Locked";
    public const string PaymentCollected = "Payment.Collected";
    public const string PaymentCancelled = "Payment.Cancelled";
    public const string PaymentAmountChanged = "Payment.AmountChanged";
```

### Step 2 — Which fields an edit changes

Create `SafyaClinic/SafyaClinic.Application/Services/MedicalRecordChanges.cs`:

```csharp
using SafyaClinic.Application.DTOs.MedicalRecord;

namespace SafyaClinic.Application.Services;

/// <summary>
/// Names of the fields a medical-record edit changes (F3b-2). The audit trail records these
/// names instead of the clinical text itself. Text is compared trimmed, as the edit stores it.
/// </summary>
public static class MedicalRecordChanges
{
    public static IReadOnlyList<string> ChangedFields(UpdatePatientRecordRequest current, UpdatePatientRecordRequest update)
    {
        var changed = new List<string>();

        void Text(string name, string? before, string? after)
        {
            if ((before?.Trim() ?? string.Empty) != (after?.Trim() ?? string.Empty))
                changed.Add(name);
        }

        Text(nameof(update.ChiefComplaint), current.ChiefComplaint, update.ChiefComplaint);
        Text(nameof(update.PresentIllnessHistory), current.PresentIllnessHistory, update.PresentIllnessHistory);
        Text(nameof(update.Diagnosis), current.Diagnosis, update.Diagnosis);
        Text(nameof(update.DifferentialDiagnosis), current.DifferentialDiagnosis, update.DifferentialDiagnosis);
        Text(nameof(update.TreatmentPlan), current.TreatmentPlan, update.TreatmentPlan);
        Text(nameof(update.Notes), current.Notes, update.Notes);
        if (current.FollowUpDate != update.FollowUpDate)
            changed.Add(nameof(update.FollowUpDate));

        return changed;
    }
}
```

### Step 3 — `PatientRecordService`

**3a.** Add `using SafyaClinic.Application.Common.Auditing;` and replace

```csharp
    public PatientRecordService(IUnitOfWork uow) => _uow = uow;
```

with

```csharp
    private readonly IAuditTrail _audit;

    public PatientRecordService(IUnitOfWork uow, IAuditTrail? audit = null)
    {
        _uow = uow;
        _audit = audit ?? NoAuditTrail.Instance;
    }
```

**3b.** In `UpdateRecordAsync`, replace the first query

```csharp
        var recordState = await _uow.PatientRecords
            .Query()
            .Where(r => r.Id == recordId)
            .Select(r => new
            {
                r.IsLocked
            })
            .FirstOrDefaultAsync();
```

with

```csharp
        var recordState = await _uow.PatientRecords
            .Query()
            .Where(r => r.Id == recordId)
            .Select(r => new
            {
                r.IsLocked,
                Current = new UpdatePatientRecordRequest
                {
                    ChiefComplaint = r.ChiefComplaint,
                    PresentIllnessHistory = r.PresentIllnessHistory,
                    Diagnosis = r.Diagnosis,
                    DifferentialDiagnosis = r.DifferentialDiagnosis,
                    TreatmentPlan = r.TreatmentPlan,
                    Notes = r.Notes,
                    FollowUpDate = r.FollowUpDate
                }
            })
            .FirstOrDefaultAsync();
```

and replace the end of the method

```csharp
        if (affectedRecord == 0)
            return ServiceResult.Failure("Record not found or locked.");

        return ServiceResult.Success("Record updated.");
```

with

```csharp
        if (affectedRecord == 0)
            return ServiceResult.Failure("Record not found or locked.");

        await _audit.RecordAsync(AuditActions.RecordEdited, "PatientRecord", recordId,
            new { changed = MedicalRecordChanges.ChangedFields(recordState.Current, request) });
        await _uow.SaveChangesAsync();

        return ServiceResult.Success("Record updated.");
```

**3c.** In `LockRecordAsync`, replace

```csharp
        return affectedRows == 0
            ? ServiceResult.Failure("Record not found.")
            : ServiceResult.Success("Record locked.");
```

with

```csharp
        if (affectedRows == 0)
            return ServiceResult.Failure("Record not found.");

        await _audit.RecordAsync(AuditActions.RecordLocked, "PatientRecord", recordId);
        await _uow.SaveChangesAsync();
        return ServiceResult.Success("Record locked.");
```

### Step 4 — `PaymentService`

**4a.** Add `using SafyaClinic.Application.Common.Auditing;` and replace

```csharp
    public PaymentService(IUnitOfWork uow) => _uow = uow;
```

with

```csharp
    private readonly IAuditTrail _audit;

    public PaymentService(IUnitOfWork uow, IAuditTrail? audit = null)
    {
        _uow = uow;
        _audit = audit ?? NoAuditTrail.Instance;
    }
```

**4b.** Collected — in the collection code, replace

```csharp
        await _uow.SaveChangesAsync();

        // Auto-mark reservation as paid if linked and payments cover total
```

with

```csharp
        await _uow.SaveChangesAsync();

        // F3b-2: saved by the final SaveChangesAsync below, inside the same payment lock.
        await _audit.RecordAsync(AuditActions.PaymentCollected, "Payment", payment.Id, new
        {
            amount = payment.Amount,
            method = payment.PaymentMethod.ToString(),
            reservationId = payment.ReservationId,
            enrollmentId = payment.EnrollmentId
        }, currentUserId);

        // Auto-mark reservation as paid if linked and payments cover total
```

**4c.** Cancelled — in `CancelPaymentAsync`, replace

```csharp
        // NOTE: Cancelling a payment writes the amount off — it is removed from "collected
```

with

```csharp
        await _audit.RecordAsync(AuditActions.PaymentCancelled, "Payment", payment.Id,
            new { amount = oldAmount }, currentUserId);

        // NOTE: Cancelling a payment writes the amount off — it is removed from "collected
```

**4d.** Amount changed — in `ChangePaymentAmountAsync`, replace

```csharp
        // Adjust enrollment total paid by the delta
```

with

```csharp
        await _audit.RecordAsync(AuditActions.PaymentAmountChanged, "Payment", payment.Id,
            new { oldAmount, newAmount = request.NewAmount }, currentUserId);

        // Adjust enrollment total paid by the delta
```

### Step 5 — Tests

Create `SafyaClinic/tests/SafyaClinic.Tests/Application/AuditRecordsAndPaymentsTests.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using SafyaClinic.Application.Common.Auditing;
using SafyaClinic.Application.DTOs.MedicalRecord;
using SafyaClinic.Application.DTOs.Payment;
using SafyaClinic.Application.Services;
using SafyaClinic.Domain.Entities.Patient;
using SafyaClinic.Domain.Entities.Payment;
using SafyaClinic.Domain.Enums;
using SafyaClinic.Infrastructure.Data;
using SafyaClinic.Infrastructure.Repositories;
using Xunit;

namespace SafyaClinic.Tests.Application;

/// <summary>F3b-2: which record fields changed; payment cancel/change are audited.</summary>
public class AuditRecordsAndPaymentsTests
{
    private sealed class FixedContext : IAuditContext
    {
        public int? UserId => null;
        public string? IpAddress => "10.0.0.2";
        public string? RequestId => "REQ-2";
    }

    // ── Which fields changed (pure) ─────────────────────────

    [Fact]
    public void Unchanged_edit_reports_no_fields()
    {
        var current = new UpdatePatientRecordRequest { Diagnosis = "Flu", Notes = "Rest" };

        Assert.Empty(MedicalRecordChanges.ChangedFields(current, new UpdatePatientRecordRequest { Diagnosis = "Flu", Notes = "Rest" }));
    }

    [Fact]
    public void Changed_fields_are_named_and_whitespace_is_ignored()
    {
        var current = new UpdatePatientRecordRequest { Diagnosis = "Flu", Notes = "Rest", FollowUpDate = new DateTime(2026, 12, 1) };
        var update = new UpdatePatientRecordRequest { Diagnosis = "Influenza", Notes = "  Rest ", FollowUpDate = new DateTime(2026, 12, 8) };

        Assert.Equal(new[] { "Diagnosis", "FollowUpDate" }, MedicalRecordChanges.ChangedFields(current, update));
    }

    // ── Payments (in memory) ────────────────────────────────

    private static async Task<(SafyaDbContext Context, int AdminId, int PaymentId)> SeedPaymentAsync()
    {
        var context = new SafyaDbContext(new DbContextOptionsBuilder<SafyaDbContext>()
            .UseInMemoryDatabase($"audit-pay-{Guid.NewGuid()}").Options);
        await DbSeeder.SeedAsync(context, new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { [DbSeeder.AdminPasswordSettingKey] = "Audit-Pay-Password-1" })
            .Build());

        var adminId = await context.Users.Where(u => u.PhoneNumber == "0100").Select(u => u.Id).SingleAsync();
        var patient = new Patient { FirstName = "Test", LastName = "Patient" };
        context.Patients.Add(patient);
        await context.SaveChangesAsync();
        var payment = new Payment
        {
            PatientId = patient.Id, CollectedBy = adminId, Amount = 500m, OriginalAmount = 500m,
            ClinicNetAmount = 500m, Status = PaymentStatusEnum.Active
        };
        context.Payments.Add(payment);
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();
        return (context, adminId, payment.Id);
    }

    private static PaymentService Payments(SafyaDbContext context)
    {
        context.ChangeTracker.Clear();
        var uow = new UnitOfWork(context);
        return new PaymentService(uow, new AuditTrail(uow, new FixedContext()));
    }

    [Fact]
    public async Task Cancelled_payment_is_audited()
    {
        var (context, adminId, paymentId) = await SeedPaymentAsync();

        await Payments(context).CancelPaymentAsync(new CancelPaymentRequest { PaymentId = paymentId, Reason = "Duplicate" }, adminId);

        var entry = Assert.Single(await context.AuditLogs.AsNoTracking().ToListAsync());
        Assert.Equal(AuditActions.PaymentCancelled, entry.Action);
        Assert.Equal(paymentId, entry.EntityId);
        Assert.Equal(adminId, entry.UserId);
        Assert.Contains("500", entry.Details);
    }

    [Fact]
    public async Task Changed_amount_is_audited_with_old_and_new_amounts()
    {
        var (context, adminId, paymentId) = await SeedPaymentAsync();

        await Payments(context).ChangePaymentAmountAsync(
            new ChangePaymentAmountRequest { PaymentId = paymentId, NewAmount = 450m, Reason = "Discount" }, adminId);

        var entry = Assert.Single(await context.AuditLogs.AsNoTracking().ToListAsync());
        Assert.Equal(AuditActions.PaymentAmountChanged, entry.Action);
        Assert.Contains("500", entry.Details);
        Assert.Contains("450", entry.Details);
    }
}
```

**Check:**

```powershell
dotnet build SafyaClinic\SafyaClinic.sln
dotnet test SafyaClinic\SafyaClinic.sln
```

Expected **Passed: 124** (120 + 4), Failed: 0. If anything fails, send me the output — do not change the tests to make them pass. (Record edit/lock and payment collection use SQL-Server-only features, so they are checked manually in Step 6.)

### Step 6 — Manual check

`dotnet run --project SafyaClinic\SafyaClinic.Web --launch-profile http`, then as a doctor/Admin:

1. Edit an unlocked medical record: change the diagnosis only → save. Lock it.
2. As Admin/Reception: collect a small payment on a test reservation, change its amount, cancel it.
3. Read the trail (read-only):

   ```powershell
   $cfg = Get-Content SafyaClinic\SafyaClinic.Web\appsettings.json -Raw | ConvertFrom-Json
   $conn = New-Object System.Data.SqlClient.SqlConnection $cfg.ConnectionStrings.SafyaClinicDb
   $conn.Open(); $cmd = $conn.CreateCommand()
   $cmd.CommandText = "SELECT TOP 10 Id, OccurredAtUtc, UserId, Action, EntityType, EntityId, Details FROM AuditLogs ORDER BY Id DESC"
   $r = $cmd.ExecuteReader(); $t = New-Object System.Data.DataTable; $t.Load($r); $t | Format-Table -AutoSize; $conn.Close()
   ```

   Expect `MedicalRecord.Edited` with `{"changed":["Diagnosis"]}` (no diagnosis text), `MedicalRecord.Locked`, `Payment.Collected`, `Payment.AmountChanged`, `Payment.Cancelled`, each with your user id.

### Step 7 — Commit, push, PR

```powershell
git add SafyaClinic/SafyaClinic.Application SafyaClinic/tests Docs
git status
git commit -m "F3b-2: audit medical record edits (changed field names) and locks; payment collect, cancel, amount change"
git push -u origin chore/f3b2-audit-records-payments
```

Open the PR and send me the number and CI result. Do not merge before review.

## Acceptance criteria

- [ ] Record edit writes `MedicalRecord.Edited` with the changed field names only; lock writes `MedicalRecord.Locked`.
- [ ] Payment collect / cancel / amount change each write one row with amounts and the acting user.
- [ ] No clinical text or free-text reasons in `Details`.
- [ ] 124 tests pass locally and in CI.
