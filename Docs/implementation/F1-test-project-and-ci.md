# Slice F1 — Test project and CI

Roadmap item **F1** (`Docs/assessment/12-prioritized-roadmap.md`, Group 2), done first so every later slice adds tests. Fixes F22 and R41 (part).

| | |
|---|---|
| Branch | `chore/f1-tests-ci` (from an up-to-date `main`) |
| Behaviour change | **None** in the application. Only tests, build settings and a CI workflow are added |
| Size | M, split into two parts: **F1a (this guide)** — test project, ported checks, SDK pin, CI. **F1b (later, with the first slice that needs it)** — SQL Server–backed service-test fixture |

## Scope

In scope:
1. Pin the .NET SDK with `global.json`.
2. Create `SafyaClinic.Tests` (xUnit, net8.0) and add it to the solution.
3. Port the 30 console checks in `tests/PaymentAccessChecks` to xUnit tests, then delete the console project.
4. Add characterization tests for the current password helpers (baseline for slice S5).
5. Run the existing Node test in CI.
6. Add a GitHub Actions workflow: restore, build, .NET tests, JavaScript tests, vulnerable-package report.
7. Align the `dotnet-ef` tool with EF Core 8 (optional step; see step 7).

Out of scope: database-backed tests (F1b), code coverage targets, any change to application code, `tests/DeploymentChecks` (stays as is; it is used in slice S8).

## Affected files

| Action | Path |
|---|---|
| Add | `global.json` (repository root) |
| Add | `SafyaClinic/tests/SafyaClinic.Tests/SafyaClinic.Tests.csproj` |
| Add | `SafyaClinic/tests/SafyaClinic.Tests/Web/Payments/PaymentServiceSpy.cs` |
| Add | `SafyaClinic/tests/SafyaClinic.Tests/Web/Payments/PaymentsControllerAccessTests.cs` |
| Add | `SafyaClinic/tests/SafyaClinic.Tests/Application/AuthServicePasswordTests.cs` |
| Edit | `SafyaClinic/SafyaClinic.sln` (via `dotnet sln add`, not by hand) |
| Delete | `SafyaClinic/tests/PaymentAccessChecks/` (2 files) |
| Edit | `.gitignore` (root) — add `TestResults/` |
| Add | `.github/workflows/ci.yml` |
| Edit (optional) | `SafyaClinic/SafyaClinic.Web/dotnet-tools.json` |

## Risks

| Risk | Mitigation |
|---|---|
| CI runs on Linux; something in the build might be Windows-only | The build has no Windows-specific code found in the review; if the first CI run fails, send me the log — the fix belongs in this slice |
| Running net8.0 tests needs the .NET 8 runtime as well as the SDK | The workflow installs both 8.0.x and 10.0.x |
| Time-dependent tests (today in Cairo, the 2026 DST day) | Ported exactly as they passed in the console version; the "today" test compares two values computed within milliseconds |
| The vulnerable-package step would fail until slice S2 | It runs as **report-only** (`continue-on-error`) until S2 switches it to blocking |
| GitHub Actions minutes on a private repo | One job of a few minutes per push/PR; within the free allowance |

## Rollback

Revert the merge commit of the PR (`git revert -m 1 <merge-commit>`). Nothing in the application depends on these files. To restore the old console checks, they remain in git history at `SafyaClinic/tests/PaymentAccessChecks/`.

---

## Steps

Run all commands in **PowerShell** from `D:\MyDevWork\clinic-MS` unless stated otherwise.

### Step 0 — Start from an up-to-date `main`

1. On GitHub, open a pull request from `docs/assessment-phase-1-2` into `main` and merge it (documentation only).
2. Then:
   ```powershell
   git switch main
   git pull
   git switch -c chore/f1-tests-ci
   ```
3. If you have not done it yet, unblock the tool manifest (Windows marked it as downloaded):
   ```powershell
   Unblock-File SafyaClinic\SafyaClinic.Web\dotnet-tools.json
   ```

