# Slice F5a — Schema integrity: shadow keys, protected history, content type

Roadmap item **F5**, first half (Group 2). Fixes **F20** (cascade deletes, shadow foreign keys) and the content-type length part of **F17**. The unique patient phone (F11 part, D8) follows as **F5b**.

| | |
|---|---|
| Branch | `chore/f5a-schema-integrity` (from an up-to-date `main`, after PR #16 is merged) |
| Behaviour change | **None in the screens.** In the database: 1. Three leftover duplicate key columns (`MedicalAnalyses.AnalysisTypeId1`, `WeeklyAdministeredItems.PackageItemId1`, `Prescriptions.PatientRecordId`) are removed. 2. Deleting a **user** who collected payments or owns enrollments, a **patient** or **package** with enrollments, a **payment** with adjustments, a **medical record** with prescriptions/treatments, or an **analysis type** used in follow-up lab results is now **refused by the database** instead of silently deleting that history. (No screen deletes any of these today — this protects against mistakes and future code.) 3. Attachment content types are `nvarchar(100)` in both attachment tables |
| Database | ⚠ One migration: drops 3 unused columns, changes 8 foreign keys from CASCADE to RESTRICT, resizes 2 columns. Step 6 checks the data first |
| Size | M |

## Why

- **Shadow keys:** three relationships are configured twice (once in `SafyaDbContext`, once in a configuration class, or without naming the collection), so EF created a second, hidden key column for each. They confuse the model and can silently hold wrong values.
- **Cascade deletes:** deleting a user would delete every payment they collected; deleting a patient would delete their nutrition enrollments. Financial and clinical history must never disappear as a side effect.

## Design

| Relationship | Before | After |
|---|---|---|
| `Payments.CollectedBy → Users` | CASCADE | RESTRICT |
| `PatientNutritionEnrollments.DoctorId → Users` | CASCADE | RESTRICT |
| `PatientNutritionEnrollments.PatientId → Patients` | CASCADE | RESTRICT |
| `PatientNutritionEnrollments.PackageId → NutritionPackages` | CASCADE | RESTRICT |
| `PaymentAdjustments.PaymentId → Payments` | CASCADE | RESTRICT |
| `Prescriptions.RecordId → PatientRecords` | CASCADE | RESTRICT |
| `Treatments.RecordId → PatientRecords` | CASCADE | RESTRICT |
| `WeeklyFollowUpLabResults.AnalysisTypeId → AnalysisTypes` | CASCADE | RESTRICT |

Kept as CASCADE (child data with no meaning alone): phones/addresses → patient, role links → user/role, prescription items/attachments → prescription, package items → package, clinic-source agreements, follow-up details → follow-up.

Out of scope: unique phone (F5b); the leftover `''` default on `Payments.Status` (harmless — the app always sets the status; cleaned up with the payments work).

## Affected files

| Action | Path |
|---|---|
| Edit | `SafyaClinic/SafyaClinic.Infrastructure/Data/SafyaDbContext.cs` (remove 2 duplicate blocks) |
| Edit | `Data/Configurations/MedicalAnalysisConfiguration.cs`, `WeeklyAdministeredItemConfiguration.cs`, `PrescriptionConfiguration.cs`, `PaymentConfiguration.cs`, `PatientNutritionEnrollmentConfiguration.cs`, `PaymentAdjustmentConfiguration.cs`, `TreatmentConfiguration.cs`, `AnalysisAttachmentConfiguration.cs`, `PrescriptionAttachmentConfiguration.cs` |
| Add | `Data/Configurations/WeeklyFollowUpLabResultConfiguration.cs` |
| Add | `Migrations/<timestamp>_SchemaIntegrity.cs` (+ Designer, snapshot) — generated |
| Add | `SafyaClinic/tests/SafyaClinic.Tests/Infrastructure/ModelIntegrityTests.cs` |
| Add | `Docs/implementation/F5a-schema-integrity.md` |

## Risks

| Risk | Mitigation |
|---|---|
| A shadow column holds a value that differs from the real key | Step 6a counts such rows on the database first; must be 0 |
| An existing content type is longer than 100 | Step 6a checks the longest value |
| Some code deletes one of these parents | Searched: none does. If it ever happens, the database refuses and the error page shows a reference (S4) |
| Hosted databases | Same checks before running the deployment script (S8) |

## Rollback

Revert the PR's merge commit and run `dotnet ef database update ReshapeAuditLogs` on dev (ask me first). The dropped shadow columns were unused, so nothing is lost.

---

## Steps

PowerShell, from `D:\MyDevWork\clinic-MS`.

### Step 0 — Branch

```powershell
git switch main
git pull
git switch -c chore/f5a-schema-integrity
```

### Step 1 — Remove the duplicate relationship blocks

In `SafyaClinic/SafyaClinic.Infrastructure/Data/SafyaDbContext.cs`, inside `OnModelCreating`, delete these two blocks (keep the `Patient` weight block above and the `ApplyConfigurationsFromAssembly` line below):

```csharp
            modelBuilder.Entity<MedicalAnalysis>()
                .HasOne(ma=> ma.Type)
                .WithMany(at=> at.Analyses)
                .HasForeignKey(ma=> ma.AnalysisTypeId);

            modelBuilder.Entity<WeeklyAdministeredItem>()
                .HasOne(wi => wi.PackageItem)
                .WithMany(pi => pi.AdministeredItems)
                .HasForeignKey(wi => wi.PackageItemId);
```

### Step 2 — Name the collections in the configuration classes (removes the shadow keys)

| File | Replace | With |
|---|---|---|
| `MedicalAnalysisConfiguration.cs` (the `AnalysisType → MedicalAnalysis` block) | `builder.HasOne(ma => ma.Type)` + next line `.WithMany()` | `builder.HasOne(ma => ma.Type)` + `.WithMany(t => t.Analyses)` |
| `WeeklyAdministeredItemConfiguration.cs` (the `PackageItemId → PackageItems` block) | `builder.HasOne(w => w.PackageItem)` + next line `.WithMany()` | `builder.HasOne(w => w.PackageItem)` + `.WithMany(p => p.AdministeredItems)` |

So they read:

```csharp
            builder.HasOne(ma => ma.Type)
                   .WithMany(t => t.Analyses)
                   .HasForeignKey(ma => ma.AnalysisTypeId)
                   .OnDelete(DeleteBehavior.Restrict);
```

```csharp
            builder.HasOne(w => w.PackageItem)
                   .WithMany(p => p.AdministeredItems)
                   .HasForeignKey(w => w.PackageItemId)
                   .OnDelete(DeleteBehavior.NoAction);
```

`PrescriptionConfiguration.cs` — replace

```csharp
        builder.HasOne(p => p.Record)
               .WithMany()
               .HasForeignKey(p => p.RecordId)
               .OnDelete(DeleteBehavior.Cascade);
```

with

```csharp
        // F5a: name the collection (removes the shadow PatientRecordId) and protect clinical history.
        builder.HasOne(p => p.Record)
               .WithMany(r => r.Prescriptions)
               .HasForeignKey(p => p.RecordId)
               .OnDelete(DeleteBehavior.Restrict);
```

### Step 3 — Protect financial and clinical history (RESTRICT)

**3a.** `PaymentConfiguration.cs` — after the `PatientSource` relationship block (the one ending `.OnDelete(DeleteBehavior.Restrict);` after `HasForeignKey(p => p.PatientSourceId)`), add:

```csharp

            // F5a: a user who collected payments cannot be deleted (deactivate instead).
            builder.HasOne(p => p.Collector)
                   .WithMany()
                   .HasForeignKey(p => p.CollectedBy)
                   .OnDelete(DeleteBehavior.Restrict);
```

**3b.** `PatientNutritionEnrollmentConfiguration.cs` — after `builder.HasIndex(e => e.Status);` add:

```csharp

            // F5a: enrollments are financial history — never deleted as a side effect.
            builder.HasOne(e => e.Patient)
                   .WithMany(p => p.NutritionEnrollments)
                   .HasForeignKey(e => e.PatientId)
                   .OnDelete(DeleteBehavior.Restrict);
            builder.HasOne(e => e.Package)
                   .WithMany(p => p.Enrollments)
                   .HasForeignKey(e => e.PackageId)
                   .OnDelete(DeleteBehavior.Restrict);
            builder.HasOne(e => e.Doctor)
                   .WithMany()
                   .HasForeignKey(e => e.DoctorId)
                   .OnDelete(DeleteBehavior.Restrict);
```

**3c.** `PaymentAdjustmentConfiguration.cs` and `TreatmentConfiguration.cs` — in the relationship block, change `.OnDelete(DeleteBehavior.Cascade);` to `.OnDelete(DeleteBehavior.Restrict);` (one place in each file).

**3d.** Create `SafyaClinic/SafyaClinic.Infrastructure/Data/Configurations/WeeklyFollowUpLabResultConfiguration.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SafyaClinic.Domain.Entities.Nutrition;

namespace SafyaClinic.Infrastructure.Data.Configurations
{
    public class WeeklyFollowUpLabResultConfiguration : IEntityTypeConfiguration<WeeklyFollowUpLabResult>
    {
        public void Configure(EntityTypeBuilder<WeeklyFollowUpLabResult> builder)
        {
            // F5a: an analysis type used in recorded results cannot be deleted.
            builder.HasOne(r => r.AnalysisType)
                   .WithMany()
                   .HasForeignKey(r => r.AnalysisTypeId)
                   .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
```

### Step 4 — Content type length

- `AnalysisAttachmentConfiguration.cs`: change `builder.Property(aa => aa.ContentType).HasMaxLength(50);` to `builder.Property(aa => aa.ContentType).HasMaxLength(100);`
- `PrescriptionAttachmentConfiguration.cs`: after the `FilePath` line add
  ```csharp
        builder.Property(pa => pa.ContentType).HasMaxLength(100);
  ```

### Step 5 — Tests

Create `SafyaClinic/tests/SafyaClinic.Tests/Infrastructure/ModelIntegrityTests.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using SafyaClinic.Domain.Entities.Analysis;
using SafyaClinic.Domain.Entities.MedicalRecord;
using SafyaClinic.Domain.Entities.Nutrition;
using SafyaClinic.Domain.Entities.Payment;
using SafyaClinic.Domain.Entities.Prescription;
using SafyaClinic.Infrastructure.Data;
using Xunit;

namespace SafyaClinic.Tests.Infrastructure;

/// <summary>F5a / F20: the SQL Server model has no shadow keys and protects history. No database is contacted.</summary>
public class ModelIntegrityTests
{
    private static readonly IModel Model = new SafyaDbContext(new DbContextOptionsBuilder<SafyaDbContext>()
        .UseSqlServer("Server=model-only;Database=ModelOnly;Integrated Security=True").Options).Model;

    [Theory]
    [InlineData(typeof(MedicalAnalysis), "AnalysisTypeId1")]
    [InlineData(typeof(WeeklyAdministeredItem), "PackageItemId1")]
    [InlineData(typeof(Prescription), "PatientRecordId")]
    public void Shadow_key_columns_are_gone(Type entity, string column)
    {
        Assert.Null(Model.FindEntityType(entity)!.FindProperty(column));
    }

    [Theory]
    [InlineData(typeof(Payment), "CollectedBy")]
    [InlineData(typeof(PatientNutritionEnrollment), "PatientId")]
    [InlineData(typeof(PatientNutritionEnrollment), "PackageId")]
    [InlineData(typeof(PatientNutritionEnrollment), "DoctorId")]
    [InlineData(typeof(PaymentAdjustment), "PaymentId")]
    [InlineData(typeof(Prescription), "RecordId")]
    [InlineData(typeof(Treatment), "RecordId")]
    [InlineData(typeof(WeeklyFollowUpLabResult), "AnalysisTypeId")]
    public void History_is_never_deleted_by_cascade(Type entity, string foreignKey)
    {
        var fk = Assert.Single(Model.FindEntityType(entity)!.GetForeignKeys(),
            k => k.Properties.Count == 1 && k.Properties[0].Name == foreignKey);
        Assert.Equal(DeleteBehavior.Restrict, fk.DeleteBehavior);
    }

    [Theory]
    [InlineData(typeof(AnalysisAttachment))]
    [InlineData(typeof(PrescriptionAttachment))]
    public void Content_type_holds_100_characters(Type entity)
    {
        Assert.Equal(100, Model.FindEntityType(entity)!.FindProperty("ContentType")!.GetMaxLength());
    }
}
```

**Check:**

```powershell
dotnet build SafyaClinic\SafyaClinic.sln
dotnet test SafyaClinic\SafyaClinic.sln
```

Expected **Passed: 137** (124 + 13), Failed: 0. If anything fails, send me the output — do not change the tests to make them pass.

### Step 6 — Migration (needs your approval)

⚠ **Do not run the app until 6c.**

**6a.** Data checks (read-only) — every number must be **0**, and the longest content type at most **100**:

```powershell
$cfg = Get-Content SafyaClinic\SafyaClinic.Web\appsettings.json -Raw | ConvertFrom-Json
$conn = New-Object System.Data.SqlClient.SqlConnection $cfg.ConnectionStrings.SafyaClinicDb
$conn.Open(); $cmd = $conn.CreateCommand()
$cmd.CommandText = @"
SELECT 'MedicalAnalyses shadow differs' AS [check], COUNT(*) AS [value] FROM MedicalAnalyses WHERE AnalysisTypeId1 IS NOT NULL AND AnalysisTypeId1 <> AnalysisTypeId
UNION ALL SELECT 'WeeklyAdministeredItems shadow differs', COUNT(*) FROM WeeklyAdministeredItems WHERE PackageItemId1 IS NOT NULL AND PackageItemId1 <> PackageItemId
UNION ALL SELECT 'Prescriptions shadow differs', COUNT(*) FROM Prescriptions WHERE PatientRecordId IS NOT NULL AND PatientRecordId <> RecordId
UNION ALL SELECT 'Longest AnalysisAttachments.ContentType', ISNULL(MAX(LEN(ContentType)), 0) FROM AnalysisAttachments
UNION ALL SELECT 'Longest PrescriptionAttachments.ContentType', ISNULL(MAX(LEN(ContentType)), 0) FROM PrescriptionAttachments
"@
$r = $cmd.ExecuteReader(); $t = New-Object System.Data.DataTable; $t.Load($r); $t | Format-Table -AutoSize; $conn.Close()
```

If a "shadow differs" count is not 0, or a content type is over 100 — stop and send me the output.

**6b.** Generate and check:

```powershell
Push-Location SafyaClinic\SafyaClinic.Web
dotnet ef migrations add SchemaIntegrity --project ..\SafyaClinic.Infrastructure
Pop-Location
```

`Up` in `…_SchemaIntegrity.cs` should contain only:

- for `MedicalAnalyses.AnalysisTypeId1`, `WeeklyAdministeredItems.PackageItemId1`, `Prescriptions.PatientRecordId`: `DropForeignKey`, `DropIndex`, `DropColumn`;
- `DropForeignKey` + `AddForeignKey` with `onDelete: ReferentialAction.Restrict` for the 8 relationships in the Design table;
- `AlterColumn` `ContentType` on `AnalysisAttachments` and `PrescriptionAttachments` (`maxLength: 100`).

Anything else (other tables, other columns, `DropTable`) → stop and send me the file.

**6c.** Back up and start the app:

```powershell
powershell -ExecutionPolicy Bypass -File .assessment\backup-db.ps1
dotnet run --project SafyaClinic\SafyaClinic.Web --launch-profile http
dotnet run --project SafyaClinic\tests\DeploymentChecks -- $env:TEMP\migrations-idempotent.sql
```

(Run the last command in a second window.) Doing 6c is your explicit approval for these changes on the **dev** database. DeploymentChecks must print `Model changes without a migration: False`.

### Step 7 — Manual check

Smoke test the screens that read these tables: a patient's medical records and a prescription (with an attachment), an analysis with an attachment, a nutrition enrollment and its follow-ups, the payments dashboard and a payment's audit page. All open as before.

### Step 8 — Commit, push, PR

```powershell
git add SafyaClinic/SafyaClinic.Infrastructure SafyaClinic/tests Docs
git status
git commit -m "F5a: remove shadow foreign keys; RESTRICT deletes on financial and clinical history; content type length 100"
git push -u origin chore/f5a-schema-integrity
```

Open the PR and send me the number and CI result. Do not merge before review.

## Hosted servers

Before running the deployment script on the test/demo databases, run the same Step 6a checks there (MonsterASP SQL tool). All "shadow differs" counts must be 0.

## Acceptance criteria

- [ ] The three shadow columns are gone; the model has no pending changes.
- [ ] The 8 listed relationships are RESTRICT (tests).
- [ ] Both `ContentType` columns are `nvarchar(100)`.
- [ ] Screens using these tables work as before.
- [ ] 137 tests pass locally and in CI.
