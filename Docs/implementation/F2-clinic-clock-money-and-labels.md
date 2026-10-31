# Slice F2 — Clinic clock, money and labels

Roadmap item **F2** (Group 2). Fixes **F13** and **F14**; implements D10 (Western digits) for money.

| | |
|---|---|
| Branch | `chore/f2-clock-money` (from an up-to-date `main`) |
| Behaviour change | 1. **Amounts show as `1,250.50 EGP`** everywhere they used the server's currency (`$1,250.50` before) — 51 places. 2. **Dashboard "Today's revenue" uses the Cairo day**: payments made 00:00–02:00/03:00 Cairo time now count for the right day. 3. "Today" on the dashboard, Today/Queue pages, reservation screens and the top bar is the **Cairo date**, also on a server that runs in UTC. 4. Check-in / consultation-start times, the patient timeline, the payment audit trail and users' last sign-in are shown **in Cairo time**, without "UTC" labels. 5. Treatment categories read *Internal Medicine* instead of *InternalMedicine* on the dashboard, reservation and medical-record screens |
| Database | None |
| Size | S |

## Design

| Decision | Choice |
|---|---|
| Clock | `IClinicClock` (singleton) with the clinic time zone from `Clinic:TimeZone` (default `Africa/Cairo`, works on Windows and Linux). Storage stays UTC. "Today" = the clinic date; day ranges = `DayStartUtc(date)` … `DayStartUtc(date + 1)` (handles Egypt's daylight-saving switch) |
| Money | `Money.Format(x)` / `x.ToMoney()` → `N2` with invariant culture + ` EGP` — Western digits whatever the UI language (D10). Tabular figures come with the design tokens (F6) |
| Labels | `Labels.Humanize("InternalMedicine")` → "Internal Medicine". Interim; F9 moves labels to English/Arabic resource files |
| Where applied now | All money displays (mechanical, Step 5); the dashboard; reservation/queue/today screens; timeline, payment audit and Users times. Other "today" uses (patients, medical records, follow-ups, nutrition) are switched when those screens are migrated |
| Payment report | Already converts correctly; unchanged in this slice |

## Affected files

| Action | Path |
|---|---|
| Add | `SafyaClinic/SafyaClinic.Application/Common/ClinicClock.cs`, `Money.cs`, `Labels.cs` |
| Edit | `SafyaClinic/SafyaClinic.Application/Interfaces/Services/IPaymentService.cs`, `Services/PaymentService.cs` (revenue range in UTC) |
| Edit | `SafyaClinic/SafyaClinic.Application/Services/ReservationService.cs` (clinic "today") |
| Edit | `SafyaClinic/SafyaClinic.Web/Program.cs`, `appsettings.json`, `Views/_ViewImports.cshtml` |
| Edit | `SafyaClinic/SafyaClinic.Web/Controllers/DashboardController.cs`, `PaymentsController.cs`, `FollowUpsController.cs` |
| Edit | 15 views (Step 5 script) + `Shared/_Layout.cshtml`, `Reservations/Details`, `Today`, `Queue`, `Create`, `Edit`, `Patients/Timeline`, `Payments/Audit`, `Users/Index`, `Analysis/Request`, `MedicalRecords/Details`, `PatientRecords`, `Dashboard/Index`, `Reservations/Index` |
| Add | `SafyaClinic/tests/SafyaClinic.Tests/Application/ClinicFormattingTests.cs`, `Web/DashboardControllerTests.cs`, `Web/ReservationServiceStub.cs` |
| Edit | `SafyaClinic/tests/SafyaClinic.Tests/Web/Payments/PaymentServiceSpy.cs` |
| Add | `Docs/implementation/F2-clinic-clock-money-and-labels.md` |
| Also commit | `Docs/assessment/10-…`, `12-…` (F36) and `Docs/implementation/H1-…` — already on your disk |

## Risks

| Risk | Mitigation |
|---|---|
| A stored time is not actually UTC and now shows shifted | Only fields named `…Utc`, `CreatedAt`, `PerformedAt`, `LastLoginAt` are converted — all written with `DateTime.UtcNow`. Reservation dates/times are local and **not** converted |
| Bulk money replacement breaks a view | Plain text replacement of `.ToString("C")` only; the build compiles every view; manual check of the money screens |
| Server without the Cairo time zone | `FindZone` falls back from the IANA to the Windows id; the app fails at start-up with a clear error rather than showing wrong times |

## Rollback

Revert the PR's merge commit. No data changes.

---

## Steps

PowerShell, from `D:\MyDevWork\clinic-MS`.

### Step 0 — Branch

```powershell
git switch main
git pull
git switch -c chore/f2-clock-money
```

### Step 1 — Helpers (Application)

**1a.** Create `SafyaClinic/SafyaClinic.Application/Common/ClinicClock.cs`:

```csharp
namespace SafyaClinic.Application.Common;

/// <summary>
/// The clinic's local time (F2). Storage stays in UTC; "today", day ranges and
/// every time shown to staff go through this clock.
/// </summary>
public interface IClinicClock
{
    TimeZoneInfo TimeZone { get; }
    DateTime UtcNow { get; }

    /// <summary>Current clinic local time.</summary>
    DateTime Now { get; }

    /// <summary>Current clinic calendar date (time 00:00).</summary>
    DateTime Today { get; }

    /// <summary>Converts a stored UTC instant to clinic local time.</summary>
    DateTime ToClinicTime(DateTime utc);

    /// <summary>The UTC instant at which a clinic calendar date starts.</summary>
    DateTime DayStartUtc(DateTime clinicDate);
}

public sealed class ClinicClock : IClinicClock
{
    public const string TimeZoneSettingKey = "Clinic:TimeZone";
    public const string DefaultTimeZoneId = "Africa/Cairo";

    private readonly Func<DateTime> _utcNow;

    public ClinicClock(TimeZoneInfo timeZone, Func<DateTime>? utcNow = null)
    {
        TimeZone = timeZone;
        _utcNow = utcNow ?? (() => DateTime.UtcNow);
    }

    /// <summary>Fallback for code paths without dependency injection (e.g. tests).</summary>
    public static ClinicClock Cairo { get; } = new(FindZone(DefaultTimeZoneId));

    /// <summary>Accepts IANA ids ("Africa/Cairo") on every OS, falling back to the Windows id.</summary>
    public static TimeZoneInfo FindZone(string id)
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(id);
        }
        catch (TimeZoneNotFoundException) when (TimeZoneInfo.TryConvertIanaIdToWindowsId(id, out var windowsId))
        {
            return TimeZoneInfo.FindSystemTimeZoneById(windowsId!);
        }
    }

    public TimeZoneInfo TimeZone { get; }

    public DateTime UtcNow => DateTime.SpecifyKind(_utcNow(), DateTimeKind.Utc);

    public DateTime Now => ToClinicTime(UtcNow);

    public DateTime Today => Now.Date;

    public DateTime ToClinicTime(DateTime utc) =>
        TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), TimeZone);

    public DateTime DayStartUtc(DateTime clinicDate)
    {
        // On a spring-forward day midnight may not exist; use the first real instant of the date.
        var local = DateTime.SpecifyKind(clinicDate.Date, DateTimeKind.Unspecified);
        while (TimeZone.IsInvalidTime(local)) local = local.AddMinutes(1);
        if (TimeZone.IsAmbiguousTime(local))
            return new DateTimeOffset(local, TimeZone.GetAmbiguousTimeOffsets(local).Max()).UtcDateTime;
        return TimeZoneInfo.ConvertTimeToUtc(local, TimeZone);
    }
}
```

**1b.** Create `SafyaClinic/SafyaClinic.Application/Common/Money.cs`:

```csharp
using System.Globalization;

namespace SafyaClinic.Application.Common;

/// <summary>Money display (F2, D10): "1,250.50 EGP", Western digits in every UI language.</summary>
public static class Money
{
    public const string Currency = "EGP";

    public static string Format(decimal amount) =>
        amount.ToString("N2", CultureInfo.InvariantCulture) + " " + Currency;

    public static string ToMoney(this decimal amount) => Format(amount);
}
```

**1c.** Create `SafyaClinic/SafyaClinic.Application/Common/Labels.cs`:

```csharp
using System.Text.RegularExpressions;

namespace SafyaClinic.Application.Common;

/// <summary>
/// Readable labels for code values shown to staff (F2): "InternalMedicine" → "Internal Medicine",
/// "NoShow" → "No Show". Interim — F9 moves labels to resource files for English/Arabic.
/// </summary>
public static class Labels
{
    public static string Humanize(string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : Regex.Replace(value.Trim(), "(?<=[a-z0-9])(?=[A-Z])", " ");
}
```

### Step 2 — Revenue for a UTC range (`IPaymentService`, `PaymentService`)

The dashboard used `GetRevenueByDateRangeAsync`, which cuts its inputs to whole dates — so a Cairo day could never be expressed. It is replaced by a method that takes exact UTC instants. (The dashboard is its only caller.)

**2a.** `Interfaces/Services/IPaymentService.cs` — replace

```csharp
    Task<ServiceResult<decimal>> GetRevenueByDateRangeAsync(
                    DateTime from,
                    DateTime to);
```

with

```csharp
    /// <summary>Total of active payments with fromUtc &lt;= PaymentDate &lt; toUtcExclusive (UTC instants).</summary>
    Task<ServiceResult<decimal>> GetRevenueBetweenAsync(DateTime fromUtc, DateTime toUtcExclusive);
```

**2b.** `Services/PaymentService.cs` — replace the whole method `public async Task<ServiceResult<decimal>> GetRevenueByDateRangeAsync(DateTime from, DateTime to) { … }` (under `//-----Aggregate method-----//`) with:

```csharp
    public async Task<ServiceResult<decimal>> GetRevenueBetweenAsync(DateTime fromUtc, DateTime toUtcExclusive)
    {
        var total = await _uow.Payments.Query()
            .Where(p => p.Status == PaymentStatusEnum.Active
                        && p.PaymentDate >= fromUtc
                        && p.PaymentDate < toUtcExclusive)
            .SumAsync(p => p.Amount);

        return ServiceResult<decimal>.Success(total);
    }
```

### Step 3 — Clinic "today" in `ReservationService`

**3a.** Add with the `using` lines: `using SafyaClinic.Application.Common;`

**3b.** Replace

```csharp
    private readonly IUnitOfWork _uow;

    public ReservationService(IUnitOfWork uow) => _uow = uow;
```

with

```csharp
    private readonly IUnitOfWork _uow;
    private readonly IClinicClock _clock;

    public ReservationService(IUnitOfWork uow, IClinicClock? clock = null)
    {
        _uow = uow;
        _clock = clock ?? ClinicClock.Cairo;
    }
```

**3c.** In this file only, replace every `DateTime.Today` with `_clock.Today` (Visual Studio: *Edit → Find and Replace → Current Document*; 5 replacements).

### Step 4 — Web wiring

**4a.** `Program.cs` — add `using SafyaClinic.Application.Common;`, and after the lockout registration (`builder.Services.AddSingleton(… LockoutSettings …);`) add:

```csharp

// ── Clinic clock (F2): one time zone for "today" and every time shown ──
builder.Services.AddSingleton<IClinicClock>(new ClinicClock(ClinicClock.FindZone(
    builder.Configuration[ClinicClock.TimeZoneSettingKey] ?? ClinicClock.DefaultTimeZoneId)));
```

**4b.** `appsettings.json` — after the `"Security": { … },` block add:

```json
  "Clinic": {
    "TimeZone": "Africa/Cairo"
  },
```

**4c.** `Views/_ViewImports.cshtml` — add at the end:

```cshtml
@using SafyaClinic.Application.Common
@inject IClinicClock Clock
```

**4d.** `Controllers/DashboardController.cs` — add `using SafyaClinic.Application.Common;`, then replace the fields/constructor and the "today" lines:

```csharp
    private readonly IReservationService _reservationService;
    private readonly IPaymentService _paymentService;

    public DashboardController(
        IReservationService reservationService,
        IPaymentService paymentService)
    {
        _reservationService = reservationService;  
        _paymentService = paymentService;
    }
```

with

```csharp
    private readonly IReservationService _reservationService;
    private readonly IPaymentService _paymentService;
    private readonly IClinicClock _clock;

    public DashboardController(
        IReservationService reservationService,
        IPaymentService paymentService,
        IClinicClock clock)
    {
        _reservationService = reservationService;
        _paymentService = paymentService;
        _clock = clock;
    }
```

and

```csharp
        var today = DateTime.Today;
        var endOfToday = today.AddDays(1).AddSeconds(-1);

        var todayPayments = await _paymentService.GetRevenueByDateRangeAsync(
            today,
            endOfToday);
```

with

```csharp
        // F2: the clinic's day, as an exact UTC range (payments are stored in UTC).
        var today = _clock.Today;
        var todayPayments = await _paymentService.GetRevenueBetweenAsync(
            _clock.DayStartUtc(today),
            _clock.DayStartUtc(today.AddDays(1)));
```

**4e.** Money in controller messages — add `using SafyaClinic.Application.Common;` to both files, then:

- `Controllers/PaymentsController.cs` (the success message after collecting, ~line 278): replace each `{result.Data.Amount:C}` with `{Money.Format(result.Data.Amount)}` and `{result.Data.SourceDeductionAmount:C}` with `{Money.Format(result.Data.SourceDeductionAmount)}` (three replacements on those two lines; leave the rest of the text).
- `Controllers/FollowUpsController.cs` (~line 235): replace `{t.DefaultCost.Value:C}` with `{Money.Format(t.DefaultCost.Value)}`.


### Step 5 — Money in the views (scripted)

This replaces every `.ToString("C")` in the views with `.ToMoney()`, keeping each file's encoding exactly as it is:

```powershell
Get-ChildItem SafyaClinic\SafyaClinic.Web\Views -Recurse -Filter *.cshtml | ForEach-Object {
    $path = $_.FullName
    $bytes = [IO.File]::ReadAllBytes($path)
    $hasBom = $bytes.Length -ge 3 -and $bytes[0] -eq 0xEF -and $bytes[1] -eq 0xBB -and $bytes[2] -eq 0xBF
    $text = [IO.File]::ReadAllText($path)
    $new = $text.Replace('.ToString("C")', '.ToMoney()')
    if ($new -ne $text) {
        [IO.File]::WriteAllText($path, $new, (New-Object System.Text.UTF8Encoding($hasBom)))
        $path
    }
}
git grep -n 'ToString("C")' -- SafyaClinic/SafyaClinic.Web
```

It prints the changed files (15); the final `git grep` must print nothing. Then fix the four interpolations the script does not cover:

| File | Replace | With |
|---|---|---|
| `Views/Analysis/Request.cshtml` | `$"({t.DefaultCost:C})"` | `$"({t.DefaultCost.Value.ToMoney()})"` |
| `Views/Reservations/Create.cshtml` | `$"— {t.DefaultCost:C}"` | `$"— {t.DefaultCost.Value.ToMoney()}"` |
| `Views/Reservations/Edit.cshtml` | `$"— {t.DefaultCost:C}"` | `$"— {t.DefaultCost.Value.ToMoney()}"` |
| `Views/MedicalRecords/Details.cshtml` | `$"• {t.Cost:C}"` | `$"• {t.Cost.Value.ToMoney()}"` |

(Each is already inside a `!= null` check.) **Check:** `git grep -n ":C}" -- SafyaClinic/SafyaClinic.Web` prints nothing. (Old guides under `SafyaClinic/docs` quote former code and still contain `:C}` — that is fine.)

### Step 6 — Times and "today" in the views

| File | Replace | With |
|---|---|---|
| `Views/Shared/_Layout.cshtml` | `@DateTime.Now.ToString("dddd, dd MMM yyyy")` | `@Clock.Now.ToString("dddd, dd MMM yyyy")` |
| `Views/Reservations/Today.cshtml` | `@DateTime.Today.ToString("dd MMM yyyy")` | `@Clock.Today.ToString("dd MMM yyyy")` |
| `Views/Reservations/Queue.cshtml` | `@DateTime.Today.ToString("dddd, dd MMM yyyy")` | `@Clock.Today.ToString("dddd, dd MMM yyyy")` |
| `Views/Reservations/Details.cshtml` (top) | `Model.ReservationDate.Date == DateTime.Today &&` | `Model.ReservationDate.Date == Clock.Today &&` |
| `Views/Reservations/Details.cshtml` | `@Model.CheckedInAtUtc.Value.ToString("dd MMM yyyy HH:mm") UTC` | `@Clock.ToClinicTime(Model.CheckedInAtUtc.Value).ToString("dd MMM yyyy HH:mm")` |
| `Views/Reservations/Details.cshtml` | `@Model.ConsultationStartedAtUtc.Value.ToString("dd MMM yyyy HH:mm") UTC` | `@Clock.ToClinicTime(Model.ConsultationStartedAtUtc.Value).ToString("dd MMM yyyy HH:mm")` |
| `Views/Patients/Timeline.cshtml` | `Newest first · Event timestamps in UTC` | `Newest first` |
| `Views/Patients/Timeline.cshtml` | `@item.OccurredAt.ToString("dd MMM yyyy · HH:mm") UTC` | `@Clock.ToClinicTime(item.OccurredAt).ToString("dd MMM yyyy · HH:mm")` |
| `Views/Payments/Audit.cshtml` | `Newest first · Timestamps in UTC` | `Newest first` |
| `Views/Payments/Audit.cshtml` | `<th>Date / Time (UTC)</th>` | `<th>Date / Time</th>` |
| `Views/Payments/Audit.cshtml` | `@adjustment.PerformedAt.ToString("dd MMM yyyy HH:mm")` | `@Clock.ToClinicTime(adjustment.PerformedAt).ToString("dd MMM yyyy HH:mm")` |
| `Views/Users/Index.cshtml` | `@(u.LastLoginAt?.ToString("dd MMM yyyy HH:mm") ?? "Never")` | `@(u.LastLoginAt is DateTime lastLogin ? Clock.ToClinicTime(lastLogin).ToString("dd MMM yyyy HH:mm") : "Never")` |

### Step 7 — Readable categories

Replace (exact text, one per file):

| File | Replace | With |
|---|---|---|
| `Views/Dashboard/Index.cshtml` | `@r.Category</span>` | `@Labels.Humanize(r.Category)</span>` |
| `Views/Reservations/Index.cshtml` | `@r.Category</span>` | `@Labels.Humanize(r.Category)</span>` |
| `Views/Reservations/Today.cshtml` | `@r.Category</span>` | `@Labels.Humanize(r.Category)</span>` |
| `Views/Reservations/Details.cshtml` | `@Model.Category</span>` | `@Labels.Humanize(Model.Category)</span>` |
| `Views/MedicalRecords/Details.cshtml` | `@Model.Category</span>` | `@Labels.Humanize(Model.Category)</span>` |
| `Views/MedicalRecords/PatientRecords.cshtml` | `@r.Category</span>` | `@Labels.Humanize(r.Category)</span>` |


### Step 8 — Tests

**8a.** Create `SafyaClinic/tests/SafyaClinic.Tests/Application/ClinicFormattingTests.cs`:

```csharp
using System.Globalization;
using SafyaClinic.Application.Common;
using Xunit;

namespace SafyaClinic.Tests.Application;

/// <summary>F2 / F13, F14: clinic time, money and labels.</summary>
public class ClinicFormattingTests
{
    private static ClinicClock ClockAt(DateTime utcNow) =>
        new(ClinicClock.FindZone(ClinicClock.DefaultTimeZoneId), () => utcNow);

    [Fact]
    public void Today_is_the_clinic_date_not_the_utc_date()
    {
        // 22:30 UTC on 1 Dec = 00:30 on 2 Dec in Cairo (winter, UTC+2).
        var clock = ClockAt(new DateTime(2026, 12, 1, 22, 30, 0, DateTimeKind.Utc));

        Assert.Equal(new DateTime(2026, 12, 2), clock.Today);
    }

    [Fact]
    public void Winter_day_starts_at_22_00_utc_the_day_before()
    {
        Assert.Equal(new DateTime(2026, 12, 1, 22, 0, 0), ClinicClock.Cairo.DayStartUtc(new DateTime(2026, 12, 2)));
    }

    [Fact]
    public void Summer_day_starts_at_21_00_utc_the_day_before()
    {
        Assert.Equal(new DateTime(2026, 7, 14, 21, 0, 0), ClinicClock.Cairo.DayStartUtc(new DateTime(2026, 7, 15)));
    }

    [Fact]
    public void Stored_utc_times_are_shown_in_clinic_time()
    {
        Assert.Equal(new DateTime(2026, 12, 1, 12, 6, 0), ClinicClock.Cairo.ToClinicTime(new DateTime(2026, 12, 1, 10, 6, 0)));
    }

    [Fact]
    public void Money_uses_egp_and_western_digits_in_any_culture()
    {
        var previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("ar-EG");
            Assert.Equal("1,250.50 EGP", 1250.5m.ToMoney());
            Assert.Equal("0.00 EGP", Money.Format(0m));
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }

    [Theory]
    [InlineData("InternalMedicine", "Internal Medicine")]
    [InlineData("NoShow", "No Show")]
    [InlineData("InConsultation", "In Consultation")]
    [InlineData("", "")]
    public void Code_values_become_readable_labels(string value, string expected)
    {
        Assert.Equal(expected, Labels.Humanize(value));
    }
}
```

**8b.** In `SafyaClinic/tests/SafyaClinic.Tests/Web/Payments/PaymentServiceSpy.cs`, inside the `switch`, before the `_ =>` line, add:

```csharp
            "GetRevenueBetweenAsync" =>
                Task.FromResult(ServiceResult<decimal>.Success(0m)),
```

**8c.** Create `SafyaClinic/tests/SafyaClinic.Tests/Web/ReservationServiceStub.cs`:

```csharp
using System.Reflection;
using SafyaClinic.Application.DTOs.Common;
using SafyaClinic.Application.DTOs.Reservation;

namespace SafyaClinic.Tests.Web;

/// <summary>Test double for IReservationService: today's list is empty.</summary>
public class ReservationServiceStub : DispatchProxy
{
    protected override object? Invoke(MethodInfo? targetMethod, object?[]? args) => targetMethod!.Name switch
    {
        "GetTodayReservationsAsync" =>
            Task.FromResult(ServiceResult<IEnumerable<ReservationSummaryDto>>.Success(Array.Empty<ReservationSummaryDto>())),
        _ => throw new NotSupportedException(targetMethod.Name)
    };
}
```

**8d.** Create `SafyaClinic/tests/SafyaClinic.Tests/Web/DashboardControllerTests.cs`:

```csharp
using System.Reflection;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SafyaClinic.Application.Common;
using SafyaClinic.Application.Interfaces.Services;
using SafyaClinic.Tests.Web.Payments;
using SafyaClinic.Web.Controllers;
using Xunit;

namespace SafyaClinic.Tests.Web;

/// <summary>F2 / F14: today's revenue is the Cairo day, as a UTC range.</summary>
public class DashboardControllerTests
{
    [Fact]
    public async Task Today_revenue_counts_payments_from_cairo_midnight()
    {
        var payments = DispatchProxy.Create<IPaymentService, PaymentServiceSpy>();
        var reservations = DispatchProxy.Create<IReservationService, ReservationServiceStub>();
        // 22:30 UTC on 1 Dec is 00:30 on 2 Dec in Cairo: "today" must be 2 Dec.
        var clock = new ClinicClock(ClinicClock.FindZone(ClinicClock.DefaultTimeZoneId),
            () => new DateTime(2026, 12, 1, 22, 30, 0, DateTimeKind.Utc));
        var controller = new DashboardController(reservations, payments, clock)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity()) }
            }
        };

        await controller.Index();

        var call = ((PaymentServiceSpy)(object)payments).Calls.Single(c => c.Name == "GetRevenueBetweenAsync");
        Assert.Equal(new DateTime(2026, 12, 1, 22, 0, 0), (DateTime)call.Args[0]!);
        Assert.Equal(new DateTime(2026, 12, 2, 22, 0, 0), (DateTime)call.Args[1]!);
    }
}
```

**Check:**

```powershell
dotnet build SafyaClinic\SafyaClinic.sln
dotnet test SafyaClinic\SafyaClinic.sln
git grep -n 'ToString("C")\|:C}\| UTC<\|in UTC' -- SafyaClinic/SafyaClinic.Web
```

Expected: **Passed: 108** (98 + 9 formatting + 1 dashboard), Failed: 0; the `git grep` prints nothing. If anything fails, send me the output — do not change the tests to make them pass.

### Step 9 — Manual check

`dotnet run --project SafyaClinic\SafyaClinic.Web --launch-profile http`, sign in as Admin:

1. **Dashboard:** *Today's revenue* shows `… EGP`; the top bar shows today's date.
2. **Payments → Dashboard, Report, a patient's payment summary, a payment's audit page:** every amount ends in `EGP`, digits are 0–9, no `$`. Audit times have no "UTC" and are 2 hours later than before (winter; 3 hours in summer).
3. **A reservation that was checked in:** check-in and consultation-start times match the wall clock when they happened, no "UTC".
4. **Patient timeline:** no "UTC" label; times in Cairo time.
5. **Users:** last sign-in times in Cairo time.
6. **Reservations list / Today / dashboard table:** category reads *Internal Medicine*.
7. **Nutrition packages / enrollment details:** prices end in `EGP`.

### Step 10 — Commit, push, PR

```powershell
git add SafyaClinic/SafyaClinic.Application SafyaClinic/SafyaClinic.Web SafyaClinic/tests Docs
git status
git commit -m "F2: clinic clock (Cairo) for today and displayed times; EGP money format; readable categories"
git push -u origin chore/f2-clock-money
```

Open the PR and send me the number and CI result. Do not merge before review.

## Acceptance criteria

- [ ] No `$` amounts: every money display shows `… EGP` with Western digits.
- [ ] Dashboard revenue counts payments from Cairo midnight to Cairo midnight (test).
- [ ] "Today" on dashboard/reservation screens is the Cairo date regardless of the server's time zone.
- [ ] Staff screens listed show Cairo times and no "UTC" labels.
- [ ] Categories are readable.
- [ ] 108 tests pass locally and in CI.
