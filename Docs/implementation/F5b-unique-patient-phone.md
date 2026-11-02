# Slice F5b — One patient per primary phone number

Roadmap item **F5**, second half (Group 2). Implements decision **D8** ("no duplicate patients — the phone is unique") and the phone part of **F11**.

| | |
|---|---|
| Branch | `chore/f5b-unique-phone` (from an up-to-date `main`, after PR #17 is merged) |
| Behaviour change | 1. **Registering a patient needs a phone number**, and its **primary** phone may not be another patient's primary phone — the message names the existing patient so Reception opens that file instead. 2. Phones are **stored in one form**: digits only, Egypt's `+20`/`0020` becomes `0`, Arabic-Indic digits become 0–9 (`+20 100 123 4567` → `01001234567`), so the same number is recognised however it is typed; patient search by phone finds it in any format too. 3. Every patient has **exactly one primary** phone: if none is ticked, the first one is primary; adding a phone marked *Primary* moves the flag. 4. The **primary phone cannot be removed** (make another phone primary first). 5. **Secondary phones may be shared** (e.g. a parent's number on several children) |
| Database | ⚠ One migration: normalises existing phone numbers, fixes patients with no / several primary phones, adds a **unique index on primary phone numbers**. Step 1 checks your data first — duplicates must be resolved before the migration can run |
| Size | M |

## Design

| Decision | Choice |
|---|---|
| What is unique | The **primary** phone number across patients (filtered unique index `WHERE IsPrimary = 1`). Other phones are free, because families share numbers |
| Normal form | `PhoneNumbers.Normalize`: keep digits (Arabic-Indic → Western); `0020…` → `0…`; `20…` with ≥ 11 digits → `0…`. At least 7 digits |
| Existing data | The migration applies the same rules in SQL, then: patient with no primary → their oldest phone becomes primary; several primaries → keep the oldest |
| Duplicates already in the data | Reported in Step 1 (read-only). Dev data is test data (D8), so we fix them directly — **with your approval of the exact statements** I send back |
| Message | "This phone number is already the primary phone of *Name* (patient #12). Open that patient instead of registering a new one." — staff-only screen |

Out of scope: merging duplicate patients (not needed for test data); a phone-first registration screen (pilot P2).

## Affected files

| Action | Path |
|---|---|
| Add | `SafyaClinic/SafyaClinic.Application/Common/PhoneNumbers.cs` |
| Edit | `SafyaClinic/SafyaClinic.Application/Services/PatientService.cs` |
| Edit | `SafyaClinic/SafyaClinic.Infrastructure/Data/Configurations/PatientPhoneConfiguration.cs` |
| Add | `Migrations/<timestamp>_UniquePrimaryPhone.cs` (+ Designer, snapshot) — generated, plus SQL you add |
| Edit | `SafyaClinic/SafyaClinic.Web/Views/Patients/Create.cshtml` (phone required) |
| Add | `SafyaClinic/tests/SafyaClinic.Tests/Application/PatientPhoneTests.cs` |
| Add | `Docs/implementation/F5b-unique-patient-phone.md` |

## Risks

| Risk | Mitigation |
|---|---|
| Duplicates in existing data make the migration fail | Step 1 report first; the migration runs in a transaction, so a failure leaves the database unchanged |
| A real family shares one number as *primary* | Register the number as primary for one patient and as a secondary phone for the others; give the others their own primary number when known |
| Formats the rules do not cover | Stored exactly as digits; the report shows anything unusual before migrating |

## Rollback

Revert the PR's merge commit; on dev, `dotnet ef database update SchemaIntegrity` drops the index (ask me first). Normalised phone numbers stay normalised (they are still valid numbers).

---

## Steps

PowerShell, from `D:\MyDevWork\clinic-MS`.

### Step 1 — Duplicate report (read-only) — send me the output

```powershell
$cfg = Get-Content SafyaClinic\SafyaClinic.Web\appsettings.json -Raw | ConvertFrom-Json
$conn = New-Object System.Data.SqlClient.SqlConnection $cfg.ConnectionStrings.SafyaClinicDb
$conn.Open(); $cmd = $conn.CreateCommand()
$cmd.CommandText = @"
WITH n AS (
  SELECT pp.Id, pp.PatientId, pp.IsPrimary, pp.PhoneNumber,
         REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(LTRIM(RTRIM(pp.PhoneNumber)),' ',''),'-',''),'(',''),')',''),'.',''),'+','') AS N
  FROM PatientPhones pp),
m AS (
  SELECT Id, PatientId, IsPrimary, PhoneNumber,
         CASE WHEN N LIKE '0020%' THEN '0' + SUBSTRING(N, 5, 50)
              WHEN N LIKE '20%' AND LEN(N) >= 11 THEN '0' + SUBSTRING(N, 3, 50)
              ELSE N END AS Normalized
  FROM n)
SELECT m.Normalized, m.PatientId, p.FirstName + ' ' + p.LastName AS Patient, m.IsPrimary, m.PhoneNumber AS Stored, m.Id AS PhoneId
FROM m JOIN Patients p ON p.Id = m.PatientId
WHERE m.Normalized IN (SELECT Normalized FROM m GROUP BY Normalized HAVING COUNT(DISTINCT PatientId) > 1)
   OR LEN(m.Normalized) < 7
ORDER BY m.Normalized, m.PatientId;
"@
$r = $cmd.ExecuteReader(); $t = New-Object System.Data.DataTable; $t.Load($r); $t | Format-Table -AutoSize
$cmd.CommandText = "SELECT COUNT(*) FROM Patients p WHERE NOT EXISTS (SELECT 1 FROM PatientPhones pp WHERE pp.PatientId = p.Id)"
"Patients without any phone: " + $cmd.ExecuteScalar(); $conn.Close()
```

- **No rows** → nothing to clean; continue with Step 2.
- **Rows listed** → send me the output. I will reply with exact clean-up statements for your approval (test data only). Do not migrate until that is done.

(Patients without a phone are fine; they simply have no primary phone. The rule applies when phones are added or new patients are registered.)

### Step 2 — Branch

```powershell
git switch main
git pull
git switch -c chore/f5b-unique-phone
```

### Step 3 — The normal form

Create `SafyaClinic/SafyaClinic.Application/Common/PhoneNumbers.cs`:

```csharp
using System.Text;

namespace SafyaClinic.Application.Common;

/// <summary>
/// One stored form for phone numbers, so the same number always matches (F5b, D8):
/// digits only (Arabic-Indic digits become 0–9) and Egypt's international prefix becomes
/// the local 0. "+20 100 123 4567", "0020-100-1234567" and "٠١٠٠١٢٣٤٥٦٧" all become "01001234567".
/// </summary>
public static class PhoneNumbers
{
    public const int MinimumDigits = 7;

    public static string Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;

        var digits = new StringBuilder(value.Length);
        foreach (var c in value)
        {
            if (c is >= '0' and <= '9') digits.Append(c);
            else if (c is >= '\u0660' and <= '\u0669') digits.Append((char)('0' + (c - '\u0660')));   // Arabic-Indic
            else if (c is >= '\u06F0' and <= '\u06F9') digits.Append((char)('0' + (c - '\u06F0')));   // Extended Arabic-Indic
        }

        var number = digits.ToString();
        if (number.StartsWith("0020")) return "0" + number[4..];
        if (number.StartsWith("20") && number.Length >= 11) return "0" + number[2..];
        return number;
    }
}
```

### Step 4 — `PatientService`

File: `SafyaClinic/SafyaClinic.Application/Services/PatientService.cs`. Add `using SafyaClinic.Application.Common;`.

**4a.** Helper — directly after the constructor line `public PatientService(IUnitOfWork uow) => _uow = uow;` add:

```csharp

    // F5b: the primary phone identifies the patient (D8).
    private async Task<string?> PrimaryPhoneConflictAsync(string phoneNumber, int? exceptPatientId)
    {
        var owner = await _uow.PatientPhones.Query()
            .Where(ph => ph.IsPrimary && ph.PhoneNumber == phoneNumber
                         && (exceptPatientId == null || ph.PatientId != exceptPatientId))
            .Select(ph => new { ph.PatientId, ph.Patient.FirstName, ph.Patient.LastName })
            .FirstOrDefaultAsync();

        return owner is null
            ? null
            : $"This phone number is already the primary phone of {owner.FirstName} {owner.LastName} " +
              $"(patient #{owner.PatientId}). Open that patient instead of registering a new one.";
    }
```

**4b.** `CreatePatientAsync` — directly **before** the line `if (!string.IsNullOrWhiteSpace(request.Gender) &&` add:

```csharp
        // F5b: a phone is required; numbers are normalised; the primary one must be free.
        var phoneEntries = request.Phones.Where(ph => !string.IsNullOrWhiteSpace(ph.PhoneNumber)).ToList();
        if (phoneEntries.Count == 0)
            return ServiceResult<PatientDto>.Failure("Enter the patient's phone number.");

        var phoneNumbers = phoneEntries.Select(ph => PhoneNumbers.Normalize(ph.PhoneNumber)).ToList();
        if (phoneNumbers.Any(n => n.Length < PhoneNumbers.MinimumDigits))
            return ServiceResult<PatientDto>.Failure($"Enter a valid phone number (at least {PhoneNumbers.MinimumDigits} digits).");

        var primaryIndex = Math.Max(0, phoneEntries.FindIndex(ph => ph.IsPrimary));
        var phoneConflict = await PrimaryPhoneConflictAsync(phoneNumbers[primaryIndex], exceptPatientId: null);
        if (phoneConflict is not null)
            return ServiceResult<PatientDto>.Failure(phoneConflict);

```

and replace the phones block

```csharp
        var phones = request.Phones
            .Select(ph => new PatientPhone
            {
                PatientId = patient.Id,
                PhoneNumber = ph.PhoneNumber.Trim(),
                PhoneType = ph.PhoneType,
                IsPrimary = ph.IsPrimary
            })
            .ToList();
```

with

```csharp
        var phones = phoneEntries
            .Select((ph, i) => new PatientPhone
            {
                PatientId = patient.Id,
                PhoneNumber = phoneNumbers[i],
                PhoneType = ph.PhoneType,
                IsPrimary = i == primaryIndex   // exactly one primary
            })
            .ToList();
```

**4c.** Replace the whole `AddPhoneAsync` method with:

```csharp
    public async Task<ServiceResult> AddPhoneAsync(
        int patientId, CreatePatientPhoneRequest request)
    {
        if (!await _uow.Patients.ExistsAsync(patientId))
            return ServiceResult.Failure("Patient not found.");

        var number = PhoneNumbers.Normalize(request.PhoneNumber);
        if (number.Length < PhoneNumbers.MinimumDigits)
            return ServiceResult.Failure($"Enter a valid phone number (at least {PhoneNumbers.MinimumDigits} digits).");

        var currentPrimaries = await _uow.PatientPhones.Query(asNoTracking: false)
            .Where(ph => ph.PatientId == patientId && ph.IsPrimary)
            .ToListAsync();
        var makePrimary = request.IsPrimary || currentPrimaries.Count == 0;

        if (makePrimary)
        {
            var conflict = await PrimaryPhoneConflictAsync(number, patientId);
            if (conflict is not null)
                return ServiceResult.Failure(conflict);

            // Clear the old primary first, so the unique index never sees two at once.
            foreach (var phone in currentPrimaries) phone.IsPrimary = false;
            await _uow.SaveChangesAsync();
        }

        await _uow.PatientPhones.AddAsync(new PatientPhone
        {
            PatientId = patientId,
            PhoneNumber = number,
            PhoneType = request.PhoneType,
            IsPrimary = makePrimary
        });
        await _uow.SaveChangesAsync();
        return ServiceResult.Success(makePrimary ? "Phone added as the primary phone." : "Phone added.");
    }
```

**4d.** In `RemovePhoneAsync`, directly after the opening `{` add:

```csharp
        var isPrimary = await _uow.PatientPhones.Query()
            .Where(phone => phone.Id == phoneId && phone.PatientId == patientId)
            .Select(phone => (bool?)phone.IsPrimary)
            .FirstOrDefaultAsync();
        if (isPrimary == true)
            return ServiceResult.Failure("The primary phone cannot be removed. Add another phone as primary first.");

```

**4e.** Search — in `SearchPatientsAsync`, replace

```csharp
               p.Phones.Any(ph => ph.PhoneNumber.Contains(search)));
```

with

```csharp
               p.Phones.Any(ph => ph.PhoneNumber.Contains(search)) ||
               (phoneSearch.Length >= 3 && p.Phones.Any(ph => ph.PhoneNumber.Contains(phoneSearch))));
```

and directly above `if (!string.IsNullOrWhiteSpace(search))` (just after `var search = request.Search?.Trim();`) add:

```csharp
        var phoneSearch = PhoneNumbers.Normalize(search);   // "+20 100…" finds "0100…"
```

### Step 5 — Unique index (Infrastructure)

In `PatientPhoneConfiguration.cs`, after `builder.Property(pp => pp.PhoneType).HasMaxLength(20);` add:

```csharp

            // F5b / D8: a primary phone number belongs to one patient only.
            builder.HasIndex(pp => pp.PhoneNumber)
                   .IsUnique()
                   .HasFilter("[IsPrimary] = 1")
                   .HasDatabaseName("UX_PatientPhones_PrimaryPhoneNumber");
```

### Step 6 — Registration form: phone required

`Views/Patients/Create.cshtml` — replace

```cshtml
                    <label for="Phones_0_PhoneNumber" class="form-label">Phone Number</label>
                    <input type="text" id="Phones_0_PhoneNumber" name="Phones[0].PhoneNumber" class="form-control" placeholder="e.g. 01xxxxxxxxx" />
```

with

```cshtml
                    <label for="Phones_0_PhoneNumber" class="form-label">Phone Number *</label>
                    <input type="tel" id="Phones_0_PhoneNumber" name="Phones[0].PhoneNumber" class="form-control"
                           placeholder="e.g. 01xxxxxxxxx" required inputmode="tel" autocomplete="tel" />
```

### Step 7 — Tests

Create `SafyaClinic/tests/SafyaClinic.Tests/Application/PatientPhoneTests.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using SafyaClinic.Application.Common;
using SafyaClinic.Application.DTOs.Patient;
using SafyaClinic.Application.Services;
using SafyaClinic.Domain.Entities.Patient;
using SafyaClinic.Infrastructure.Data;
using SafyaClinic.Infrastructure.Repositories;
using Xunit;

namespace SafyaClinic.Tests.Application;

/// <summary>F5b / D8: one normal form for phones; a primary phone belongs to one patient.</summary>
public class PatientPhoneTests
{
    [Theory]
    [InlineData("+20 100 123 4567", "01001234567")]
    [InlineData("0020-100-1234567", "01001234567")]
    [InlineData("٠١٠٠١٢٣٤٥٦٧", "01001234567")]
    [InlineData("(010) 0123-4567", "01001234567")]
    [InlineData("01001234567", "01001234567")]
    [InlineData("  ", "")]
    public void Phone_numbers_have_one_normal_form(string typed, string stored)
    {
        Assert.Equal(stored, PhoneNumbers.Normalize(typed));
    }

    [Fact]
    public void Primary_phone_numbers_are_unique_in_the_database()
    {
        var model = new SafyaDbContext(new DbContextOptionsBuilder<SafyaDbContext>()
            .UseSqlServer("Server=model-only;Database=ModelOnly;Integrated Security=True").Options).Model;

        var index = Assert.Single(model.FindEntityType(typeof(PatientPhone))!.GetIndexes(),
            i => i.Properties.Count == 1 && i.Properties[0].Name == nameof(PatientPhone.PhoneNumber));
        Assert.True(index.IsUnique);
        Assert.Equal("[IsPrimary] = 1", index.GetFilter());
    }

    private static async Task<SafyaDbContext> SeededAsync()
    {
        var context = new SafyaDbContext(new DbContextOptionsBuilder<SafyaDbContext>()
            .UseInMemoryDatabase($"phones-{Guid.NewGuid()}").Options);
        await DbSeeder.SeedAsync(context, new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { [DbSeeder.AdminPasswordSettingKey] = "Phone-Test-Password-1" })
            .Build());
        context.ChangeTracker.Clear();
        return context;
    }

    private static PatientService Patients(SafyaDbContext context)
    {
        context.ChangeTracker.Clear();   // each call is a new request in the app
        return new PatientService(new UnitOfWork(context));
    }

    private static CreatePatientRequest Patient(string firstName, params (string Number, bool Primary)[] phones) => new()
    {
        FirstName = firstName,
        LastName = "Test",
        Phones = phones.Select(p => new CreatePatientPhoneRequest { PhoneNumber = p.Number, IsPrimary = p.Primary }).ToList()
    };

    [Fact]
    public async Task Same_primary_phone_in_another_format_is_refused()
    {
        await using var context = await SeededAsync();
        await Patients(context).CreatePatientAsync(Patient("First", ("01001234567", true)), 1);

        var second = await Patients(context).CreatePatientAsync(Patient("Second", ("+20 100 123 4567", true)), 1);

        Assert.False(second.IsSuccess);
        Assert.Contains("First Test", second.Errors.First());
    }

    [Fact]
    public async Task First_phone_becomes_primary_and_numbers_are_stored_normalised()
    {
        await using var context = await SeededAsync();

        await Patients(context).CreatePatientAsync(Patient("Only", ("010 0123 4567", false), ("+20 122 000 1111", false)), 1);

        var phones = await context.PatientPhones.AsNoTracking().OrderBy(p => p.Id).ToListAsync();
        Assert.Equal(new[] { "01001234567", "01220001111" }, phones.Select(p => p.PhoneNumber));
        Assert.Equal(new[] { true, false }, phones.Select(p => p.IsPrimary));
    }

    [Fact]
    public async Task A_secondary_phone_may_be_shared()
    {
        await using var context = await SeededAsync();
        await Patients(context).CreatePatientAsync(Patient("Parent", ("01001234567", true)), 1);

        var child = await Patients(context).CreatePatientAsync(Patient("Child", ("01009999999", true), ("01001234567", false)), 1);

        Assert.True(child.IsSuccess);
    }

    [Fact]
    public async Task A_phone_is_required()
    {
        await using var context = await SeededAsync();

        var result = await Patients(context).CreatePatientAsync(Patient("NoPhone"), 1);

        Assert.False(result.IsSuccess);
    }

    [Fact]
    public async Task Adding_a_primary_phone_moves_the_primary_flag()
    {
        await using var context = await SeededAsync();
        var created = await Patients(context).CreatePatientAsync(Patient("Mover", ("01001234567", true)), 1);
        var patientId = created.Data!.Id;

        await Patients(context).AddPhoneAsync(patientId, new CreatePatientPhoneRequest { PhoneNumber = "01112223333", IsPrimary = true });

        var primaries = await context.PatientPhones.AsNoTracking()
            .Where(p => p.PatientId == patientId && p.IsPrimary).Select(p => p.PhoneNumber).ToListAsync();
        Assert.Equal(new[] { "01112223333" }, primaries);
    }

    [Fact]
    public async Task The_primary_phone_cannot_be_removed()
    {
        await using var context = await SeededAsync();
        var created = await Patients(context).CreatePatientAsync(Patient("Keeper", ("01001234567", true)), 1);
        var phoneId = await context.PatientPhones.Where(p => p.PatientId == created.Data!.Id).Select(p => p.Id).SingleAsync();

        var result = await Patients(context).RemovePhoneAsync(created.Data!.Id, phoneId);

        Assert.False(result.IsSuccess);
    }
}
```

**Check:**

```powershell
dotnet build SafyaClinic\SafyaClinic.sln
dotnet test SafyaClinic\SafyaClinic.sln
```

Expected **Passed: 150** (137 + 13), Failed: 0. If anything fails, send me the output — do not change the tests to make them pass.

### Step 8 — Migration (needs your approval; only after Step 1 is clean)

**8a.** Generate:

```powershell
Push-Location SafyaClinic\SafyaClinic.Web
dotnet ef migrations add UniquePrimaryPhone --project ..\SafyaClinic.Infrastructure
Pop-Location
```

`Up` must contain only one `CreateIndex` named `UX_PatientPhones_PrimaryPhoneNumber` on `PatientPhones` with `unique: true` and `filter: "[IsPrimary] = 1"`. Anything else → stop and send me the file.

**8b.** Add the data clean-up **before** the index. In `…_UniquePrimaryPhone.cs`, at the very beginning of `Up` (before `migrationBuilder.CreateIndex(`), add:

```csharp
            // F5b: same normal form as PhoneNumbers.Normalize (symbols removed, +20/0020 → 0).
            migrationBuilder.Sql(@"
UPDATE PatientPhones SET PhoneNumber =
    REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(LTRIM(RTRIM(PhoneNumber)),' ',''),'-',''),'(',''),')',''),'.',''),'+','');
UPDATE PatientPhones SET PhoneNumber = '0' + SUBSTRING(PhoneNumber, 5, 50) WHERE PhoneNumber LIKE '0020%';
UPDATE PatientPhones SET PhoneNumber = '0' + SUBSTRING(PhoneNumber, 3, 50) WHERE PhoneNumber LIKE '20%' AND LEN(PhoneNumber) >= 11;");

            // F5b: exactly one primary per patient — the oldest phone when none is marked, the oldest when several are.
            migrationBuilder.Sql(@"
UPDATE pp SET IsPrimary = 1 FROM PatientPhones pp
WHERE pp.Id IN (SELECT MIN(Id) FROM PatientPhones GROUP BY PatientId HAVING SUM(CASE WHEN IsPrimary = 1 THEN 1 ELSE 0 END) = 0);
UPDATE pp SET IsPrimary = 0 FROM PatientPhones pp
WHERE pp.IsPrimary = 1 AND EXISTS (SELECT 1 FROM PatientPhones o WHERE o.PatientId = pp.PatientId AND o.IsPrimary = 1 AND o.Id < pp.Id);");

```

`Down` stays as generated (it only drops the index).

**8c.** Back up and start the app:

```powershell
powershell -ExecutionPolicy Bypass -File .assessment\backup-db.ps1
dotnet run --project SafyaClinic\SafyaClinic.Web --launch-profile http
```

Doing 8c is your explicit approval for these changes on the **dev** database: phone numbers rewritten to the normal form, primary flags fixed, unique index added. If the migration fails (a duplicate was missed), nothing is changed — send me the error.

### Step 9 — Manual check

1. Patients → New: leave the phone empty → the browser asks for it.
2. Register a test patient with phone `0100 000 0001`. Register another with `+20 100 000 0001` → red message naming the first patient.
3. Search patients for `+201000000001` → the first patient is found.
4. On the first patient's page: add `01000000002` ticked *Primary* → it becomes primary; try removing it → refused; remove the old (now secondary) one → works.

### Step 10 — Commit, push, PR

```powershell
git add SafyaClinic/SafyaClinic.Application SafyaClinic/SafyaClinic.Infrastructure SafyaClinic/SafyaClinic.Web SafyaClinic/tests Docs
git status
git commit -m "F5b: normalised patient phones; unique primary phone (D8); phone required; primary cannot be removed"
git push -u origin chore/f5b-unique-phone
```

Open the PR and send me the number and CI result. Do not merge before review.

## Hosted servers

Run the Step 1 report on the test and demo databases before their deployment script; resolve duplicates there the same way (with approval) before running the script.

## Acceptance criteria

- [ ] Registering with another patient's primary phone (any format) is refused with the existing patient named.
- [ ] Phones are stored normalised; search by phone works in any format.
- [ ] Each patient with phones has exactly one primary; the primary cannot be removed; secondary phones may be shared.
- [ ] Unique filtered index `UX_PatientPhones_PrimaryPhoneNumber` exists.
- [ ] 150 tests pass locally and in CI.
