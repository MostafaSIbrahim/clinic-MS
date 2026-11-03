# Slice F4b-2 — Controllers check permissions instead of roles

Roadmap item **F4**, slice b, part 2 (Group 2). Builds on F4a (catalogue, system groups) and F4b-1 (`[RequirePermission]`, access baseline).

| | |
|---|---|
| Branch | `chore/f4b2-controllers-permissions` (from an up-to-date `main`, after PR #20 is merged) |
| Behaviour change | **None for users.** Every `[Authorize(Policy = …)]` and `[Authorize(Roles = …)]` on controllers becomes `[RequirePermission(…)]`. The role checks inside actions (`IsAdmin`, `IsReception`, `IsDoctor` in payments) become permission checks, and so do the reservation rules D4 and D6. The seven role policies are deleted. From this slice on, **access is decided by groups**, which is what lets F4d make groups editable |
| Proof | `access-matrix.txt` must stay **byte-for-byte unchanged**, and the existing payment and reservation-rule tests keep passing with permissions in place of roles |
| Database | None |
| Size | M (many small edits of the same kind) |

## Design

| Decision | Choice |
|---|---|
| Controller level | The controller gets the area's **view** permission (for example `Patients.View`). Actions that change data **add** their own (`Patients.Edit`). Both must be met, the same as today's combined attributes, so access is identical and groups can later separate "view" from "edit" |
| Checks inside actions | `BaseController.CanAsync(code)` replaces `IsAdmin` and `IsReception`. These two properties are deleted, so the compiler finds any check that was missed |
| Still role-based | `IsDoctor || IsNutritionist` in **Dashboard** (a clinician sees their own day). That decides whose day to show, not what they may do, and it moves to `ClinicianType` in F4c together with the views |
| Reservation rules | `ReservationRules` takes the acting user's permission codes: D4 uses `Reservations.OverridePrice` and `Reservations.SetCompleted`, and D6 uses `Reservations.ChangeAnyDoctorStatus`. `ReservationService` reads them through `IPermissionStore` |
| Tests | The existing tests describe outcomes **by role**. They now turn each role into its group's permissions (the `RolePermissions` helper), so they prove the role-to-outcome results are unchanged |

## Affected files

| Action | Path |
|---|---|
| Edit | `SafyaClinic/SafyaClinic.Web/Controllers/BaseController.cs` |
| Edit | 14 controllers: `AnalysisController`, `Api/MedicinesController`, `Api/PatientsApiController`, `ClinicsController`, `FollowUpsController`, `LookupsController`, `MedicalRecordsController`, `NutritionController`, `PatientSourcesController`, `PatientsController`, `PaymentsController`, `ReservationsController`, `RolesController`, `UsersController` |
| Edit | `SafyaClinic/SafyaClinic.Web/Program.cs` |
| Delete | `SafyaClinic/SafyaClinic.Web/Security/LegacyRolePolicies.cs` |
| Edit | `SafyaClinic/SafyaClinic.Application/Services/ReservationRules.cs` |
| Edit | `SafyaClinic/SafyaClinic.Application/Services/ReservationService.cs` |
| Add | `SafyaClinic/tests/SafyaClinic.Tests/Web/Security/RolePermissions.cs` |
| Edit | `SafyaClinic/tests/SafyaClinic.Tests/Web/Security/AccessMatrixTests.cs` |
| Edit | `SafyaClinic/tests/SafyaClinic.Tests/Web/Payments/PaymentsControllerAccessTests.cs` |
| Edit | `SafyaClinic/tests/SafyaClinic.Tests/Application/ReservationRulesTests.cs` |
| Edit | `SafyaClinic/tests/SafyaClinic.Tests/Application/ReservationServiceRulesTests.cs` |
| Add | `Docs/implementation/F4b2-controllers-use-permissions.md` |

## Risks

| Risk | Mitigation |
|---|---|
| An attribute is missed or wrong | The access matrix lists every action; any difference fails the build and shows the line. A new test also fails if any role-based `[Authorize]` remains |
| An inside-action check is missed | `IsAdmin` and `IsReception` are deleted, so a missed one fails to compile. The payment and reservation-rule tests cover the logic |
| Groups in the database differ from the code's definition | Groups were seeded from that definition in F4a and nobody can edit them before F4d. Step 9 signs in with each role |

## Rollback

Revert the PR's merge commit. No data changes.

---

## Steps

Run these in PowerShell from `D:\MyDevWork\clinic-MS`. Open the solution in Visual Studio for the controller edits.

### Step 1 — Branch

```powershell
git switch main
git pull
git switch -c chore/f4b2-controllers-permissions
```

### Step 2 — `BaseController`

In `SafyaClinic/SafyaClinic.Web/Controllers/BaseController.cs`:

- Add with the other `using` lines:

  ```csharp
  using SafyaClinic.Web.Security;
  ```

- Replace these four lines:

  ```csharp
          protected bool IsAdmin => User.IsInRole("Admin");
          protected bool IsDoctor => User.IsInRole("Doctor");
          protected bool IsNutritionist => User.IsInRole("Nutritionist");
          protected bool IsReception => User.IsInRole("Reception");
  ```

  with:

  ```csharp
          // F4b: what a user may do is a permission from their groups.
          protected Task<bool> CanAsync(string permission) =>
              HttpContext.RequestServices.GetRequiredService<ICurrentPermissions>().HasAsync(permission);

          // Which clinician's day the dashboard shows; moves to ClinicianType in F4c.
          protected bool IsDoctor => User.IsInRole("Doctor");
          protected bool IsNutritionist => User.IsInRole("Nutritionist");
  ```

### Step 3 — Attributes on controllers

In **each controller** below, first add these two lines with its other `using` lines:

```csharp
using SafyaClinic.Application.Security;
using SafyaClinic.Web.Security;
```

Then make the changes listed. Wherever a table says **add**, put the attribute on its own line directly above the action's `public …` line, together with its other attributes. Where it says **replace**, the old `[Authorize(…)]` line goes and the new one takes its place. If an action has a GET and a POST version, change both.

**3a. `AnalysisController`**

| Where | Change |
|---|---|
| class | replace `[Authorize(Policy = "DoctorOrAdmin")]` → `[RequirePermission(Permissions.Analyses.View)]` |
| `GetRequest`, `PostRequest`, `UpdateStatus` | add `[RequirePermission(Permissions.Analyses.Manage)]` |
| `UploadAttachment` | replace `[Authorize(Policy = "ClinicalStaff")]` → `[RequirePermission(Permissions.Analyses.Manage)]` |
| `DeleteAttachment` | replace `[Authorize(Policy = "AdminOnly")]` → `[RequirePermission(Permissions.Attachments.Delete)]` |

**3b. `Api/MedicinesController`.** On the class, replace `[Authorize(Policy = "ClinicalStaff")]` with `[RequirePermission(Permissions.Medicines.Search)]`.

**3c. `Api/PatientsApiController`.** On the class, replace `[Authorize(Policy = "ClinicalStaff")]` with `[RequirePermission(Permissions.Patients.View)]`.

**3d. `ClinicsController`.** On the class, replace `[Authorize(Policy = "AdminOnly")]` with `[RequirePermission(Permissions.Settings.Clinics)]`.

**3e. `FollowUpsController`**

| Where | Change |
|---|---|
| class | replace `[Authorize(Roles = "Admin,Reception,Doctor")]` → `[RequirePermission(Permissions.FollowUps.View)]` |
| `Book` (GET and POST), `Dismiss` | add `[RequirePermission(Permissions.FollowUps.Manage)]` |

**3f. `LookupsController`.** On the class, replace `[Authorize(Roles = "Admin")]` with `[RequirePermission(Permissions.Settings.Lookups)]`.

**3g. `MedicalRecordsController`**

| Where | Change |
|---|---|
| class | replace `[Authorize(Policy = "DoctorOrAdmin")]` → `[RequirePermission(Permissions.Records.View)]` |
| `Create` (GET, POST), `Edit` (GET, POST), `AddTreatment`, `RemoveTreatment`, `CreatePrescription` (POST, GET), `AddPrescriptionItem`, `RemovePrescriptionItem`, `MarkPrescriptionPrinted` | add `[RequirePermission(Permissions.Records.Write)]` |
| `UploadPrescriptionAttachment` | replace `[Authorize(Policy = "ClinicalStaff")]` → `[RequirePermission(Permissions.Records.Write)]` |
| `CompleteConsultation` | replace `[Authorize(Policy = "DoctorOrAdmin")]` → `[RequirePermission(Permissions.Records.Write)]` |
| `Lock` | replace `[Authorize(Policy = "AdminOnly")]` → `[RequirePermission(Permissions.Records.Lock)]` |
| `DeleteAttachment` | replace `[Authorize(Policy = "AdminOnly")]` → `[RequirePermission(Permissions.Attachments.Delete)]` |

**3h. `NutritionController`**

| Where | Change |
|---|---|
| class | replace `[Authorize(Policy = "NutritionTeam")]` → `[RequirePermission(Permissions.Nutrition.View)]` |
| `Enroll` (GET, POST), `UpdateEnrollmentStatus`, `RecordFollowUp` (GET, POST), `CompleteFollowUp`, `EditFollowUp` (GET, POST), `DeleteFollowUp` | add `[RequirePermission(Permissions.Nutrition.Manage)]` |
| `CreatePackage` (GET, POST), `DeactivatePackage` | replace `[Authorize(Policy = "AdminOnly")]` → `[RequirePermission(Permissions.Nutrition.ManagePackages)]` |
| `InjectionTypes`, `CreateInjectionType` (GET, POST), `EditInjectionType` (GET, POST), `DeleteInjectionType`, `VitaminTypes`, `CreateVitaminType` (GET, POST), `EditVitaminType` (GET, POST), `DeleteVitaminType` | replace `[Authorize(Policy = "AdminOnly")]` → `[RequirePermission(Permissions.Settings.Lookups)]` |

**3i. `PatientSourcesController`.** On the class, replace `[Authorize(Policy = "AdminOnly")]` with `[RequirePermission(Permissions.Settings.PatientSources)]`.

**3j. `PatientsController`**

| Where | Change |
|---|---|
| class | replace `[Authorize(Policy = "ClinicalStaff")]` → `[RequirePermission(Permissions.Patients.View)]` |
| `Create` (GET, POST), `EditBasic` (GET, POST), `AddPhone`, `RemovePhone`, `AddAddress`, `RemoveAddress` | add `[RequirePermission(Permissions.Patients.Edit)]` |
| `Timeline` | replace `[Authorize(Policy = "DoctorOrAdmin")]` → `[RequirePermission(Permissions.Patients.ViewTimeline)]` |
| `EditMedical` (GET, POST) | replace `[Authorize(Policy = "DoctorOrAdmin")]` → `[RequirePermission(Permissions.Patients.EditMedical)]` |

**3k. `PaymentsController`.** Keep the class-level `[Authorize]`.

| Where | Change |
|---|---|
| `PatientSummary`, `Audit`, `Dashboard` | replace `[Authorize(Policy = "PaymentStaff")]` → `[RequirePermission(Permissions.Payments.View)]` |
| `Report` | replace `[Authorize(Policy = "PaymentReports")]` → `[RequirePermission(Permissions.Payments.ViewReport)]` |
| `RecalculateAllPaidStatuses`, `BackfillZeroCostPayments` | replace `[Authorize(Policy = "AdminOnly")]` → `[RequirePermission(Permissions.Payments.Maintenance)]` |
| `DashboardLineDetails` | replace `[Authorize(Policy = "AdminOnly")] // Payment access: …` → `[RequirePermission(Permissions.Payments.ViewFullDashboard)]` |
| `Collect` (GET, POST) | replace `[Authorize(Policy = "PaymentStaff")]` → `[RequirePermission(Permissions.Payments.Collect)]` |
| `Cancel` (GET, POST), `ChangeAmount` (GET, POST) | replace `[Authorize(Policy = "PaymentStaff")]` → `[RequirePermission(Permissions.Payments.Adjust)]` |

**3l. `ReservationsController`**

| Where | Change |
|---|---|
| class | replace `[Authorize(Policy = "ClinicalStaff")]` → `[RequirePermission(Permissions.Reservations.View)]` |
| `Create` (GET, POST), `Edit` (GET, POST), `UpdateStatus`, `Cancel` | add `[RequirePermission(Permissions.Reservations.Manage)]` |
| `Queue` | replace `[Authorize(Roles = "Admin,Reception,Doctor")]` → `[RequirePermission(Permissions.Reservations.ViewQueue)]` |
| `CheckIn` | replace `[Authorize(Policy = "ReceptionOrAdmin")]` → `[RequirePermission(Permissions.Reservations.CheckIn)]` |
| `StartConsultation` | replace `[Authorize(Policy = "DoctorOrAdmin")]` → `[RequirePermission(Permissions.Reservations.StartConsultation)]` |

**3m. `RolesController` and `UsersController`.** On each class, replace `[Authorize(Policy = "AdminOnly")]` with `[RequirePermission(Permissions.Users.Manage)]`.

### Step 4 — Checks inside actions

**4a. `PaymentsController.Report`.** Replace:

```csharp
        var todayOnly = !IsAdmin && !IsDoctor;
```

with:

```csharp
        var todayOnly = !await CanAsync(Permissions.Payments.ReportAnyDate);
        var ownPaymentsOnly = !await CanAsync(Permissions.Payments.ReportAllDoctors);
```

Then replace:

```csharp
        if (!IsAdmin && IsDoctor && CurrentUserId <= 0) return Forbid();
```

with:

```csharp
        if (ownPaymentsOnly && CurrentUserId <= 0) return Forbid();
```

and replace:

```csharp
            fromUtc, toUtc, !IsAdmin && IsDoctor ? CurrentUserId : null);
```

with:

```csharp
            fromUtc, toUtc, ownPaymentsOnly ? CurrentUserId : null);
```

**4b. `PaymentsController.Dashboard`.** Replace these lines:

```csharp
        var result = await _paymentService.GetPaymentDashboardAsync(IsAdmin ? from : null, IsAdmin ? to : null, unpaidOnly: !IsAdmin);
```

```csharp
        return View(IsAdmin ? "Dashboard" : "Unpaid", result.IsSuccess ? result.Data : new PaymentDashboardDto());
```

with, respectively:

```csharp
        var fullDashboard = await CanAsync(Permissions.Payments.ViewFullDashboard);
        var result = await _paymentService.GetPaymentDashboardAsync(fullDashboard ? from : null, fullDashboard ? to : null, unpaidOnly: !fullDashboard);
```

```csharp
        return View(fullDashboard ? "Dashboard" : "Unpaid", result.IsSuccess ? result.Data : new PaymentDashboardDto());
```

**4c. `FollowUpsController`.** Replace:

```csharp
        var canViewAll = IsAdmin || IsReception;
```

with:

```csharp
        var canViewAll = await CanAsync(Permissions.FollowUps.ViewAll);
```

Then, in this file only, replace **all 3** occurrences of `IsAdmin || IsReception);` with `await CanAsync(Permissions.FollowUps.ViewAll));`. In Visual Studio: Ctrl+H, scope *Current document*, *Replace all*.

**4d. `MedicalRecordsController`.** In this file only, replace **all 5** occurrences of `IsAdmin);` with `await CanAsync(Permissions.Consultations.ActOnBehalf));`. Then replace:

```csharp
            if (IsAdmin || IsReception)
```

with:

```csharp
            if (await CanAsync(Permissions.Payments.Collect))
```

**4e. `ReservationsController`.** Make these four replacements:

| Find | Replace with |
|---|---|
| `var canViewAllQueues = IsAdmin \|\| IsReception;` | `var canViewAllQueues = await CanAsync(Permissions.Reservations.ViewAllQueues);` |
| `if ((IsAdmin \|\| IsReception) && result.Data is { } reservation)` | `if (await CanAsync(Permissions.Payments.Collect) && result.Data is { } reservation)` |
| `if (result.Data is { StatusName: "Completed" } r && (IsAdmin \|\| IsReception))` | `if (result.Data is { StatusName: "Completed" } r && await CanAsync(Permissions.Payments.Collect))` |
| `IsAdmin);` (the only one, in `StartConsultation`) | `await CanAsync(Permissions.Consultations.ActOnBehalf));` |

### Step 5 — Remove the role policies

**5a.** In `Program.cs`, replace:

```csharp
// ── Authorization ────────────────────────────────────────────
// Role-based policies until F4b-2 moves every controller to [RequirePermission].
builder.Services.AddAuthorization(options =>
{
    foreach (var (name, roles) in LegacyRolePolicies.All)
        options.AddPolicy(name, policy => policy.RequireRole(roles));
});
```

with:

```csharp
// ── Authorization ────────────────────────────────────────────
builder.Services.AddAuthorization();
```

**5b.** Delete the file:

```powershell
git rm SafyaClinic/SafyaClinic.Web/Security/LegacyRolePolicies.cs
```

### Step 6 — Reservation rules use permissions (D4, D6)

**6a.** Replace the whole content of `SafyaClinic/SafyaClinic.Application/Services/ReservationRules.cs` with:

```csharp
using SafyaClinic.Application.Security;

namespace SafyaClinic.Application.Services;

/// <summary>
/// Reservation rules (S7; decisions D4 and D6), decided by the acting user's permissions (F4b).
/// Permissions are read from the database by the caller, never from the browser.
/// </summary>
public static class ReservationRules
{
    public const string CompletedStatus = "Completed";

    /// <summary>D4: a price different from the treatment type's needs Reservations.OverridePrice.</summary>
    public static bool CanOverridePrice(IReadOnlySet<string> permissions) =>
        permissions.Contains(Permissions.Reservations.OverridePrice);

    /// <summary>Returns a message for the user, or null when the status change is allowed.</summary>
    public static string? CheckStatusChange(
        IReadOnlySet<string> permissions, int actingUserId, int reservationDoctorId, string targetStatus)
    {
        // D4: doctors complete a visit from the consultation screen, which records it properly.
        if (targetStatus == CompletedStatus && !permissions.Contains(Permissions.Reservations.SetCompleted))
            return "Only an administrator can set a visit to Completed here. The doctor completes it from the consultation screen.";

        // D6: without Reservations.ChangeAnyDoctorStatus, only one's own reservations.
        if (reservationDoctorId != actingUserId && !permissions.Contains(Permissions.Reservations.ChangeAnyDoctorStatus))
            return "You cannot change the status of another doctor's reservation.";

        return null;
    }
}
```

**6b.** `SafyaClinic/SafyaClinic.Application/Services/ReservationService.cs`:

- Add with the other `using` lines (if it isn't there yet):

  ```csharp
  using SafyaClinic.Application.Security;
  ```

- Replace the fields, constructor and `RoleNamesAsync` helper:

  ```csharp
      private readonly IUnitOfWork _uow;
      private readonly IClinicClock _clock;

      public ReservationService(IUnitOfWork uow, IClinicClock? clock = null)
      {
          _uow = uow;
          _clock = clock ?? ClinicClock.Cairo;
      }
      // ── S7 rule helpers ──────────────────────────────────────
      private Task<List<string>> RoleNamesAsync(int userId) =>
          _uow.UserRoles
              .Query()
              .Where(ur => ur.UserId == userId)
              .Select(ur => ur.Role.RoleName)
              .ToListAsync();
  ```

  with:

  ```csharp
      private readonly IUnitOfWork _uow;
      private readonly IPermissionStore _permissions;
      private readonly IClinicClock _clock;

      public ReservationService(IUnitOfWork uow, IPermissionStore permissions, IClinicClock? clock = null)
      {
          _uow = uow;
          _permissions = permissions;
          _clock = clock ?? ClinicClock.Cairo;
      }
      // ── S7 rule helpers (F4b: permissions) ───────────────────
      private Task<IReadOnlySet<string>> PermissionsAsync(int userId) =>
          _permissions.GetPermissionsAsync(userId);
  ```

- Replace the 3 uses of `await RoleNamesAsync(` with `await PermissionsAsync(`: one in `StatusChangeProblemAsync`, one in `CreateReservationAsync`, one in the update method. In Visual Studio: Ctrl+H, current document, *Replace all*.

**Check:** `dotnet build SafyaClinic\SafyaClinic.sln` gives 0 errors, apart from the tests, which Step 7 fixes. If a controller shows `The name 'IsAdmin' does not exist` or `'IsReception'`, a check from Step 4 was missed in that file; send me the line if it isn't one listed above.

### Step 7 — Tests

**7a.** Create `SafyaClinic/tests/SafyaClinic.Tests/Web/Security/RolePermissions.cs`:

```csharp
using SafyaClinic.Application.Security;
using SafyaClinic.Web.Security;

namespace SafyaClinic.Tests.Web.Security;

/// <summary>Test double: the permissions a user with these roles gets from the system groups (F4a).</summary>
public sealed class RolePermissions : ICurrentPermissions
{
    private readonly HashSet<string> _codes;

    public RolePermissions(IEnumerable<string> roles) => _codes = Of(roles);

    public Task<bool> HasAsync(string code) => Task.FromResult(_codes.Contains(code));

    public static HashSet<string> Of(IEnumerable<string> roles) =>
        roles.SelectMany(role => SystemGroups.All.Single(g => g.RoleName == role).Permissions).ToHashSet();
}
```

**7b.** `PaymentsControllerAccessTests.cs`:

- Add with the other `using` lines:

  ```csharp
  using Microsoft.Extensions.DependencyInjection;
  using SafyaClinic.Application.Security;
  using SafyaClinic.Tests.Web.Security;
  using SafyaClinic.Web.Security;
  ```

- In `Create`, replace:

  ```csharp
                  HttpContext = new DefaultHttpContext
                  {
                      User = new ClaimsPrincipal(new ClaimsIdentity(claims, "tests"))
                  }
  ```

  with:

  ```csharp
                  HttpContext = new DefaultHttpContext
                  {
                      User = new ClaimsPrincipal(new ClaimsIdentity(claims, "tests")),
                      RequestServices = new ServiceCollection()
                          .AddSingleton<ICurrentPermissions>(new RolePermissions(roles))
                          .BuildServiceProvider()
                  }
  ```

- `View(...)` reads TempData from the request's services once they exist, so give each test controller an empty TempData. Add `using Microsoft.AspNetCore.Mvc.ViewFeatures;`, then add this class just above `Create`:

  ```csharp
      // The controller calls View(), which reads TempData; nothing is stored in these tests.
      private sealed class NoTempDataProvider : ITempDataProvider
      {
          public IDictionary<string, object> LoadTempData(HttpContext context) => new Dictionary<string, object>();
          public void SaveTempData(HttpContext context, IDictionary<string, object> values) { }
      }
  ```

  and, in `Create`, directly above `return (controller, (PaymentServiceSpy)(object)service);`, add:

  ```csharp
          controller.TempData = new TempDataDictionary(controller.HttpContext, new NoTempDataProvider());
  ```

- Replace the last test and the `ExpectedPolicy` method (from `[Fact]` above `Every_action_declares_its_expected_payment_policy` to the end of `ExpectedPolicy`) with:

  ```csharp
      [Fact]
      public void Every_action_declares_its_expected_payment_permission()
      {
          var actions = typeof(PaymentsController)
              .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
              .ToList();

          var missing = actions
              .Where(action => !action.GetCustomAttributes<RequirePermissionAttribute>()
                  .Any(attribute => attribute.Code == ExpectedPermission(action.Name)))
              .Select(action => $"{action.Name} (expected {ExpectedPermission(action.Name)})")
              .ToList();

          Assert.NotEmpty(actions);
          Assert.Empty(missing);
      }

      private static string ExpectedPermission(string action) => action switch
      {
          "Report" => Permissions.Payments.ViewReport,
          "DashboardLineDetails" => Permissions.Payments.ViewFullDashboard,
          "RecalculateAllPaidStatuses" or "BackfillZeroCostPayments" => Permissions.Payments.Maintenance,
          "Collect" => Permissions.Payments.Collect,
          "Cancel" or "ChangeAmount" => Permissions.Payments.Adjust,
          _ => Permissions.Payments.View
      };
  ```

**7c.** `ReservationRulesTests.cs`:

- Add with the other `using` lines:

  ```csharp
  using SafyaClinic.Tests.Web.Security;
  ```

- Replace:

  ```csharp
      private static string[] Roles(params string[] roles) => roles;
  ```

  with:

  ```csharp
      // F4b: the rules take permissions; each role stands for its system group's permissions.
      private static IReadOnlySet<string> Roles(params string[] roles) => RolePermissions.Of(roles);
  ```

- Change the summary line `/// <summary>S7: D4 (price, completion) and D6 (other doctors' reservations).</summary>` to:

  ```csharp
  /// <summary>S7/F4b: D4 (price, completion) and D6 (other doctors' reservations), by role, through permissions.</summary>
  ```

**7d.** `ReservationServiceRulesTests.cs`:

- Add with the other `using` lines:

  ```csharp
  using SafyaClinic.Infrastructure.Security;
  ```

- In `Service`, replace:

  ```csharp
          return new ReservationService(new UnitOfWork(context));
  ```

  with:

  ```csharp
          return new ReservationService(new UnitOfWork(context), new PermissionStore(context));
  ```

**7e.** `AccessMatrixTests.cs`. Replace the whole `RolesFor` method with:

```csharp
    private static IEnumerable<string> RolesFor(string policy)
    {
        Assert.True(policy.StartsWith(PermissionPolicy.Prefix, StringComparison.Ordinal),
            $"'{policy}' is not a permission policy (F4b-2 removed the role policies).");
        var code = policy[PermissionPolicy.Prefix.Length..];
        return SystemGroups.All.Where(g => g.Permissions.Contains(code)).Select(g => g.RoleName);
    }
```

Then add this test at the end of the class, before its closing `}`:

```csharp

    [Fact]
    public void No_controller_uses_roles_or_named_policies()
    {
        var rules = Controllers.SelectMany(c =>
            c.GetCustomAttributes(true).OfType<IAuthorizeData>().Select(rule => (Where: c.Name, Rule: rule))
                .Concat(c.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                    .SelectMany(m => m.GetCustomAttributes(true).OfType<IAuthorizeData>()
                        .Select(rule => (Where: $"{c.Name}.{m.Name}", Rule: rule)))));

        var offenders = rules
            .Where(x => !string.IsNullOrEmpty(x.Rule.Roles)
                        || (!string.IsNullOrEmpty(x.Rule.Policy)
                            && !x.Rule.Policy.StartsWith(PermissionPolicy.Prefix, StringComparison.Ordinal)))
            .Select(x => x.Where)
            .Distinct()
            .ToList();

        Assert.Empty(offenders);
    }
```

**Do not edit `access-matrix.txt`.**

**Check:**

```powershell
dotnet build SafyaClinic\SafyaClinic.sln
dotnet test SafyaClinic\SafyaClinic.sln
git status
```

Expected:
- **Passed: 179** (178 + 1), Failed: 0.
- `git status` does **not** list `access-matrix.txt` as modified.

If `Access_matches_the_approved_baseline` fails, the message lists the lines that changed. Fix the attribute on that action using the Step 3 tables; don't change the baseline. Send me anything you can't resolve.

**7f.** Search for leftovers. This should print nothing:

```powershell
git grep -n -E 'Authorize\(Policy|Authorize\(Roles|IsAdmin|IsReception' -- SafyaClinic/SafyaClinic.Web/Controllers
```

### Step 8 — Run

```powershell
dotnet run --project SafyaClinic\SafyaClinic.Web --launch-profile http
```

### Step 9 — Manual check (one user per role)

| User | Check |
|---|---|
| **Admin** | Payments → Dashboard shows the full dashboard. The payment report allows any dates. Users, Clinics and Lookups open. A reservation's status can be set to Completed. |
| **Doctor** | Patients, Records and Lab open. Payments → Report shows only their own payments, any dates. Nutrition → Packages is refused (access denied). They can't change the status of another doctor's reservation. |
| **Nutritionist** (not Enas, who is also a Doctor) | Nutrition opens. Records and the Queue are refused. |
| **Reception** | Queue shows every doctor. Check-in works. Payments shows the Unpaid list. The report covers today only. Records are refused. |
| **mm** (ReceptionAdmin only) | Same as before (Dashboard, Unpaid payments, payment report for today). Patients is refused. |

### Step 10 — Commit, push, PR

```powershell
git add -A SafyaClinic Docs
git status
git commit -m "F4b-2: controllers and reservation rules check permissions; role policies removed"
git push -u origin chore/f4b2-controllers-permissions
```

Open the PR and send me the number and the CI result. Don't merge before review.

## Acceptance criteria

- [ ] No `[Authorize(Policy/Roles = …)]` with a role or role policy remains on any controller (test).
- [ ] `access-matrix.txt` is unchanged.
- [ ] `IsAdmin` and `IsReception` no longer exist; payments, follow-ups, records and reservations use `CanAsync`.
- [ ] D4 and D6 are decided by permissions; the existing role-based rule tests still pass.
- [ ] Manual check per role shows no change.
- [ ] 179 tests pass locally and in CI.

## Next: F4c

F4c replaces the 63 role checks in views with `Can(…)` and builds the sidebar from the permission catalogue. The Dashboard's "own day" check moves to `ClinicianType`, and with that no role-name strings remain outside the catalogue.