**Check:** `git status` shows branch `chore/f1-tests-ci` and only the usual `SafyaClinic.Web.csproj.user` change.

### Step 1 — Pin the SDK

Create `global.json` in the repository root (next to `.gitignore`):

```json
{
  "sdk": {
    "version": "10.0.301",
    "rollForward": "latestFeature"
  }
}
```

The projects still target .NET 8; this only fixes which SDK builds them (any installed 10.0 SDK at or above 10.0.301).

**Check:** `dotnet --version` run from the repo root prints `10.0.301` or `10.0.401`.

### Step 2 — Create the test project

Create the folder `SafyaClinic\tests\SafyaClinic.Tests` and the four files below **exactly**.

**`SafyaClinic/tests/SafyaClinic.Tests/SafyaClinic.Tests.csproj`**

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <IsPackable>false</IsPackable>
    <IsTestProject>true</IsTestProject>
  </PropertyGroup>

  <ItemGroup>
    <FrameworkReference Include="Microsoft.AspNetCore.App" />
  </ItemGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.11.1" />
    <PackageReference Include="xunit" Version="2.9.2" />
    <PackageReference Include="xunit.runner.visualstudio" Version="2.8.2">
      <PrivateAssets>all</PrivateAssets>
      <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
    </PackageReference>
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\..\SafyaClinic.Web\SafyaClinic.Web.csproj" />
  </ItemGroup>

</Project>
```

**`SafyaClinic/tests/SafyaClinic.Tests/Web/Payments/PaymentServiceSpy.cs`**

```csharp
using System.Reflection;
using SafyaClinic.Application.DTOs.Common;
using SafyaClinic.Application.DTOs.Payment;

namespace SafyaClinic.Tests.Web.Payments;

/// <summary>
/// Test double for IPaymentService: records every call and returns empty
/// successful results, so controller rules can be tested without a database.
/// </summary>
public class PaymentServiceSpy : DispatchProxy
{
    public List<(string Name, object?[] Args)> Calls { get; } = new();

    protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
    {
        Calls.Add((targetMethod!.Name, args ?? Array.Empty<object?>()));

        return targetMethod.Name switch
        {
            "GetPaymentsByDateRangeAsync" =>
                Task.FromResult(ServiceResult<IEnumerable<PaymentDto>>.Success(Array.Empty<PaymentDto>())),
            "GetPaymentDashboardAsync" =>
                Task.FromResult(ServiceResult<PaymentDashboardDto>.Success(new PaymentDashboardDto())),
            _ => throw new NotSupportedException(targetMethod.Name)
        };
    }
}
```

**`SafyaClinic/tests/SafyaClinic.Tests/Web/Payments/PaymentsControllerAccessTests.cs`**

```csharp
using System.Reflection;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SafyaClinic.Application.Interfaces.Services;
using SafyaClinic.Web.Controllers;
using Xunit;

namespace SafyaClinic.Tests.Web.Payments;

/// <summary>
/// Ported from tests/PaymentAccessChecks (30 console checks, 2026-09).
/// Server-derived report scopes and action authorization for PaymentsController.
/// </summary>
public class PaymentsControllerAccessTests
{
    private static readonly TimeZoneInfo Cairo = TimeZoneInfo.FindSystemTimeZoneById("Africa/Cairo");

