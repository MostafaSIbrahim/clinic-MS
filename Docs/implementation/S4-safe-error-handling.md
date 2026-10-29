# Slice S4 — Safe error handling

Roadmap item **S4** (Group 1). Fixes F08, F16 (error-message part) and the new finding **F33**.

| | |
|---|---|
| Branch | `chore/s4-error-handling` (from an up-to-date `main`, after PR #5 is merged) |
| Behaviour change | 1. Unexpected errors show a **generic message with a reference code** (HTTP 500) instead of the raw exception text, and the text no longer sticks around for later visits. 2. The four lookup screens no longer show exception text. 3. **F33 fix:** deleting a treatment, injection or vitamin type that is still in use now *deactivates* it, as the screen's own message always promised, instead of failing |
| Size | S |

## Background

- `ExceptionHandlingMiddleware` stores `ex.Message` in the session and redirects to `/Home/Error`, which shows it to the user; nothing clears it (F08, verified at runtime).
- `LookupsController` writes `ex.Message` into `TempData["Error"]` in six places.
- **F33 (new, verified in code):** the delete actions check the wrong table before deleting:

| Action | Checks today (wrong) | Should check |
|---|---|---|
| `DeleteTreatmentType` | `Treatments` whose **Id** equals the type id | `Reservations.TreatmentTypeId` |
| `DeleteInjectionType` | `TreatmentTypes` whose **Id** equals the injection id | `PackageItems.InjectionId` |
| `DeleteVitaminType` | `Treatments` whose **Id** equals the vitamin id | `PackageItems.VitaminId` |

Result today: deleting a type that is in use usually fails with a database foreign-key error (shown raw); deleting an *unused* type can be wrongly refused and deactivated instead.

## Scope

1. Use ASP.NET Core's built-in exception handler (outside Development) with a generic error page showing a reference code; delete the custom middleware; remove the now-unused session.
2. Console logs include the request id, so the reference code on screen matches the log line.
3. Lookups: replace exception text with a generic message + logging.
4. Fix the three in-use checks (F33).
5. Tests: error page model; the three delete rules.

Out of scope: validation of lookup field lengths (the lookup screens are rebuilt on DTOs in M5 — until then an over-long value gives the generic error page); custom 404 page.

## Affected files

| Action | Path |
|---|---|
| Edit | `SafyaClinic/SafyaClinic.Web/Program.cs` |
| Delete | `SafyaClinic/SafyaClinic.Web/Middleware/ExceptionHandlingMiddleware.cs` |
| Edit | `SafyaClinic/SafyaClinic.Web/Controllers/HomeController.cs` |
| Edit | `SafyaClinic/SafyaClinic.Web/Views/Shared/Error.cshtml` |
| Edit | `SafyaClinic/SafyaClinic.Web/Controllers/LookupsController.cs` |
| Edit | `SafyaClinic/SafyaClinic.Web/appsettings.json` (console log scopes) |
| Add | `SafyaClinic/tests/SafyaClinic.Tests/Web/HomeControllerErrorTests.cs` |
| Add | `SafyaClinic/tests/SafyaClinic.Tests/Web/Lookups/LookupsControllerDeleteTests.cs` |
| Add | `Docs/implementation/S4-safe-error-handling.md` |
| Edit | `Docs/assessment/10-evaluation-findings.md`, `12-prioritized-roadmap.md` (F33 recorded) |
| Edit | `Docs/assessment/09-risks-unknowns-questions.md` (already changed on your machine, still uncommitted) |

## Risks

| Risk | Mitigation |
|---|---|
| In Development you no longer see the friendly page | Intended: Development shows the detailed developer page; test the friendly page by running as Production (step 8) |
| Removing the session breaks something | Searched: only the error message used it; TempData uses cookies by default |
| Deactivating instead of deleting surprises an admin | That is what the existing on-screen message already says happens |

## Rollback

Revert the PR's merge commit.

---

## Steps

PowerShell, from `D:\MyDevWork\clinic-MS`.

### Step 0 — Branch

```powershell
git switch main
git pull
git switch -c chore/s4-error-handling
```

### Step 1 — Pipeline (`Program.cs`)

**1a.** Delete the line:

```csharp
using SafyaClinic.Web.Middleware;
```

**1b.** Delete the whole session block:

```csharp
// ── Session (for flash messages / TempData) ──────────────────
builder.Services.AddSession(o =>
{
    o.IdleTimeout        = TimeSpan.FromMinutes(30);
    o.Cookie.HttpOnly    = true;
    o.Cookie.IsEssential = true;
});
```

**1c.** Delete these two lines from the pipeline:

```csharp
app.UseSession();
```

```csharp
app.UseMiddleware<ExceptionHandlingMiddleware>();
```

Also delete the two stale comment lines above the `if (!app.Environment.IsDevelopment())` block (`// FIX: ExceptionHandlingMiddleware must come BEFORE…` and `// so it catches exceptions…`).

Keep this block exactly as it is — it is now the only error handler and it already sits first in the pipeline:

```csharp
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}
```

(In Development, .NET 8 shows its detailed developer exception page automatically.)

### Step 2 — Delete the custom middleware

```powershell
git rm SafyaClinic/SafyaClinic.Web/Middleware/ExceptionHandlingMiddleware.cs
```

### Step 3 — Error action (`HomeController.cs`)

**3a.** Replace the `using` lines at the top of the file:

```csharp
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
```

with:

```csharp
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SafyaClinic.Web.Models;
```

**3b.** Replace the whole `Error` action:

```csharp
    public IActionResult Error()
    {
        var msg = HttpContext.Session.GetString("GlobalError") ?? "An unexpected error occurred.";
        ViewBag.ErrorMessage = msg;
        return View();
    }
```

with:

```csharp
    // Shown by UseExceptionHandler. Never displays exception details; the
    // reference matches the RequestId in the server log for this request.
    [AllowAnonymous]
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = HttpContext.TraceIdentifier });
    }
```

### Step 4 — Error view (`Views/Shared/Error.cshtml`)

Replace the whole file with:

```cshtml
@model SafyaClinic.Web.Models.ErrorViewModel
@{
    ViewData["Title"] = "Something went wrong";
}

<div class="text-center py-5">
    <i class="bi bi-exclamation-octagon text-danger fs-1" aria-hidden="true"></i>
    <h1 class="h3 mt-3">Something went wrong</h1>
    <p class="text-muted mb-1">The request could not be completed. Please try again.</p>
    @if (Model?.ShowRequestId == true)
    {
        <p class="text-muted">If it keeps happening, give this reference to the administrator: <code>@Model.RequestId</code></p>
    }
    <a href="/Dashboard" class="btn btn-primary mt-2">
        <i class="bi bi-house me-1" aria-hidden="true"></i>Back to Dashboard
    </a>
</div>
```

### Step 5 — Lookups: no exception text, correct in-use checks

File: `SafyaClinic/SafyaClinic.Web/Controllers/LookupsController.cs`.

**5a.** Add at the top with the other `using` lines:

```csharp
using Microsoft.Extensions.Logging;
```

**5b.** Replace the field and constructor:

```csharp
        private readonly SafyaDbContext _context;

        public LookupsController(SafyaDbContext context)
        {
            _context = context;
        }
```

with:

```csharp
        private readonly SafyaDbContext _context;
        private readonly ILogger<LookupsController> _logger;

        public LookupsController(SafyaDbContext context, ILogger<LookupsController> logger)
        {
            _context = context;
            _logger = logger;
        }

        // Logs the full exception; shows the user only a generic message with a reference.
        private string LogFailure(Exception ex, string action)
        {
            _logger.LogError(ex, "Lookups: could not {Action}", action);
            return $"Could not {action}. If this keeps happening, give the administrator this reference: {HttpContext.TraceIdentifier}";
        }
```

**5c.** Replace each of the six exception messages (search for `ex.Message`):

| Find | Replace with |
|---|---|
| `TempData["Error"] = $"An error occurred while saving: {ex.Message}";` (in `SaveTreatmentType`) | `TempData["Error"] = LogFailure(ex, "save the treatment type");` |
| `TempData["Error"] = $"Error deleting treatment type: {ex.Message}";` | `TempData["Error"] = LogFailure(ex, "delete the treatment type");` |
| `TempData["Error"] = $"An error occurred while saving: {ex.Message}";` (in `SaveInjectionType`) | `TempData["Error"] = LogFailure(ex, "save the injection type");` |
| `TempData["Error"] = $"Error deleting injection type: {ex.Message}";` | `TempData["Error"] = LogFailure(ex, "delete the injection type");` |
| `TempData["Error"] = $"An error occurred while saving: {ex.Message}";` (in `SaveVitaminType`) | `TempData["Error"] = LogFailure(ex, "save the vitamin type");` |
| `TempData["Error"] = $"Error deleting vitamin type: {ex.Message}";` | `TempData["Error"] = LogFailure(ex, "delete the vitamin type");` |

**Check:** searching the file for `ex.Message` finds nothing.

**5d.** Fix the in-use checks (F33):

| In | Replace | With |
|---|---|---|
| `DeleteTreatmentType` | `bool isUsed = await _context.Treatments.AnyAsync(t => t.Id == id);` | `bool isUsed = await _context.Reservations.AnyAsync(r => r.TreatmentTypeId == id);` |
| `DeleteInjectionType` | `bool isUsed = await _context.TreatmentTypes.AnyAsync(i => i.Id == id);` | `bool isUsed = await _context.PackageItems.AnyAsync(p => p.InjectionId == id);` |
| `DeleteVitaminType` | `bool isUsed = await _context.Treatments.AnyAsync(v => v.Id == id);` | `bool isUsed = await _context.PackageItems.AnyAsync(p => p.VitaminId == id);` |

### Step 6 — Log the request id

In `SafyaClinic/SafyaClinic.Web/appsettings.json`, inside `"Logging"`, add a `Console` section after `"LogLevel": { … }` (mind the comma after the closing brace of `LogLevel`):

```json
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning",
      "Microsoft.EntityFrameworkCore.Database.Command": "Warning"
    },
    "Console": {
      "IncludeScopes": true
    }
  },
```

Each logged error then carries `RequestId:…`, the same value the error page shows.

### Step 7 — Tests

**7a.** `SafyaClinic/tests/SafyaClinic.Tests/Web/HomeControllerErrorTests.cs`

```csharp
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SafyaClinic.Web.Controllers;
using SafyaClinic.Web.Models;
using Xunit;

namespace SafyaClinic.Tests.Web;

public class HomeControllerErrorTests
{
    [Fact]
    public void Error_page_shows_only_a_reference_code()
    {
        var controller = new HomeController
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { TraceIdentifier = "REF-123" }
            }
        };

        var view = Assert.IsType<ViewResult>(controller.Error());

        var model = Assert.IsType<ErrorViewModel>(view.Model);
        Assert.Equal("REF-123", model.RequestId);
        Assert.True(model.ShowRequestId);
    }
}
```

**7b.** `SafyaClinic/tests/SafyaClinic.Tests/Web/Lookups/LookupsControllerDeleteTests.cs`

```csharp
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using SafyaClinic.Domain.Entities.MedicalRecord;
using SafyaClinic.Domain.Entities.Nutrition;
using SafyaClinic.Domain.Entities.Reservation;
using SafyaClinic.Infrastructure.Data;
using SafyaClinic.Web.Controllers;
using Xunit;

namespace SafyaClinic.Tests.Web.Lookups;

/// <summary>F33: "in use" means referenced by reservations / package items.</summary>
public class LookupsControllerDeleteTests
{
    private sealed class NoTempDataProvider : ITempDataProvider
    {
        public IDictionary<string, object> LoadTempData(HttpContext context) => new Dictionary<string, object>();
        public void SaveTempData(HttpContext context, IDictionary<string, object> values) { }
    }

    private static SafyaDbContext NewContext() =>
        new(new DbContextOptionsBuilder<SafyaDbContext>()
            .UseInMemoryDatabase($"lookups-{Guid.NewGuid()}")
            .Options);

    private static LookupsController NewController(SafyaDbContext context)
    {
        var httpContext = new DefaultHttpContext();
        return new LookupsController(context, NullLogger<LookupsController>.Instance)
        {
            ControllerContext = new ControllerContext { HttpContext = httpContext },
            TempData = new TempDataDictionary(httpContext, new NoTempDataProvider())
        };
    }

    [Fact]
    public async Task Treatment_type_used_by_a_reservation_is_deactivated_not_deleted()
    {
        await using var context = NewContext();
        var type = new TreatmentType { TypeName = "Consultation" };
        context.TreatmentTypes.Add(type);
        await context.SaveChangesAsync();
        context.Reservations.Add(new Reservation { TreatmentTypeId = type.Id, PatientId = 1, DoctorId = 1, ClinicId = 1, StatusId = 1 });
        await context.SaveChangesAsync();

        await NewController(context).DeleteTreatmentType(type.Id);

        var stored = await context.TreatmentTypes.AsNoTracking().SingleAsync();
        Assert.False(stored.IsActive);
    }

    [Fact]
    public async Task Unused_treatment_type_is_deleted()
    {
        await using var context = NewContext();
        var type = new TreatmentType { TypeName = "Unused" };
        context.TreatmentTypes.Add(type);
        await context.SaveChangesAsync();

        await NewController(context).DeleteTreatmentType(type.Id);

        Assert.False(await context.TreatmentTypes.AnyAsync());
    }

    [Fact]
    public async Task Injection_type_used_by_a_package_is_deactivated_not_deleted()
    {
        await using var context = NewContext();
        var injection = new InjectionType { InjectionName = "B12", Unit = "mcg" };
        context.InjectionTypes.Add(injection);
        await context.SaveChangesAsync();
        context.PackageItems.Add(new PackageItem { PackageId = 1, InjectionId = injection.Id, WeekNumber = 1 });
        await context.SaveChangesAsync();

        await NewController(context).DeleteInjectionType(injection.Id);

        Assert.False((await context.InjectionTypes.AsNoTracking().SingleAsync()).IsActive);
    }

    [Fact]
    public async Task Vitamin_type_used_by_a_package_is_deactivated_not_deleted()
    {
        await using var context = NewContext();
        var vitamin = new VitaminType { VitaminName = "Vitamin C", Unit = "mg" };
        context.VitaminTypes.Add(vitamin);
        await context.SaveChangesAsync();
        context.PackageItems.Add(new PackageItem { PackageId = 1, VitaminId = vitamin.Id, WeekNumber = 1 });
        await context.SaveChangesAsync();

        await NewController(context).DeleteVitaminType(vitamin.Id);

        Assert.False((await context.VitaminTypes.AsNoTracking().SingleAsync()).IsActive);
    }
}
```

**Check:**

```powershell
dotnet build SafyaClinic\SafyaClinic.sln
dotnet test SafyaClinic\SafyaClinic.sln
```

Expected **Passed: 32** (27 + 5), Failed: 0. If a test fails, send me the output — do not change the tests to make them pass.

### Step 8 — Manual check of the friendly error page

The friendly page only appears outside Development, so run once as Production:

```powershell
$env:ASPNETCORE_ENVIRONMENT = "Production"
dotnet run --project SafyaClinic\SafyaClinic.Web --no-launch-profile --urls http://localhost:5070
```

1. **Error page.** Sign in, open **System Lookups → Analysis Types**, paste more than 500 characters into **Description** (any name), and save.
   **Expected:** the "Something went wrong" page with a reference code and no database text; the PowerShell window logs the error with the same `RequestId`.
   (Do not use the Name field for this: that form never sends the name — see F34 — so nothing fails.)
2. **Lookup message.** Open **Treatment Types**, add a type whose **name** is longer than 100 characters, and save.
   **Expected:** you return to the list with a red message "Could not save the treatment type… reference: …", no database text; the same reference appears in the log.
3. **No stale message.** Open `http://localhost:5070/Home/Error` directly. **Expected:** the same generic page with a new reference code and nothing from the earlier error. (This page *is* the error page, so seeing "Something went wrong" here is correct.)
4. Stop with **Ctrl+C**, then reset the environment for normal work:

```powershell
Remove-Item Env:ASPNETCORE_ENVIRONMENT
```

Then do the usual Development smoke test (`--launch-profile http`), sign in, open the dashboard.

### Step 9 — Commit, push, PR

```powershell
git add SafyaClinic/SafyaClinic.Web/Program.cs SafyaClinic/SafyaClinic.Web/Controllers/HomeController.cs SafyaClinic/SafyaClinic.Web/Controllers/LookupsController.cs
git add SafyaClinic/SafyaClinic.Web/Views/Shared/Error.cshtml SafyaClinic/SafyaClinic.Web/appsettings.json
git add SafyaClinic/tests/SafyaClinic.Tests Docs/implementation Docs/assessment
git status
git commit -m "S4: built-in exception handler with reference code; no exception text in UI; fix lookup in-use checks (F33)"
git push -u origin chore/s4-error-handling
```

`git status`: the files above plus the deleted middleware; `SafyaClinic.Web.csproj.user` stays unstaged. Open the PR, send me the number and CI result.

## Acceptance criteria

- [ ] Outside Development, an unhandled error shows the generic page with a reference code, returns HTTP 500 and is logged with the same request id.
- [ ] No exception text appears in any UI (`git grep -n "ex.Message" -- SafyaClinic/SafyaClinic.Web` returns nothing).
- [ ] `/Home/Error` opened later shows no stale message; `ExceptionHandlingMiddleware` and the session are gone.
- [ ] In-use treatment/injection/vitamin types are deactivated, unused ones deleted (tests).
- [ ] 32 tests pass locally and in CI; Development smoke test passes.
