# Slice F4c — Views check permissions; the dashboard uses the clinician type

Roadmap item **F4**, slice c (Group 2). Builds on F4b-2: controllers already check permissions.

| | |
|---|---|
| Branch | `chore/f4c-views-permissions` (from an up-to-date `main`, after PR #21 is merged) |
| Behaviour change | **None for users** apart from one sign-out (see the next row). 1. The **63 role checks in 15 views** become permission checks. They decide which buttons, menu items and links are shown, and every screen keeps exactly the same buttons and links for each role. 2. The dashboard's "a clinician sees their own day" uses the user's **clinician type** (F4a) instead of the Doctor/Nutritionist roles. After this slice **no code checks a role name** |
| One sign-out | The clinician type is stored in the sign-in cookie, so sessions started before this slice end once and users sign in again (as after S6) |
| Database | None |
| Size | M (many small view edits) |

## Design

| Decision | Choice |
|---|---|
| Views | `_ViewImports` injects the current user's permissions as `Can`. A check reads `@if (await Can.HasAsync(Permissions.Records.Lock))`. The permissions are loaded once per request (F4b), so many checks on a page cost nothing extra |
| Same buttons per role | Each check gets the permission with the **same role set** as the role check it replaces (table in the appendix). For example, "Admin or Reception" on a payment button becomes `Payments.Collect`, which only Admin and Reception have among the users who can open that page |
| Sidebar | Keeps today's structure, with each item gated by its permission. The Administration menu shows each link only with its own permission. Building the sidebar from a navigation catalogue moves to **F7** (app shell), where the sidebar is redesigned anyway |
| Clinician type | Added to the sign-in cookie as a claim. A change to the clinician type follows a role change, which renews the security stamp and ends the session, so the claim is never stale. Cookies without the claim (issued before this slice) are refused once |
| Guard | A test fails if any view or controller contains `IsInRole(` again |

## Affected files

| Action | Path |
|---|---|
| Edit | `SafyaClinic/SafyaClinic.Application/DTOs/Auth/AuthDtos.cs`, `Services/AuthService.cs` |
| Edit | `SafyaClinic/SafyaClinic.Web/Security/AuthClaims.cs`, `Security/SessionValidator.cs` |
| Edit | `SafyaClinic/SafyaClinic.Web/Controllers/AuthController.cs`, `BaseController.cs`, `DashboardController.cs`, `FollowUpsController.cs` (line break) |
| Edit | `SafyaClinic/SafyaClinic.Web/Views/_ViewImports.cshtml` and 15 views: `Analysis/Details`, `Dashboard/Index`, `FollowUps/Index`, `MedicalRecords/Details`, `MedicalRecords/PrescriptionDetails`, `Nutrition/PackageDetails`, `Nutrition/Packages`, `Patients/Details`, `Payments/Report`, `Reservations/Create`, `Reservations/Details`, `Reservations/Edit`, `Reservations/Queue`, `Shared/_Layout`, `Shared/_PaymentStatusToggle` |
| Edit | `SafyaClinic/tests/SafyaClinic.Tests/Web/Security/SessionValidatorTests.cs` |
| Add | `SafyaClinic/tests/SafyaClinic.Tests/Web/Security/NoRoleChecksTests.cs` |
| Add | `Docs/implementation/F4c-views-use-permissions.md` |

## Risks

| Risk | Mitigation |
|---|---|
| A button appears for, or disappears from, a role | Each replacement uses a permission with the same role set (appendix); Step 8 checks the main screens per role |
| A view check is missed | The new test scans every view and controller for `IsInRole(` |
| Everyone is signed out once | Expected; same as S6. Only on deployment (dev now; hosted sites get a fresh deployment later) |

## Rollback

Revert the PR's merge commit. Users sign in once more.

---

## Steps

Run these in PowerShell from `D:\MyDevWork\clinic-MS`.

### Step 1 — Branch

```powershell
git switch main
git pull
git switch -c chore/f4c-views-permissions
```

### Step 2 — Clinician type in the sign-in cookie

**2a.** `SafyaClinic/SafyaClinic.Application/DTOs/Auth/AuthDtos.cs`:

- Add at the top with the other `using` lines (or as the first line if there are none):

  ```csharp
  using SafyaClinic.Domain.Enums;
  ```

- In `LoginResponse`, after `public string SecurityStamp { get; init; } = string.Empty;`, add:

  ```csharp
      public ClinicianType ClinicianType { get; init; }   // F4c: whose day the dashboard shows
  ```

**2b.** `SafyaClinic/SafyaClinic.Application/Services/AuthService.cs`. In the `new LoginResponse { … }` of `LoginAsync`, after `SecurityStamp = user.SecurityStamp,`, add:

```csharp
            ClinicianType = user.ClinicianType,
```

**2c.** `SafyaClinic/SafyaClinic.Web/Security/AuthClaims.cs`. Before the class's closing `}`, add:

```csharp

    /// <summary>The user's clinician type at sign-in (F4c); changes with a role change, which renews the stamp.</summary>
    public const string ClinicianType = "safya:clinician_type";
```

**2d.** `SafyaClinic/SafyaClinic.Web/Controllers/AuthController.cs`. In `Login`, replace:

```csharp
            new(AuthClaims.SecurityStamp, data.SecurityStamp)
        };
```

with:

```csharp
            new(AuthClaims.SecurityStamp, data.SecurityStamp),
            new(AuthClaims.ClinicianType, data.ClinicianType.ToString())
        };
```

**2e.** `SafyaClinic/SafyaClinic.Web/Security/SessionValidator.cs`. Replace:

```csharp
        if (!int.TryParse(context.Principal?.FindFirstValue(ClaimTypes.NameIdentifier), out var userId)
            || string.IsNullOrEmpty(stamp))
        {
            await RejectAsync(context);   // e.g. a cookie issued before S6
            return;
        }
```

with:

```csharp
        if (!int.TryParse(context.Principal?.FindFirstValue(ClaimTypes.NameIdentifier), out var userId)
            || string.IsNullOrEmpty(stamp)
            || context.Principal?.FindFirst(AuthClaims.ClinicianType) is null)
        {
            await RejectAsync(context);   // a cookie issued before S6 or F4c
            return;
        }
```

### Step 3 — Controllers

**3a.** `BaseController.cs`:

- Add with the other `using` lines:

  ```csharp
  using SafyaClinic.Domain.Enums;
  ```

- Replace:

  ```csharp
          // Which clinician's day the dashboard shows; moves to ClinicianType in F4c.
          protected bool IsDoctor => User.IsInRole("Doctor");
          protected bool IsNutritionist => User.IsInRole("Nutritionist");
  ```

  with:

  ```csharp
          // F4c: clinicians see their own day on the dashboard (clinician type, not roles).
          protected bool IsClinician =>
              User.FindFirstValue(AuthClaims.ClinicianType) is { } type && type != nameof(ClinicianType.None);
  ```

**3b.** `DashboardController.cs`. Replace:

```csharp
        var doctorId = (IsDoctor || IsNutritionist)
            ? CurrentUserId
            : (int?)null;
```

with:

```csharp
        var doctorId = IsClinician ? CurrentUserId : (int?)null;
```

**3c.** `FollowUpsController.cs`. A line break was lost in F4b-2. Replace:

```csharp
        var canViewAll = await CanAsync(Permissions.FollowUps.ViewAll); var doctors = new List<SelectListItem>();
```

with two lines:

```csharp
        var canViewAll = await CanAsync(Permissions.FollowUps.ViewAll);
        var doctors = new List<SelectListItem>();
```

### Step 4 — Views

**4a.** `Views/_ViewImports.cshtml`. Add at the end:

```cshtml
@using SafyaClinic.Application.Security
@using SafyaClinic.Web.Security
@inject ICurrentPermissions Can
```

**4b.** Short replacements. In each file, replace the text on the left with the text on the right. Each left-hand text occurs once in its file, unless the table gives a count.

| File | Find | Replace with |
|---|---|---|
| `Analysis/Details.cshtml` | `@if (User.IsInRole("Admin"))` | `@if (await Can.HasAsync(Permissions.Attachments.Delete))` |
| `MedicalRecords/PrescriptionDetails.cshtml` | `@if (User.IsInRole("Admin"))` | `@if (await Can.HasAsync(Permissions.Attachments.Delete))` |
| `MedicalRecords/Details.cshtml` | `@if (User.IsInRole("Admin") && !Model.IsLocked)` | `@if (await Can.HasAsync(Permissions.Records.Lock) && !Model.IsLocked)` |
| `Nutrition/Packages.cshtml` | `@if (User.IsInRole("Admin"))` | `@if (await Can.HasAsync(Permissions.Nutrition.ManagePackages))` |
| `Nutrition/PackageDetails.cshtml` | `@if (User.IsInRole("Admin"))` | `@if (await Can.HasAsync(Permissions.Nutrition.ManagePackages))` |
| `Payments/Report.cshtml` | `@if (User.IsInRole("Admin") \|\| User.IsInRole("Reception") \|\| User.IsInRole("ReceptionAdmin"))` | `@if (await Can.HasAsync(Permissions.Payments.View))` |
| `FollowUps/Index.cshtml` | `var canOpenRecord = User.IsInRole("Admin") \|\| User.IsInRole("Doctor");` | `var canOpenRecord = await Can.HasAsync(Permissions.Records.View);` |
| `Shared/_PaymentStatusToggle.cshtml` | `@if (User.IsInRole("Admin") \|\| User.IsInRole("Reception"))` | `@if (await Can.HasAsync(Permissions.Reservations.View) && await Can.HasAsync(Permissions.Payments.View))` |
| `Reservations/Create.cshtml` and `Reservations/Edit.cshtml` | `readonly="@(!User.IsInRole("Admin"))"` | `readonly="@(!canOverridePrice)"` |
| `Reservations/Create.cshtml` and `Reservations/Edit.cshtml` | `@(User.IsInRole("Admin")` (the line before `? "Auto-filled …"`) | `@(canOverridePrice` |

In `Reservations/Create.cshtml` and `Reservations/Edit.cshtml`, also add this line directly below the `ViewData["Title"] = …;` line at the top:

```cshtml
    var canOverridePrice = await Can.HasAsync(Permissions.Reservations.OverridePrice);   // D4
```

**4c.** `Dashboard/Index.cshtml`

| Find | Replace with |
|---|---|
| `@if (User.IsInRole("Admin") \|\| User.IsInRole("Reception") \|\| User.IsInRole("Doctor"))` | `@if (await Can.HasAsync(Permissions.Reservations.ViewQueue))` |
| the two lines `@(User.IsInRole("Doctor") && !User.IsInRole("Admin")` and `&& !User.IsInRole("Reception") ? "My Queue" : "Doctor Queues")` | one line: `@(await Can.HasAsync(Permissions.Reservations.ViewAllQueues) ? "Doctor Queues" : "My Queue")` |
| `@if (User.IsInRole("Admin") \|\| User.IsInRole("Reception"))` | `@if (await Can.HasAsync(Permissions.Reservations.CheckIn))` (the front-desk shortcuts) |

**4d.** `Patients/Details.cshtml`

| Find | Replace with |
|---|---|
| 1st `@if (IsDoctor() \|\| IsAdmin())` (above the **Timeline** link) | `@if (await Can.HasAsync(Permissions.Patients.ViewTimeline))` |
| 2nd `@if (IsDoctor() \|\| IsAdmin())` (above the **EditMedical** menu item) | `@if (await Can.HasAsync(Permissions.Patients.EditMedical))` |
| 3rd `@if (IsDoctor() \|\| IsAdmin())` (above **Medical Records**) | `@if (await Can.HasAsync(Permissions.Records.View))` |
| `@if (IsAdmin() \|\| IsNutritionist())` | `@if (await Can.HasAsync(Permissions.Nutrition.View))` |
| `@if (IsAdmin() \|\| User.IsInRole("Reception"))` | `@if (await Can.HasAsync(Permissions.Payments.View))` |

Then delete this block near the end of the file:

```cshtml
@functions {
    bool IsDoctor()       => ViewContext.HttpContext.User.IsInRole("Doctor");
    bool IsAdmin()        => ViewContext.HttpContext.User.IsInRole("Admin");
    bool IsNutritionist() => ViewContext.HttpContext.User.IsInRole("Nutritionist");
}
```

**4e.** `Reservations/Details.cshtml`

| Find | Replace with |
|---|---|
| `(User.IsInRole("Reception") \|\| User.IsInRole("Admin")) &&` | `await Can.HasAsync(Permissions.Reservations.CheckIn) &&` |
| `(User.IsInRole("Admin") \|\|` (in `canStartConsultation`) | `(await Can.HasAsync(Permissions.Consultations.ActOnBehalf) \|\|` |
| `(User.IsInRole("Doctor") && Model.DoctorId == currentUserId)) &&` | `(await Can.HasAsync(Permissions.Reservations.StartConsultation) && Model.DoctorId == currentUserId)) &&` |
| `disabled="@(!canCompleteFromQueue \|\| !User.IsInRole("Admin"))"` | `disabled="@(!canCompleteFromQueue \|\| !await Can.HasAsync(Permissions.Reservations.SetCompleted))"` |
| `@if (User.IsInRole("Admin") \|\| User.IsInRole("Reception"))` (Payment card) | `@if (await Can.HasAsync(Permissions.Payments.Collect))` |
| `@if (User.IsInRole("Doctor") \|\| User.IsInRole("Admin"))` (Clinical card) | `@if (await Can.HasAsync(Permissions.Records.View))` |
| `@if (hasUserId && (User.IsInRole("Admin") \|\| Model.DoctorId == currentUserId)` | `@if (hasUserId && (await Can.HasAsync(Permissions.Consultations.ActOnBehalf) \|\| Model.DoctorId == currentUserId)` |

**4f.** `Reservations/Queue.cshtml`. Replace the two lines:

```cshtml
    var canStartConsultation =
        User.IsInRole("Admin") || User.IsInRole("Doctor");
```

with:

```cshtml
    var canStartConsultation = await Can.HasAsync(Permissions.Reservations.StartConsultation);
```

**4g.** `Shared/_Layout.cshtml`

First, in the `@{ … }` block at the top, directly after the line `currentController == "Reservations" && currentAction == "Queue";`, add:

```cshtml

    // F4c: menu items follow the user's permissions.
    var canManageUsers = await Can.HasAsync(Permissions.Users.Manage);
    var canManageLookups = await Can.HasAsync(Permissions.Settings.Lookups);
    var canManagePatientSources = await Can.HasAsync(Permissions.Settings.PatientSources);
    var canManageClinics = await Can.HasAsync(Permissions.Settings.Clinics);
    var canAdminister = canManageUsers || canManageLookups || canManagePatientSources || canManageClinics;
```

Then make these replacements:

| Find | Replace with |
|---|---|
| `@if (User.IsInRole("Admin") \|\| User.IsInRole("Doctor") \|\| User.IsInRole("Nutritionist") \|\| User.IsInRole("Reception"))` | `@if (await Can.HasAsync(Permissions.Patients.View))` |
| the three lines `@if (User.IsInRole("Admin") \|\|` / `User.IsInRole("Reception") \|\|` / `User.IsInRole("Doctor"))` | one line: `@if (await Can.HasAsync(Permissions.Reservations.ViewQueue))` |
| `@(User.IsInRole("Admin") \|\| User.IsInRole("Reception")` (before `? "Doctor Queues"`) | `@(await Can.HasAsync(Permissions.Reservations.ViewAllQueues)` |
| `@if (User.IsInRole("Admin") \|\| User.IsInRole("Doctor"))` (Analysis) | `@if (await Can.HasAsync(Permissions.Analyses.View))` |
| `@if (User.IsInRole("Admin") \|\| User.IsInRole("Nutritionist"))` (Nutrition) | `@if (await Can.HasAsync(Permissions.Nutrition.View))` |
| `@if (User.IsInRole("Admin") \|\| User.IsInRole("Reception") \|\| User.IsInRole("ReceptionAdmin") \|\| User.IsInRole("Doctor"))` | `@if (await Can.HasAsync(Permissions.Payments.ViewReport))` |
| `@if (User.IsInRole("Admin") \|\| User.IsInRole("Reception") \|\| User.IsInRole("ReceptionAdmin"))` | `@if (await Can.HasAsync(Permissions.Payments.View))` |
| `@(User.IsInRole("Admin") ? "Payment Dashboard" : "Unpaid Payments")` | `@(await Can.HasAsync(Permissions.Payments.ViewFullDashboard) ? "Payment Dashboard" : "Unpaid Payments")` |

Finally, replace the Administration part: everything from `@if (User.IsInRole("Admin"))` (the one followed by `Administration`) down to and including the `}` that closes `@if (User.Identity != null && User.Identity.IsAuthenticated && User.IsInRole("Admin"))`, just before `</ul>`. Put this in its place:

```cshtml
                @if (canAdminister)
                {
                    <li class="nav-section-label" role="presentation">Administration</li>
                    <li class="nav-item dropdown">
                        <a class="nav-link dropdown-toggle @(currentController is "Lookups" or "Users" or "Roles" or "PatientSources" or "Clinics" ? "active" : "")" href="#" id="lookupsDropdown" role="button" data-bs-toggle="dropdown" aria-expanded="false">
                            <i class="bi bi-sliders me-1"></i>System Lookups
                        </a>
                        <ul class="dropdown-menu" aria-labelledby="lookupsDropdown">
                            @if (canManageLookups)
                            {
                                <li><a class="dropdown-item" asp-controller="Lookups" asp-action="Index"><i class="bi bi-grid-fill me-2"></i>All Lookups</a></li>
                            }
                            @if (canManageUsers)
                            {
                                <li><a class="dropdown-item @(currentController == "Users" ? "active" : "")" asp-controller="Users" asp-action="Index"><i class="bi bi-person-gear me-2"></i>Users</a></li>
                                <li><a class="dropdown-item @(currentController == "Roles" ? "active" : "")" asp-controller="Roles" asp-action="Index"><i class="bi bi-shield-lock me-2"></i>Roles</a></li>
                            }
                            <li><hr class="dropdown-divider"></li>
                            @if (canManageLookups)
                            {
                                <li><a class="dropdown-item" asp-controller="Lookups" asp-action="AnalysisTypes"><i class="bi bi-journal-medical me-2"></i>Analysis Types</a></li>
                                <li><a class="dropdown-item" asp-controller="Lookups" asp-action="TreatmentTypes"><i class="bi bi-bandaid me-2"></i>Treatment Types</a></li>
                                <li><a class="dropdown-item" asp-controller="Lookups" asp-action="InjectionTypes"><i class="fa-solid fa-syringe fa-lg text-warning me-2"></i>Injection Types</a></li>
                                <li><a class="dropdown-item" asp-controller="Lookups" asp-action="VitaminTypes"><i class="bi bi-capsule me-2"></i>Vitamin Types</a></li>
                            }
                            @if (canManagePatientSources)
                            {
                                <li><a class="dropdown-item" asp-controller="PatientSources" asp-action="Index"><i class="bi bi-diagram-3 me-2"></i>Patient Sources</a></li>
                            }
                            @if (canManageClinics)
                            {
                                <li><a class="dropdown-item" asp-controller="Clinics" asp-action="Index"><i class="bi bi-hospital me-2"></i>Clinics</a></li>
                            }
                        </ul>
                    </li>
                }
```

**Check:** this must print nothing:

```powershell
git grep -n "IsInRole(" -- SafyaClinic/SafyaClinic.Web
```

Then `dotnet build SafyaClinic\SafyaClinic.sln` gives 0 errors.

### Step 5 — Tests

**5a.** `SessionValidatorTests.cs`:

- Change the `Context` method's first line from:

  ```csharp
      private static CookieValidatePrincipalContext Context(StampSource source, string? stampClaim, DateTimeOffset? lastCheck = null)
  ```

  to:

  ```csharp
      private static CookieValidatePrincipalContext Context(StampSource source, string? stampClaim,
          DateTimeOffset? lastCheck = null, bool withClinicianType = true)
  ```

- After the line `if (stampClaim is not null) claims.Add(new Claim(AuthClaims.SecurityStamp, stampClaim));`, add:

  ```csharp
          if (withClinicianType) claims.Add(new Claim(AuthClaims.ClinicianType, "None"));
  ```

- Add this test before the class's closing `}`:

  ```csharp

      [Fact]
      public async Task Session_from_before_F4c_ends()
      {
          var context = Context(new StampSource { Stamp = "abc" }, "abc", withClinicianType: false);

          await SessionValidator.ValidateAsync(context);

          Assert.Null(context.Principal);
      }
  ```

**5b.** Create `SafyaClinic/tests/SafyaClinic.Tests/Web/Security/NoRoleChecksTests.cs`:

```csharp
using System.Runtime.CompilerServices;
using Xunit;

namespace SafyaClinic.Tests.Web.Security;

/// <summary>F4c: what a user may see or do comes from permissions, never from role names.</summary>
public class NoRoleChecksTests
{
    private static string WebProject([CallerFilePath] string source = "") =>
        Path.GetFullPath(Path.Combine(Path.GetDirectoryName(source)!, "..", "..", "..", "..", "SafyaClinic.Web"));

    [Fact]
    public void Views_and_controllers_do_not_check_roles()
    {
        var web = WebProject();
        var files = Directory.EnumerateFiles(Path.Combine(web, "Views"), "*.cshtml", SearchOption.AllDirectories)
            .Concat(Directory.EnumerateFiles(Path.Combine(web, "Controllers"), "*.cs", SearchOption.AllDirectories))
            .ToList();

        var offenders = files
            .Where(file => File.ReadAllText(file).Contains("IsInRole("))
            .Select(file => Path.GetRelativePath(web, file))
            .ToList();

        Assert.NotEmpty(files);
        Assert.Empty(offenders);
    }
}
```

**Check:**

```powershell
dotnet test SafyaClinic\SafyaClinic.sln
git status
```

Expected: **Passed: 181** (179 + 2), Failed: 0. `access-matrix.txt` must not be listed as modified.

### Step 6 — Run

```powershell
dotnet run --project SafyaClinic\SafyaClinic.Web --launch-profile http
```

If a browser tab was still signed in, it goes to the sign-in page once. That's expected.

### Step 7 — Manual check (one user per role)

| User | Check: everything looks exactly as before |
|---|---|
| **Admin** | Sidebar: Patients, Board, Reservations, Doctor Queues, Follow-ups, Analysis, Nutrition, Payment Dashboard, Payment Reports, Administration → System Lookups (all links). The dashboard shows all of today's reservations and the front-desk shortcuts. Reservation → New: the amount can be edited. A record that isn't locked shows Lock. |
| **Doctor** | Sidebar: Patients, Board, Reservations, **My Queue**, Follow-ups, Analysis, Payment Reports. **No** Nutrition, Payment Dashboard or Administration. The dashboard shows **only their own** reservations and has no front-desk shortcuts. Patient page: Timeline, Edit medical, Medical Records. **No** Payments button. Reservation → New: the amount is read-only. |
| **Nutritionist** (not Enas) | Sidebar: Patients, Board, Reservations, Nutrition. **No** queue, Analysis or payments. The dashboard shows only their own reservations. |
| **Reception** | Sidebar: Patients, Board, Reservations, Doctor Queues, Follow-ups, **Unpaid Payments**, Payment Reports. The dashboard shows all reservations and the front-desk shortcuts. A reservation page shows the Payment card. **No** Clinical card. |
| **mm** (ReceptionAdmin only) | Sidebar: Dashboard, Unpaid Payments, Payment Reports. The dashboard shows all of today's reservations, with **no** "Payment summary" buttons. |

### Step 8 — Commit, push, PR

```powershell
git add -A SafyaClinic Docs
git status
git commit -m "F4c: views check permissions; dashboard uses clinician type; no role checks left"
git push -u origin chore/f4c-views-permissions
```

Open the PR and send me the number and the CI result. Don't merge before review.

## Acceptance criteria

- [ ] No `IsInRole(` in any view or controller (test).
- [ ] Each role sees exactly the same menus, buttons and links as before (Step 7).
- [ ] The dashboard's own-day filter uses the clinician type; sessions from before this slice end once.
- [ ] `access-matrix.txt` unchanged; 181 tests pass locally and in CI.

## Next: F4d

F4d is the admin screen for permission groups and memberships. Admins can create a group, grant permissions (for example `Payments.WriteOff` once it exists), add users and set each user's clinician type. Role memberships move into `UserGroups`, and the Roles screen and the `Roles`/`UserRoles` tables are retired.

---

## Appendix — Same role set for every replaced view check

Users who can open the page (from `access-matrix.txt`) are given in brackets.

| View check | Was | Becomes | Shown to |
|---|---|---|---|
| Delete attachment (analysis, prescription) | Admin | `Attachments.Delete` | A |
| Lock record | Admin | `Records.Lock` | A |
| Package create / deactivate | Admin | `Nutrition.ManagePackages` | A |
| Report → Dashboard link [A D R RA] | A, R, RA | `Payments.View` | A R RA |
| Follow-ups: open record [A D R] | A, D | `Records.View` | A D |
| Payment summary toggle (dashboard: everyone) | A, R | `Reservations.View` ∧ `Payments.View` | A R |
| Amount editable (D4) | Admin | `Reservations.OverridePrice` | A |
| Dashboard queue link | A, R, D | `Reservations.ViewQueue` | A D R |
| "My Queue" label | D, not A, not R | not `Reservations.ViewAllQueues` | D |
| Dashboard front-desk shortcuts | A, R | `Reservations.CheckIn` | A R |
| Patient: Timeline / Edit medical / Medical Records [A D N R] | D, A | `Patients.ViewTimeline` / `Patients.EditMedical` / `Records.View` | A D |
| Patient: Nutrition | A, N | `Nutrition.View` | A N |
| Patient: Payments [A D N R] | A, R | `Payments.View` | A R |
| Reservation: check-in | R, A | `Reservations.CheckIn` | A R |
| Reservation: start consultation | A, or D for own | `Consultations.ActOnBehalf`, or `Reservations.StartConsultation` for own | same |
| Status option Completed | Admin | `Reservations.SetCompleted` | A |
| Reservation: Payment card [A D N R] | A, R | `Payments.Collect` | A R |
| Reservation: Clinical card | D, A | `Records.View` | A D |
| Open consultation | A, or own | `Consultations.ActOnBehalf`, or own | same |
| Queue: start consultation [A D R] | A, D | `Reservations.StartConsultation` | A D |
| Sidebar: Care section | A D N R | `Patients.View` | A D N R |
| Sidebar: Queue + Follow-ups | A R D | `Reservations.ViewQueue` | A D R |
| Sidebar: "Doctor Queues" label | A, R | `Reservations.ViewAllQueues` | A R |
| Sidebar: Analysis | A, D | `Analyses.View` | A D |
| Sidebar: Nutrition | A, N | `Nutrition.View` | A N |
| Sidebar: Billing section | A R RA D | `Payments.ViewReport` | A D R RA |
| Sidebar: payment dashboard link | A R RA | `Payments.View` | A R RA |
| "Payment Dashboard" label | Admin | `Payments.ViewFullDashboard` | A |
| Sidebar: Administration | Admin | any of `Users.Manage`, `Settings.*` | A |
| Dashboard: own day | Doctor or Nutritionist role | clinician type ≠ None | same users (F4a filled it from those roles) |