    private static (PaymentsController Controller, PaymentServiceSpy Spy) Create(params string[] roles)
    {
        var service = DispatchProxy.Create<IPaymentService, PaymentServiceSpy>();
        var claims = roles
            .Select(role => new Claim(ClaimTypes.Role, role))
            .Append(new Claim(ClaimTypes.NameIdentifier, "42"));

        var controller = new PaymentsController(service, null!, null!)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(claims, "tests"))
                }
            }
        };

        return (controller, (PaymentServiceSpy)(object)service);
    }

    [Theory]
    [InlineData("Reception")]
    [InlineData("ReceptionAdmin")]
    [InlineData("Admin")]
    [InlineData("Doctor")]
    public async Task Report_without_dates_does_not_query(string role)
    {
        var (controller, spy) = Create(role);

        await controller.Report(null, null);

        Assert.Empty(spy.Calls);
    }

    [Theory]
    [InlineData("Reception")]
    [InlineData("ReceptionAdmin")]
    public async Task Front_desk_report_is_restricted_to_today(string role)
    {
        var (controller, spy) = Create(role);

        await controller.Report(new DateTime(2000, 1, 1), new DateTime(2099, 12, 31));

        var args = Assert.Single(spy.Calls).Args;
        var today = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, Cairo).Date;
        Assert.Equal(today, TimeZoneInfo.ConvertTimeFromUtc((DateTime)args[0]!, Cairo).Date);
        Assert.Equal(today.AddDays(1), TimeZoneInfo.ConvertTimeFromUtc((DateTime)args[1]!, Cairo).Date);
    }

    [Theory]
    [InlineData("Reception")]
    [InlineData("ReceptionAdmin")]
    public async Task Front_desk_dashboard_is_unpaid_only(string role)
    {
        var (controller, spy) = Create(role);

        var view = Assert.IsType<ViewResult>(await controller.Dashboard(null, null));

        Assert.Equal("Unpaid", view.ViewName);
        Assert.True((bool)spy.Calls.Last().Args[2]!);
    }

    [Theory]
    [InlineData("Admin", null)]
    [InlineData("Doctor", 42)]
    public async Task Report_is_scoped_by_role(string role, int? expectedDoctorId)
    {
        var (controller, spy) = Create(role);

        await controller.Report(new DateTime(2026, 9, 2), new DateTime(2026, 9, 3));

        var args = Assert.Single(spy.Calls).Args;
        Assert.Equal(expectedDoctorId, (int?)args[2]);
    }

    [Theory]
    [InlineData("Admin")]
    [InlineData("Doctor")]
    public async Task Report_includes_the_entire_final_date(string role)
    {
        var (controller, spy) = Create(role);

        await controller.Report(new DateTime(2026, 9, 2), new DateTime(2026, 9, 3));

        var args = Assert.Single(spy.Calls).Args;
        Assert.Equal(new DateTime(2026, 9, 4), TimeZoneInfo.ConvertTimeFromUtc((DateTime)args[1]!, Cairo));
    }

    [Fact]
    public async Task Admin_role_takes_precedence_in_report()
    {
        var (controller, spy) = Create("Admin", "Doctor", "Reception");

        await controller.Report(new DateTime(2026, 1, 1), new DateTime(2026, 1, 2));

        Assert.Null(Assert.Single(spy.Calls).Args[2]);
    }

    [Fact]
    public async Task Admin_gets_full_dashboard()
    {
        var (controller, spy) = Create("Admin", "Doctor", "Reception");

        var view = Assert.IsType<ViewResult>(await controller.Dashboard(null, null));

        Assert.Equal("Dashboard", view.ViewName);
        Assert.False((bool)spy.Calls.Last().Args[2]!);
    }

    public static TheoryData<DateTime, DateTime> InvalidRanges => new()
    {
        { new DateTime(2026, 9, 3), new DateTime(2026, 9, 2) },
        { DateTime.MaxValue.Date, DateTime.MaxValue.Date }
    };

    [Theory]
    [MemberData(nameof(InvalidRanges))]
    public async Task Invalid_range_does_not_query(DateTime from, DateTime to)
    {
        var (controller, spy) = Create("Doctor");

        await controller.Report(from, to);

        Assert.False(controller.ModelState.IsValid);
        Assert.Empty(spy.Calls);
    }

    [Fact]
    public async Task Cairo_spring_DST_day_covers_23_real_hours()
    {
        var (controller, spy) = Create("Doctor");

        await controller.Report(new DateTime(2026, 4, 24), new DateTime(2026, 4, 24));

        var args = Assert.Single(spy.Calls).Args;
        Assert.Equal(TimeSpan.FromHours(23), (DateTime)args[1]! - (DateTime)args[0]!);
    }

    [Fact]
    public void Every_action_declares_its_expected_payment_policy()
    {
        var actions = typeof(PaymentsController)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .ToList();

        var missing = actions
            .Where(action => !action.GetCustomAttributes<AuthorizeAttribute>()
                .Any(attribute => attribute.Policy == ExpectedPolicy(action.Name)))
            .Select(action => $"{action.Name} (expected {ExpectedPolicy(action.Name)})")
            .ToList();

        Assert.NotEmpty(actions);
        Assert.Empty(missing);
    }

    private static string ExpectedPolicy(string action) => action switch
    {
        "Report" => "PaymentReports",
        "DashboardLineDetails" or "RecalculateAllPaidStatuses" or "BackfillZeroCostPayments" => "AdminOnly",
        _ => "PaymentStaff"
    };
}
```

**`SafyaClinic/tests/SafyaClinic.Tests/Application/AuthServicePasswordTests.cs`**

```csharp
using SafyaClinic.Application.Services;
using Xunit;

