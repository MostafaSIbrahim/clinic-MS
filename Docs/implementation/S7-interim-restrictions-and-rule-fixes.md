# Slice S7 — Interim restrictions and rule fixes

Roadmap item **S7** (Group 1, last behaviour slice before S8). Fixes **F05**, **F06 (interim)**, **F15**, **F32**. Implements decisions **D4**, **D5 (removal part)** and **D6**.

| | |
|---|---|
| Branch | `chore/s7-restrictions` (from an up-to-date `main`, after PR #9 is merged) |
| Behaviour change | 1. **`POST /Reservations/MarkPaid` is removed** (D5) — it marked visits paid with no payment record; no screen used it. 2. **Price override is Admin-only** (D4): for everyone else the amount comes from the treatment type (and stays as it was when editing, unless the treatment type changes). The amount field is read-only for non-Admins. 3. ***Completed* from the status form is Admin-only** (D4): doctors complete visits from the consultation screen, as today. 4. **A doctor or nutritionist cannot change or cancel the status of another doctor's reservation** (D6, F32); Reception and Admin can. 5. The misnamed role **"Reception Admin" is renamed to `ReceptionAdmin`** at start-up so the existing payment policies finally apply (F06 interim); the role is created if it does not exist. 6. Analysis status changes and attachment deletes now show a success/error message (F15) |
| Database | **No schema change.** ⚠ One **data** change at start-up: the role row named `Reception Admin` is renamed (or a `ReceptionAdmin` role row is inserted). See Step 7 |
| Size | S |

## Design

| Decision | Choice |
|---|---|
| Where the rules live | In `ReservationService`, so every caller is covered. Roles are read **from the database** for the acting user, not from the browser or cookie. The rule logic itself is a small pure class (`ReservationRules`) with its own tests |
| Admin who is also a doctor | Treated as Admin (may change any reservation). D6 says front-desk and admin groups keep status changes |
| "Cancel" button | Counts as a status change, so D6 applies to it too |
| Removing `MarkPaid` | Controller action, service method and interface entry are deleted. Write-off payments (the D5 replacement) come with the permission model in the pilot |
| Role rename | Done by `DbSeeder` at start-up, idempotent: rename only if `ReceptionAdmin` does not already exist. Users who hold the role keep it (same row); they must sign in again to pick up the new name |
| Out of scope | Permission groups (pilot), free-text role creation on the Roles screen (pilot), write-off payments (pilot), double-booking (F11, pilot) |

## Affected files

| Action | Path |
|---|---|
| Add | `SafyaClinic/SafyaClinic.Application/Services/ReservationRules.cs` |
| Edit | `SafyaClinic/SafyaClinic.Application/Services/ReservationService.cs` |
| Edit | `SafyaClinic/SafyaClinic.Application/Interfaces/Services/IReservationService.cs` |
| Edit | `SafyaClinic/SafyaClinic.Infrastructure/Data/DbSeeder.cs` |
| Edit | `SafyaClinic/SafyaClinic.Web/Controllers/ReservationsController.cs` |
| Edit | `SafyaClinic/SafyaClinic.Web/Controllers/AnalysisController.cs`, `MedicalRecordsController.cs` |
| Edit | `SafyaClinic/SafyaClinic.Web/Views/Reservations/Create.cshtml`, `Edit.cshtml`, `Details.cshtml` |
| Add | `SafyaClinic/tests/SafyaClinic.Tests/Application/ReservationRulesTests.cs` |
| Add | `SafyaClinic/tests/SafyaClinic.Tests/Application/ReservationServiceRulesTests.cs` |
| Edit | `SafyaClinic/tests/SafyaClinic.Tests/Infrastructure/DbSeederTests.cs` |
| Add | `Docs/implementation/S7-interim-restrictions-and-rule-fixes.md` |

## Risks

| Risk | Mitigation |
|---|---|
| Staff relied on changing prices or completing visits from the status form | Agreed in D4; Admin can still do both. The consultation screen's *Complete* is unchanged |
| A doctor covering a colleague cannot update their reservation | Reception/Admin can; this is the D6 rule. Permission groups (pilot) may add exceptions |
| Rename surprises someone | One row; idempotent; the policies already expect `ReceptionAdmin` |

## Rollback

Revert the PR's merge commit. The renamed role can stay (old code then treats it like before the rename — which was "grants nothing"; renaming back is not needed).

---

## Steps

PowerShell, from `D:\MyDevWork\clinic-MS`.

### Step 0 — Branch

```powershell
git switch main
git pull
git switch -c chore/s7-restrictions
```

### Step 1 — The rules (Application)

Create `SafyaClinic/SafyaClinic.Application/Services/ReservationRules.cs`:

```csharp
namespace SafyaClinic.Application.Services;

/// <summary>
/// Interim reservation rules until permission groups ship (S7; decisions D4 and D6).
/// Roles are read from the database by the caller, never from the browser.
/// </summary>
public static class ReservationRules
{
    public const string AdminRole = "Admin";
    public const string DoctorRole = "Doctor";
    public const string NutritionistRole = "Nutritionist";
    public const string CompletedStatus = "Completed";

    /// <summary>D4: only an Admin may set a price different from the treatment type's.</summary>
    public static bool CanOverridePrice(IReadOnlyCollection<string> roles) => roles.Contains(AdminRole);

    /// <summary>Returns a message for the user, or null when the status change is allowed.</summary>
    public static string? CheckStatusChange(
        IReadOnlyCollection<string> roles, int actingUserId, int reservationDoctorId, string targetStatus)
    {
        var isAdmin = roles.Contains(AdminRole);

        // D4: doctors complete a visit from the consultation screen, which records it properly.
        if (targetStatus == CompletedStatus && !isAdmin)
            return "Only an administrator can set a visit to Completed here. The doctor completes it from the consultation screen.";

        // D6: a clinician never changes the status of another doctor's reservation.
        var isClinician = roles.Contains(DoctorRole) || roles.Contains(NutritionistRole);
        if (isClinician && !isAdmin && reservationDoctorId != actingUserId)
            return "You cannot change the status of another doctor's reservation.";

        return null;
    }
}
```

### Step 2 — Apply the rules in `ReservationService`

File: `SafyaClinic/SafyaClinic.Application/Services/ReservationService.cs`.

**2a. Helpers.** Directly after the constructor line

```csharp
    public ReservationService(IUnitOfWork uow) => _uow = uow;
```

add:

```csharp

    // ── S7 rule helpers ──────────────────────────────────────
    private Task<List<string>> RoleNamesAsync(int userId) =>
        _uow.UserRoles
            .Query()
            .Where(ur => ur.UserId == userId)
            .Select(ur => ur.Role.RoleName)
            .ToListAsync();

    private async Task<string?> StatusChangeProblemAsync(int reservationId, int actingUserId, string targetStatus)
    {
        var doctorId = await _uow.Reservations
            .Query()
            .Where(r => r.Id == reservationId)
            .Select(r => (int?)r.DoctorId)
            .FirstOrDefaultAsync();

        if (doctorId is null)
            return "Reservation not found.";

        return ReservationRules.CheckStatusChange(
            await RoleNamesAsync(actingUserId), actingUserId, doctorId.Value, targetStatus);
    }
```

**2b. Create — price override (D4).** In `CreateReservationAsync`, replace:

```csharp
        var totalAmount = request.TotalAmount ?? treatmentType.DefaultCost;
```

with:

```csharp
        var requestedAmount = ReservationRules.CanOverridePrice(await RoleNamesAsync(createdBy))
            ? request.TotalAmount
            : null;   // D4: only an Admin may change the price
        var totalAmount = requestedAmount ?? treatmentType.DefaultCost;
```

**2c. Edit — price override (D4).** Change the signature line

```csharp
    public async Task<ServiceResult> UpdateReservationAsync(
        int reservationId, UpdateReservationRequest request)
```

to

```csharp
    public async Task<ServiceResult> UpdateReservationAsync(
        int reservationId, UpdateReservationRequest request, int updatedBy)
```

and replace:

```csharp
        var totalAmount = request.TotalAmount
            ?? (request.TreatmentTypeId != r.TreatmentTypeId ? treatmentType.DefaultCost : r.TotalAmount);
```

with:

```csharp
        var requestedAmount = ReservationRules.CanOverridePrice(await RoleNamesAsync(updatedBy))
            ? request.TotalAmount
            : null;   // D4: only an Admin may change the price
        var totalAmount = requestedAmount
            ?? (request.TreatmentTypeId != r.TreatmentTypeId ? treatmentType.DefaultCost : r.TotalAmount);
```

**2d. Status change (D4, D6).** In `UpdateStatusAsync`, replace:

```csharp
        if (reservationId <= 0 || currentUserId <= 0)
        {
            return ServiceResult<ReservationDto>.Failure(
                "Invalid reservation or user.");
        }

        try
```

with:

```csharp
        if (reservationId <= 0 || currentUserId <= 0)
        {
            return ServiceResult<ReservationDto>.Failure(
                "Invalid reservation or user.");
        }

        var targetStatus = await _uow.ReservationStatuses
            .Query()
            .Where(s => s.Id == statusId)
            .Select(s => s.StatusName)
            .FirstOrDefaultAsync();
        if (targetStatus is null)
            return ServiceResult<ReservationDto>.Failure("Invalid status.");

        var problem = await StatusChangeProblemAsync(reservationId, currentUserId, targetStatus);
        if (problem is not null)
            return ServiceResult<ReservationDto>.Failure(problem);

        try
```

**2e. Cancel (D6).** In `CancelReservationAsync`, replace:

```csharp
        if (reservationId <= 0 || currentUserId <= 0)
            return ServiceResult.Failure("Invalid reservation or user.");

        try
```

with:

```csharp
        if (reservationId <= 0 || currentUserId <= 0)
            return ServiceResult.Failure("Invalid reservation or user.");

        var problem = await StatusChangeProblemAsync(reservationId, currentUserId, "Cancelled");
        if (problem is not null)
            return ServiceResult.Failure(problem);

        try
```

**2f. Remove `MarkAsPaidAsync` (D5).** Delete the whole method `public async Task<ServiceResult> MarkAsPaidAsync(int reservationId) { … }` (just before `CancelReservationAsync`).

**2g. Interface.** In `SafyaClinic/SafyaClinic.Application/Interfaces/Services/IReservationService.cs`:

- replace `Task<ServiceResult> UpdateReservationAsync(int reservationId, UpdateReservationRequest request);` with
  `Task<ServiceResult> UpdateReservationAsync(int reservationId, UpdateReservationRequest request, int updatedBy);`
- delete the line `Task<ServiceResult> MarkAsPaidAsync(int reservationId);`

### Step 3 — `ReceptionAdmin` role (`DbSeeder.cs`)

**3a.** In `SeedAsync`, after the line `await SeedRolesAsync(context);` add:

```csharp
        await EnsureReceptionAdminRoleAsync(context);
```

**3b.** After the end of `SeedRolesAsync` (its closing `}`), add:

```csharp

    /// <summary>Must match the role name used by the payment policies in Program.cs.</summary>
    public const string ReceptionAdminRoleName = "ReceptionAdmin";

    // F06 interim (S7): a role created as "Reception Admin" (with a space) matched no policy,
    // so its users could not reach payment pages. Rename it once, or create the role.
    private static async Task EnsureReceptionAdminRoleAsync(SafyaDbContext context)
    {
        if (await context.Roles.AnyAsync(r => r.RoleName == ReceptionAdminRoleName)) return;

        var misnamed = await context.Roles.FirstOrDefaultAsync(r => r.RoleName == "Reception Admin");
        if (misnamed is not null)
            misnamed.RoleName = ReceptionAdminRoleName;
        else
            await context.Roles.AddAsync(new Role
            {
                RoleName = ReceptionAdminRoleName,
                Description = "Front-desk supervisor: payments and payment reports"
            });

        await context.SaveChangesAsync();
    }
```

### Step 4 — Controllers

**4a.** `Controllers/ReservationsController.cs`

Delete the whole `MarkPaid` action (from its `[HttpPost]` / `[ValidateAntiForgeryToken]` lines through its closing `}`):

```csharp
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MarkPaid(int id)
    {
        await _reservationService.MarkAsPaidAsync(id);
        return RedirectToAction(nameof(Details), new { id });
    }
```

In the `Edit` POST, replace:

```csharp
        var result = await _reservationService.UpdateReservationAsync(id, model);
```

with:

```csharp
        var result = await _reservationService.UpdateReservationAsync(id, model, CurrentUserId);
```

In `Cancel`, replace:

```csharp
            return RedirectToAction(nameof(Index));
        }

        return RedirectWithSuccess(
```

with:

```csharp
            return RedirectToAction(nameof(Details), new { id });
        }

        return RedirectWithSuccess(
```

(so a refused cancel shows its message on the reservation itself).

**4b.** `Controllers/AnalysisController.cs` (F15 — message only; lab behaviour unchanged).

Replace the body of `UpdateStatus`:

```csharp
            await _analysisService.UpdateStatusAsync(id, model);
            return RedirectToAction(nameof(Details), new { id });
```

with:

```csharp
            var result = await _analysisService.UpdateStatusAsync(id, model);
            if (result.IsSuccess) Success(string.IsNullOrEmpty(result.Message) ? "Status updated." : result.Message);
            else Error(result.Errors.FirstOrDefault() ?? "Could not update the analysis status.");
            return RedirectToAction(nameof(Details), new { id });
```

In `DeleteAttachment`, replace:

```csharp
            if (result.IsSuccess && attachment.IsSuccess)
                _files.Delete(attachment.Data!.FilePath);

            return RedirectToAction(nameof(Details), new { id = analysisId });
```

with:

```csharp
            if (result.IsSuccess && attachment.IsSuccess)
                _files.Delete(attachment.Data!.FilePath);

            if (result.IsSuccess) Success("Attachment deleted.");
            else Error(result.Errors.FirstOrDefault() ?? "Could not delete the attachment.");
            return RedirectToAction(nameof(Details), new { id = analysisId });
```

**4c.** `Controllers/MedicalRecordsController.cs` (F15).

In `DeleteAttachment`, replace:

```csharp
            if (result.IsSuccess && attachment.IsSuccess)
                _files.Delete(attachment.Data!.FilePath);

            return RedirectToAction(nameof(Details), new { id = recordId });
```

with:

```csharp
            if (result.IsSuccess && attachment.IsSuccess)
                _files.Delete(attachment.Data!.FilePath);

            if (result.IsSuccess) Success("Attachment deleted.");
            else Error(result.Errors.FirstOrDefault() ?? "Could not delete the attachment.");
            return RedirectToAction(nameof(Details), new { id = recordId });
```

In `Details`, replace:

```csharp
            if (!result.IsSuccess || result.Data is null)
                return RedirectToAction("Index", "Patients");
```

with:

```csharp
            if (!result.IsSuccess || result.Data is null)
            {
                Error("Medical record not found.");
                return RedirectToAction("Index", "Patients");
            }
```

### Step 5 — Views

**5a.** `Views/Reservations/Create.cshtml` **and** `Views/Reservations/Edit.cshtml` — replace (same in both files):

```cshtml
                    <input asp-for="TotalAmount" id="totalAmountInput" type="number" step="0.01" class="form-control" />
                    <small class="text-muted">Auto-filled from the treatment type's price — adjust if needed.</small>
```

with:

```cshtml
                    <input asp-for="TotalAmount" id="totalAmountInput" type="number" step="0.01" class="form-control"
                           readonly="@(!User.IsInRole("Admin"))" aria-describedby="totalAmountHelp" />
                    <small id="totalAmountHelp" class="text-muted">
                        @(User.IsInRole("Admin")
                            ? "Auto-filled from the treatment type's price — adjust if needed."
                            : "Set by the treatment type. Only an administrator can change the price.")
                    </small>
```

(`readonly` still lets the script fill in the treatment type's price; the server ignores a changed value from non-Admins anyway.)

**5b.** `Views/Reservations/Details.cshtml` — in the *Update Status* card replace:

```cshtml
                        <option value="3" selected="@(Model.StatusName == "Completed")" disabled="@(!canCompleteFromQueue)">Completed</option>
```

with:

```cshtml
                        <option value="3" selected="@(Model.StatusName == "Completed")" disabled="@(!canCompleteFromQueue || !User.IsInRole("Admin"))">Completed</option>
```

and replace:

```cshtml
                <p class="small text-muted mt-2 mb-0">
                    Completion is available after consultation starts
                </p> 
```

with:

```cshtml
                <p class="small text-muted mt-2 mb-0">
                    Doctors complete the visit from the consultation screen. Only an administrator can set Completed here, after the consultation starts.
                </p>
```

### Step 6 — Tests

**6a.** Create `SafyaClinic/tests/SafyaClinic.Tests/Application/ReservationRulesTests.cs`:

```csharp
using SafyaClinic.Application.Services;
using Xunit;

namespace SafyaClinic.Tests.Application;

/// <summary>S7: D4 (price, completion) and D6 (other doctors' reservations).</summary>
public class ReservationRulesTests
{
    private const int Me = 10;
    private const int OtherDoctor = 20;

    private static string[] Roles(params string[] roles) => roles;

    [Fact]
    public void Admin_may_set_completed()
    {
        Assert.Null(ReservationRules.CheckStatusChange(Roles("Admin"), Me, OtherDoctor, "Completed"));
    }

    [Fact]
    public void Reception_may_not_set_completed()
    {
        Assert.NotNull(ReservationRules.CheckStatusChange(Roles("Reception"), Me, OtherDoctor, "Completed"));
    }

    [Fact]
    public void Doctor_may_not_set_completed_through_the_status_form()
    {
        Assert.NotNull(ReservationRules.CheckStatusChange(Roles("Doctor"), Me, Me, "Completed"));
    }

    [Fact]
    public void Doctor_may_change_the_status_of_their_own_reservation()
    {
        Assert.Null(ReservationRules.CheckStatusChange(Roles("Doctor"), Me, Me, "Confirmed"));
    }

    [Theory]
    [InlineData("Doctor")]
    [InlineData("Nutritionist")]
    public void Clinician_may_not_change_another_doctors_reservation(string role)
    {
        Assert.NotNull(ReservationRules.CheckStatusChange(Roles(role), Me, OtherDoctor, "Cancelled"));
    }

    [Fact]
    public void Reception_may_change_any_reservation()
    {
        Assert.Null(ReservationRules.CheckStatusChange(Roles("Reception"), Me, OtherDoctor, "Cancelled"));
    }

    [Fact]
    public void Admin_who_is_also_a_doctor_may_change_any_reservation()
    {
        Assert.Null(ReservationRules.CheckStatusChange(Roles("Admin", "Doctor"), Me, OtherDoctor, "NoShow"));
    }

    [Theory]
    [InlineData("Admin", true)]
    [InlineData("Reception", false)]
    [InlineData("Doctor", false)]
    public void Only_admin_may_override_the_price(string role, bool expected)
    {
        Assert.Equal(expected, ReservationRules.CanOverridePrice(Roles(role)));
    }
}
```

**6b.** Create `SafyaClinic/tests/SafyaClinic.Tests/Application/ReservationServiceRulesTests.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using SafyaClinic.Application.DTOs.Reservation;
using SafyaClinic.Application.Services;
using SafyaClinic.Domain.Entities.Patient;
using SafyaClinic.Domain.Entities.Reservation;
using SafyaClinic.Domain.Identity;
using SafyaClinic.Infrastructure.Data;
using SafyaClinic.Infrastructure.Repositories;
using Xunit;

namespace SafyaClinic.Tests.Application;

/// <summary>
/// S7: the service applies D4/D6 with roles read from the database.
/// Refusals happen before the SQL-Server-only status update, so they run in memory.
/// </summary>
public class ReservationServiceRulesTests
{
    private sealed record Setup(
        SafyaDbContext Context, int AdminId, int ReceptionId, int DoctorAId, int DoctorBId,
        int PatientId, int ClinicId, int TreatmentTypeId, decimal DefaultCost, int ReservationOfDoctorA);

    private static async Task<int> AddUserAsync(SafyaDbContext context, string name, string phone, string role)
    {
        var user = new User { FullName = name, PhoneNumber = phone, PasswordHash = "x", SecurityStamp = "s" };
        context.Users.Add(user);
        await context.SaveChangesAsync();
        var roleId = await context.Roles.Where(r => r.RoleName == role).Select(r => r.Id).SingleAsync();
        context.UserRoles.Add(new UserRole { UserId = user.Id, RoleId = roleId });
        await context.SaveChangesAsync();
        return user.Id;
    }

    private static async Task<Setup> SeedAsync()
    {
        var context = new SafyaDbContext(new DbContextOptionsBuilder<SafyaDbContext>()
            .UseInMemoryDatabase($"reservation-rules-{Guid.NewGuid()}")
            .Options);
        await DbSeeder.SeedAsync(context, new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { [DbSeeder.AdminPasswordSettingKey] = "Rules-Test-Password-1" })
            .Build());

        var adminId = await context.Users.Where(u => u.PhoneNumber == "0100").Select(u => u.Id).SingleAsync();
        var receptionId = await AddUserAsync(context, "Reception", "0201", "Reception");
        var doctorAId = await AddUserAsync(context, "Doctor A", "0202", "Doctor");
        var doctorBId = await AddUserAsync(context, "Doctor B", "0203", "Doctor");

        var patient = new Patient { FirstName = "Test", LastName = "Patient" };
        context.Patients.Add(patient);
        await context.SaveChangesAsync();

        var clinicId = await context.Clinics.Select(c => c.Id).FirstAsync();
        var type = await context.TreatmentTypes.Where(t => t.DefaultCost > 0).OrderBy(t => t.Id).FirstAsync();

        var reservation = new Reservation
        {
            PatientId = patient.Id, DoctorId = doctorAId, ClinicId = clinicId, TreatmentTypeId = type.Id,
            StatusId = await context.ReservationStatuses.Where(s => s.StatusName == "Confirmed").Select(s => s.Id).SingleAsync(),
            ReservationDate = DateTime.Today, ReservationTime = new TimeSpan(10, 0, 0), TotalAmount = type.DefaultCost
        };
        context.Reservations.Add(reservation);
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        return new Setup(context, adminId, receptionId, doctorAId, doctorBId, patient.Id, clinicId, type.Id,
            type.DefaultCost!.Value, reservation.Id);
    }

    private static ReservationService Service(SafyaDbContext context)
    {
        context.ChangeTracker.Clear();   // each call is a new request in the app
        return new ReservationService(new UnitOfWork(context));
    }

    private CreateReservationRequest Booking(Setup s, decimal amount) => new()
    {
        PatientId = s.PatientId, DoctorId = s.DoctorAId, ClinicId = s.ClinicId, TreatmentTypeId = s.TreatmentTypeId,
        ReservationDate = DateTime.Today.AddDays(1), ReservationTime = new TimeSpan(11, 0, 0), TotalAmount = amount
    };

    [Fact]
    public async Task Doctor_cannot_change_the_status_of_another_doctors_reservation()
    {
        var s = await SeedAsync();
        var cancelled = await s.Context.ReservationStatuses.Where(x => x.StatusName == "Cancelled").Select(x => x.Id).SingleAsync();

        var result = await Service(s.Context).UpdateStatusAsync(s.ReservationOfDoctorA, cancelled, s.DoctorBId);

        Assert.False(result.IsSuccess);
        Assert.Contains("another doctor", result.Errors.First());
    }

    [Fact]
    public async Task Doctor_cannot_cancel_another_doctors_reservation()
    {
        var s = await SeedAsync();

        var result = await Service(s.Context).CancelReservationAsync(s.ReservationOfDoctorA, s.DoctorBId, "test");

        Assert.False(result.IsSuccess);
        Assert.Contains("another doctor", result.Errors.First());
    }

    [Fact]
    public async Task Reception_price_is_replaced_by_the_treatment_type_price()
    {
        var s = await SeedAsync();

        await Service(s.Context).CreateReservationAsync(Booking(s, amount: 1m), s.ReceptionId);

        var stored = await s.Context.Reservations.AsNoTracking()
            .Where(r => r.Id != s.ReservationOfDoctorA).Select(r => r.TotalAmount).SingleAsync();
        Assert.Equal(s.DefaultCost, stored);
    }

    [Fact]
    public async Task Admin_price_override_is_kept()
    {
        var s = await SeedAsync();

        await Service(s.Context).CreateReservationAsync(Booking(s, amount: 1m), s.AdminId);

        var stored = await s.Context.Reservations.AsNoTracking()
            .Where(r => r.Id != s.ReservationOfDoctorA).Select(r => r.TotalAmount).SingleAsync();
        Assert.Equal(1m, stored);
    }
}
```

**6c.** `SafyaClinic/tests/SafyaClinic.Tests/Infrastructure/DbSeederTests.cs` — add these three tests at the end of the class (before its final `}`). They use the file's existing `NewContext()` and `Config(...)` helpers:

```csharp

    [Fact]
    public async Task Misnamed_reception_admin_role_is_renamed()
    {
        await using var context = NewContext();
        // Roles already exist, so the seeder skips its role list; the admin seed still needs "Admin".
        context.Roles.AddRange(new Role { RoleName = "Admin" }, new Role { RoleName = "Reception Admin" });
        await context.SaveChangesAsync();

        await DbSeeder.SeedAsync(context, Config(TestPassword));

        Assert.False(await context.Roles.AnyAsync(r => r.RoleName == "Reception Admin"));
        Assert.Equal(1, await context.Roles.CountAsync(r => r.RoleName == DbSeeder.ReceptionAdminRoleName));
    }

    [Fact]
    public async Task Reception_admin_role_is_created_when_missing()
    {
        await using var context = NewContext();

        await DbSeeder.SeedAsync(context, Config(TestPassword));

        Assert.True(await context.Roles.AnyAsync(r => r.RoleName == DbSeeder.ReceptionAdminRoleName));
    }

    [Fact]
    public async Task Seeding_twice_keeps_a_single_reception_admin_role()
    {
        await using var context = NewContext();

        await DbSeeder.SeedAsync(context, Config(TestPassword));
        await DbSeeder.SeedAsync(context, Config(TestPassword));

        Assert.Equal(1, await context.Roles.CountAsync(r => r.RoleName == DbSeeder.ReceptionAdminRoleName));
    }
```

If `Role` is not found in that file, add `using SafyaClinic.Domain.Identity;` at the top (it is usually there already for `User`).

**Check:**

```powershell
dotnet build SafyaClinic\SafyaClinic.sln
dotnet test SafyaClinic\SafyaClinic.sln
git grep -n "MarkPaid\|MarkAsPaid" -- SafyaClinic
```

Expected: **Passed: 94** (76 + 11 rules + 4 service + 3 seeder), Failed: 0; `git grep` prints nothing. If anything fails, send me the output — do not change the tests to make them pass.

### Step 7 — Data change at start-up (needs your approval)

Starting the app with this version renames the dev database's `Reception Admin` role row to `ReceptionAdmin` (or inserts that role if neither exists). No other data changes; no schema change.

```powershell
powershell -ExecutionPolicy Bypass -File .assessment\backup-db.ps1
dotnet run --project SafyaClinic\SafyaClinic.Web --launch-profile http
```

Running it is your explicit approval for that one-row change on the **dev** database.

### Step 8 — Manual check

1. **Roles screen:** the role now shows as `ReceptionAdmin`. A user holding it — after signing in again — sees *Payments Dashboard* / *Payment Report*.
2. **Price (Reception user):** new reservation → the Amount field is read-only and shows the treatment type's price; the saved reservation has that price. As Admin, the field is editable and a changed amount is kept.
3. **Completed (Admin vs others):** on a reservation in consultation, the *Completed* option is disabled for Reception and Doctors; as Admin it is available.
4. **Other doctor's reservation (Doctor user):** open a reservation of a different doctor → change status or Cancel → red *"You cannot change the status of another doctor's reservation."*; on their own reservation it works. Reception can change both.
5. **MarkPaid gone:** nothing in the UI used it; `git grep` above shows no references.
6. **Feedback:** delete an attachment (Admin) → green *Attachment deleted.*; open `/MedicalRecords/Details/999999` → Patients list with red *Medical record not found.*

### Step 9 — Commit, push, PR

```powershell
git add SafyaClinic/SafyaClinic.Application SafyaClinic/SafyaClinic.Infrastructure SafyaClinic/SafyaClinic.Web SafyaClinic/tests Docs
git reset SafyaClinic/SafyaClinic.Web/SafyaClinic.Web.csproj.user
git status
git commit -m "S7: remove MarkPaid; Admin-only price override and Completed-by-status; doctors cannot change others' reservations; ReceptionAdmin role; surface ignored results"
git push -u origin chore/s7-restrictions
```

Open the PR and send me the number and CI result. Do not merge before review.

---

## Hosted servers (when you next publish)

The role rename/insert happens at start-up on each site. Users holding that role must sign in again (the S6 one-time sign-out already forces this if S6 and S7 are published together).

## Acceptance criteria

- [ ] `MarkPaid` route and service method are gone.
- [ ] Non-Admins cannot change a reservation's price (create or edit); Admins can.
- [ ] Only Admin can set *Completed* from the status form; doctors' consultation completion is unchanged.
- [ ] Doctors/Nutritionists cannot change or cancel another doctor's reservation; Reception and Admin can.
- [ ] The `ReceptionAdmin` role exists (renamed from `Reception Admin` where present) and its users reach payment pages.
- [ ] Analysis status update, attachment delete and a missing medical record all show feedback.
- [ ] 94 tests pass locally and in CI.