namespace SafyaClinic.Tests.Application;

/// <summary>
/// Characterization tests for the CURRENT password helpers. They document
/// today's behaviour so slice S5 can replace the hashing safely; S5 will
/// update these tests together with the new hasher.
/// </summary>
public class AuthServicePasswordTests
{
    private const string Password = "Correct-horse-1";

    [Fact]
    public void Hash_then_verify_succeeds()
    {
        var hash = AuthService.HashPassword(Password);

        Assert.True(AuthService.VerifyPassword(Password, hash));
    }

    [Fact]
    public void Wrong_password_fails()
    {
        var hash = AuthService.HashPassword(Password);

        Assert.False(AuthService.VerifyPassword("wrong-password", hash));
    }

    [Fact]
    public void Malformed_hash_fails_without_throwing()
    {
        Assert.False(AuthService.VerifyPassword(Password, "not-a-valid-hash"));
    }

    [Fact]
    public void Same_password_gets_a_different_salt_each_time()
    {
        Assert.NotEqual(AuthService.HashPassword(Password), AuthService.HashPassword(Password));
    }
}
```

**Check:** the folder contains exactly the 4 files above.

### Step 3 — Add the project to the solution

```powershell
dotnet sln SafyaClinic\SafyaClinic.sln add SafyaClinic\tests\SafyaClinic.Tests\SafyaClinic.Tests.csproj --solution-folder tests
```

**Check:** the command prints `Project ... added to the solution.` Opening the solution in Visual Studio shows a `tests` folder containing `SafyaClinic.Tests`.

### Step 4 — Remove the old console checks

```powershell
git rm -r SafyaClinic/tests/PaymentAccessChecks
```

If Windows still has a `bin`/`obj` folder there, delete the remaining `SafyaClinic\tests\PaymentAccessChecks` folder in File Explorer.

### Step 5 — Run the tests locally

```powershell
dotnet build SafyaClinic\SafyaClinic.sln
dotnet test SafyaClinic\SafyaClinic.sln
node --test "SafyaClinic/tests/**/*.test.cjs"
```

**Expected:**
- Build: `0 Error(s)` (the 3 existing nullable warnings are fine).
- `dotnet test`: **Passed: 22, Failed: 0** (18 payment-access tests covering the original 30 checks, plus 4 password tests).
- Node: `pass 5`, `fail 0`.

If any test fails, stop and send me the output — do not change application code to make it pass.

### Step 6 — Ignore test output

Add one line to the **root** `.gitignore`:

```
TestResults/
```

### Step 7 (optional) — Align the EF Core command-line tool

The tool manifest pins `dotnet-ef` **10.0.10** while the projects use EF Core **8.0.28**. Align it:

```powershell
cd SafyaClinic\SafyaClinic.Web
dotnet tool update dotnet-ef --version 8.0.28 --local
dotnet tool list --local
cd ..\..
```

**Check:** `dotnet tool list --local` shows `dotnet-ef 8.0.28`. If version 8.0.28 is not found, run `dotnet tool search dotnet-ef --detail` and use the newest `8.0.x`; tell me which you used. Skip this step entirely if anything goes wrong — it is not required for F1.

### Step 8 — Add the CI workflow

Create `.github/workflows/ci.yml`:

```yaml
name: CI

on:
  push:
    branches: [ main ]
  pull_request:
    branches: [ main ]

permissions:
  contents: read

jobs:
  build-and-test:
    runs-on: ubuntu-latest

    steps:
      - name: Check out
        uses: actions/checkout@v4

      - name: Set up .NET (8 runtime for tests, 10 SDK for build)
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: |
            8.0.x
            10.0.x

      - name: Set up Node
        uses: actions/setup-node@v4
        with:
          node-version: '22'

      - name: Restore
        run: dotnet restore SafyaClinic/SafyaClinic.sln

      - name: Build
        run: dotnet build SafyaClinic/SafyaClinic.sln --no-restore --configuration Release

      - name: Test (.NET)
        run: >
          dotnet test SafyaClinic/SafyaClinic.sln --no-build --configuration Release
          --logger "trx;LogFileName=dotnet-tests.trx" --results-directory TestResults

      - name: Test (JavaScript)
        run: node --test "SafyaClinic/tests/**/*.test.cjs"

      - name: Vulnerable packages (report only until slice S2)
        continue-on-error: true
        run: |
          dotnet list SafyaClinic/SafyaClinic.sln package --vulnerable --include-transitive | tee vulnerable.txt
          if grep -q "has the following vulnerable packages" vulnerable.txt; then exit 1; fi

      - name: Upload test results
        if: always()
        uses: actions/upload-artifact@v4
        with:
          name: test-results
          path: TestResults
```

### Step 9 — Commit, push, open a PR

```powershell
git add global.json .gitignore .github/workflows/ci.yml SafyaClinic/SafyaClinic.sln SafyaClinic/tests/SafyaClinic.Tests
git add SafyaClinic/SafyaClinic.Web/dotnet-tools.json   # only if you did step 7
git status
git commit -m "Add xUnit test project, port payment access checks, pin SDK, add CI"
git push -u origin chore/f1-tests-ci
```

In `git status` before committing, expect: new `global.json`, `.github/workflows/ci.yml`, the 4 test files, modified `SafyaClinic.sln` and `.gitignore`, deleted `PaymentAccessChecks` files; **not** staged: `SafyaClinic.Web.csproj.user`, `bin/`, `obj/`, `TestResults/`.

Open a pull request into `main` on GitHub. **Do not merge yet** — wait for the review.

### Step 10 — Send me for review

1. The last ~15 lines of `dotnet test` from step 5.
2. The PR number. I will review the diff and the CI run from the repository (or send me the GitHub *Actions* run result if I cannot open it).

## Acceptance criteria

- [ ] `global.json` pins SDK 10.0.301 with `latestFeature` roll-forward.
- [ ] `SafyaClinic.Tests` is in the solution under a `tests` folder; `tests/PaymentAccessChecks` no longer exists.
- [ ] `dotnet test SafyaClinic\SafyaClinic.sln` passes **22** tests locally with no failures.
- [ ] Every one of the 30 original console checks is represented by an assertion in `PaymentsControllerAccessTests`.
- [ ] Node picker tests pass (5).
- [ ] The CI workflow runs on the PR and the *Restore*, *Build*, *Test (.NET)* and *Test (JavaScript)* steps are green; the vulnerable-package step reports the known S2 packages without failing the run.
- [ ] No application file under `SafyaClinic.Domain`, `.Application`, `.Infrastructure` or `.Web` is changed (except the optional `dotnet-tools.json`).
- [ ] App still starts locally and the dashboard opens (smoke check).

## After F1

Next slices, in roadmap order: **S1** (remove and rotate committed secrets) → S2 → S4 → S3 → S5 → S6 → S7 → S8.
