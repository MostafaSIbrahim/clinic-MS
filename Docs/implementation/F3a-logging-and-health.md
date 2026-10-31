# Slice F3a — Logging and health check

Roadmap item **F3**, first half (Group 2). Covers the logging and health parts of **F23**. The audit trail (the other half of F3) follows as **F3b**, because it changes the database.

| | |
|---|---|
| Branch | `chore/f3a-logging-health` (from an up-to-date `main`, after PR #13 is merged) |
| Behaviour change | 1. **Log files**: the app writes daily JSON log files to `App_Data/logs` (kept 31 days, max 20 MB each) — on the hosted sites too, where today nothing is kept. 2. Every log entry carries the **request id** (the reference shown on the error page) and, when signed in, the **user id**. 3. **One summary line per request** (method, path, status, duration, user) instead of the framework's noisy per-request messages. 4. **`/health`** answers `Healthy` (200) or `Unhealthy` (503) depending on whether the database is reachable — for an uptime monitor. No user-facing change |
| Database | None |
| New package | `Serilog.AspNetCore` **8.0.3** (targets .NET 8; brings the rolling file sink and compact JSON formatter) |
| Size | S |

## Why

On the hosted sites, logs go to the console, which IIS discards — so when something fails there is no record (F23). Microsoft's guidance is to use a logging library with size limits and rotation for routine logs rather than the IIS stdout log.

## Design

| Decision | Choice |
|---|---|
| Where | `App_Data/logs/safya-YYYYMMDD.json` under the site — never served (IIS blocks `App_Data`), not published, not in git (`App_Data/` is ignored), and Web Deploy keeps server files (`SkipExtraFilesOnServer`) |
| Format | Compact JSON (one event per line, searchable); the console keeps a readable one-line format |
| Levels | Information; ASP.NET Core framework messages at Warning; SQL commands at Information only in Development (as today) |
| Configured in code | Hosted sites keep their own `appsettings.json` (it is never published), so levels and paths are set in code with safe defaults |
| Correlation | The request id is already in every entry (ASP.NET Core scope); a small middleware adds `UserId` for signed-in requests |
| Health | A custom check (`Database.CanConnectAsync`) — no extra package. The endpoint shows only `Healthy`/`Unhealthy`, never error details |
| Not here | Audit trail (F3b); central log shipping (later, if needed) |

## Affected files

| Action | Path |
|---|---|
| Edit | `SafyaClinic/SafyaClinic.Web/SafyaClinic.Web.csproj` (package) |
| Edit | `SafyaClinic/SafyaClinic.Web/Program.cs` |
| Add | `SafyaClinic/SafyaClinic.Web/Startup/UserLogScopeMiddleware.cs` |
| Add | `SafyaClinic/SafyaClinic.Web/Startup/DatabaseHealthCheck.cs` |
| Add | `SafyaClinic/tests/SafyaClinic.Tests/Web/LoggingAndHealthTests.cs` |
| Add | `Docs/deployment/logs-and-health.md` |
| Add | `Docs/implementation/F3a-logging-and-health.md` |

## Risks

| Risk | Mitigation |
|---|---|
| The host cannot write to `App_Data/logs` | The same permission S3 needs for uploads; checked on the test site first. Serilog never crashes the app if a file cannot be written |
| Logs contain personal data | Messages already avoid phone numbers/passwords (S5); request logging records path and status, not form contents or query values of POSTs. Files are not web-accessible |
| Disk usage | 31 files × 20 MB maximum |

## Rollback

Revert the PR's merge commit. Log files already written can be deleted.

---

## Steps

PowerShell, from `D:\MyDevWork\clinic-MS`.

### Step 0 — Branch

```powershell
git switch main
git pull
git switch -c chore/f3a-logging-health
```

### Step 1 — Package

```powershell
dotnet add SafyaClinic\SafyaClinic.Web package Serilog.AspNetCore --version 8.0.3
```

### Step 2 — User id on every log entry

Create `SafyaClinic/SafyaClinic.Web/Startup/UserLogScopeMiddleware.cs`:

```csharp
using System.Security.Claims;

namespace SafyaClinic.Web.Startup;

/// <summary>
/// Adds the signed-in user's id to every log entry written while handling the request (F3a).
/// The request id is already added by ASP.NET Core.
/// </summary>
public sealed class UserLogScopeMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<UserLogScopeMiddleware> _logger;

    public UserLogScopeMiddleware(RequestDelegate next, ILogger<UserLogScopeMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var userId = context.User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId is null)
        {
            await _next(context);
            return;
        }

        using (_logger.BeginScope(new Dictionary<string, object> { ["UserId"] = userId }))
        {
            await _next(context);
        }
    }
}
```

### Step 3 — Database health check

Create `SafyaClinic/SafyaClinic.Web/Startup/DatabaseHealthCheck.cs`:

```csharp
using Microsoft.Extensions.Diagnostics.HealthChecks;
using SafyaClinic.Infrastructure.Data;

namespace SafyaClinic.Web.Startup;

/// <summary>Healthy when the database answers (F3a). Details are logged, never shown.</summary>
public sealed class DatabaseHealthCheck : IHealthCheck
{
    private readonly SafyaDbContext _db;

    public DatabaseHealthCheck(SafyaDbContext db) => _db = db;

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            return await _db.Database.CanConnectAsync(cancellationToken)
                ? HealthCheckResult.Healthy("Database reachable.")
                : HealthCheckResult.Unhealthy("Database not reachable.");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Database not reachable.", ex);
        }
    }
}
```

### Step 4 — `Program.cs`

**4a.** Add with the `using` lines:

```csharp
using System.Security.Claims;
using Serilog;
using Serilog.Events;
using Serilog.Formatting.Compact;
```

(`using SafyaClinic.Web.Startup;` is already there from S8.)

**4b.** Directly after `var builder = WebApplication.CreateBuilder(args);` add:

```csharp

// ── Logging (F3a): readable console + daily JSON files in App_Data/logs ──
// Set in code because hosted sites keep their own appsettings.json.
builder.Host.UseSerilog((context, services, logger) => logger
    .MinimumLevel.Information()
    .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
    .MinimumLevel.Override("Microsoft.EntityFrameworkCore.Database.Command",
        context.HostingEnvironment.IsDevelopment() ? LogEventLevel.Information : LogEventLevel.Warning)
    .Enrich.FromLogContext()
    .WriteTo.Console(outputTemplate:
        "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj} (request {RequestId}, user {UserId}){NewLine}{Exception}")
    .WriteTo.File(
        new CompactJsonFormatter(),
        Path.Combine(context.HostingEnvironment.ContentRootPath, "App_Data", "logs", "safya-.json"),
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 31,
        fileSizeLimitBytes: 20 * 1024 * 1024,
        rollOnFileSizeLimit: true));
```

**4c.** After the clinic clock registration (`builder.Services.AddSingleton<IClinicClock>(…);`) add:

```csharp

// ── Health check (F3a): GET /health → Healthy / Unhealthy ──
builder.Services.AddHealthChecks().AddCheck<DatabaseHealthCheck>("database");
```

**4d.** In the pipeline, replace

```csharp
app.UseStaticFiles();
```

with

```csharp
app.UseStaticFiles();

// One summary line per request (F3a); static files above are not logged.
app.UseSerilogRequestLogging(options =>
{
    options.EnrichDiagnosticContext = (diagnostics, http) =>
    {
        var userId = http.User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId is not null) diagnostics.Set("UserId", userId);
    };
});
```

and replace

```csharp
app.UseAuthentication();
```

with

```csharp
app.UseAuthentication();
app.UseMiddleware<UserLogScopeMiddleware>();   // F3a: user id on every log entry
```

**4e.** Directly **before** `app.MapControllerRoute(`, add:

```csharp
app.MapHealthChecks("/health");   // F3a
```

The `"Logging"` sections in `appsettings*.json` are no longer used for levels (Serilog takes over); leave them — removing them is not necessary.

### Step 5 — Tests

Create `SafyaClinic/tests/SafyaClinic.Tests/Web/LoggingAndHealthTests.cs`:

```csharp
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using SafyaClinic.Infrastructure.Data;
using SafyaClinic.Web.Startup;
using Xunit;

namespace SafyaClinic.Tests.Web;

/// <summary>F3a: user id in the log scope; database health check.</summary>
public class LoggingAndHealthTests
{
    private sealed class ScopeRecorder : ILogger<UserLogScopeMiddleware>
    {
        public List<object> Scopes { get; } = new();
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull { Scopes.Add(state); return null; }
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter) { }
    }

    [Fact]
    public async Task Signed_in_requests_log_the_user_id()
    {
        var recorder = new ScopeRecorder();
        var middleware = new UserLogScopeMiddleware(_ => Task.CompletedTask, recorder);
        var context = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(ClaimTypes.NameIdentifier, "7") }, "test"))
        };

        await middleware.InvokeAsync(context);

        var scope = Assert.IsAssignableFrom<IDictionary<string, object>>(Assert.Single(recorder.Scopes));
        Assert.Equal("7", scope["UserId"]);
    }

    [Fact]
    public async Task Anonymous_requests_add_no_user_scope()
    {
        var recorder = new ScopeRecorder();
        var middleware = new UserLogScopeMiddleware(_ => Task.CompletedTask, recorder);

        await middleware.InvokeAsync(new DefaultHttpContext());

        Assert.Empty(recorder.Scopes);
    }

    [Fact]
    public async Task Reachable_database_is_healthy()
    {
        await using var db = new SafyaDbContext(new DbContextOptionsBuilder<SafyaDbContext>()
            .UseInMemoryDatabase($"health-{Guid.NewGuid()}").Options);

        var result = await new DatabaseHealthCheck(db).CheckHealthAsync(new HealthCheckContext());

        Assert.Equal(HealthStatus.Healthy, result.Status);
    }

    [Fact]
    public async Task Unreachable_database_is_unhealthy()
    {
        // Port 1 on this machine: nothing listens, so the connection is refused quickly.
        await using var db = new SafyaDbContext(new DbContextOptionsBuilder<SafyaDbContext>()
            .UseSqlServer("Server=tcp:127.0.0.1,1;Database=HealthCheckTest;Integrated Security=True;Connect Timeout=2;TrustServerCertificate=True")
            .Options);

        var result = await new DatabaseHealthCheck(db).CheckHealthAsync(new HealthCheckContext());

        Assert.Equal(HealthStatus.Unhealthy, result.Status);
    }
}
```

**Check:**

```powershell
dotnet build SafyaClinic\SafyaClinic.sln
dotnet test SafyaClinic\SafyaClinic.sln
dotnet list SafyaClinic\SafyaClinic.sln package --vulnerable --include-transitive
```

Expected: **Passed: 112** (108 + 4), Failed: 0; no vulnerable packages. If anything fails, send me the output — do not change the tests to make them pass.

### Step 6 — Deployment note

Create `Docs/deployment/logs-and-health.md`:

````markdown
# Logs and health check (hosted sites)

## Health

`https://<site>/health` answers `Healthy` (HTTP 200) when the application runs and reaches its database, otherwise `Unhealthy` (HTTP 503). It shows no details. Point an uptime monitor at it (e.g. a free external monitor, checking every 5 minutes).

## Logs

The application writes one JSON file per day to `App_Data/logs/safya-YYYYMMDD.json` (31 days kept, 20 MB per file). Open them with the MonsterASP File Manager. Each line is one event; useful fields:

| Field | Meaning |
|---|---|
| `@t` | time (UTC) |
| `@l` | level (missing = Information) |
| `@mt` / `@m` | message |
| `RequestId` | the reference shown on the error page — search for it to find what went wrong |
| `UserId` | signed-in user's id |
| `@x` | exception details |

The site's application pool needs write access to `App_Data` (the same as for attachments, S3). If no file appears after a few requests, check that permission in the control panel.

Startup failures (before logging starts) are only visible with the IIS stdout log: temporarily set `stdoutLogEnabled="true"` in the server's `web.config`, reproduce, then set it back to `false` (it is not rotated).
````

### Step 7 — Manual check

`dotnet run --project SafyaClinic\SafyaClinic.Web --launch-profile http`

1. The console shows lines like `[14:02:11 INF] HTTP GET /Dashboard responded 200 in 35.2 ms (request 0HN…, user 1)`.
2. `SafyaClinic\SafyaClinic.Web\App_Data\logs\safya-<today>.json` exists and grows as you click around.
3. Open `http://localhost:5070/health` (signed out is fine) → `Healthy`.
4. Run as Production and cause the S4 error (Analysis Types, Description over 500 characters): the error page's reference appears as `RequestId` in that day's log file, together with the exception.
5. `git status` does not list anything under `App_Data`.

### Step 8 — Commit, push, PR

```powershell
git add SafyaClinic/SafyaClinic.Web SafyaClinic/tests Docs
git status
git commit -m "F3a: Serilog with daily JSON files and request summaries; user id in log scope; /health database check"
git push -u origin chore/f3a-logging-health
```

Open the PR and send me the number and CI result. Do not merge before review.

## Acceptance criteria

- [ ] Log entries include `RequestId`, and `UserId` when signed in; one summary line per request.
- [ ] Daily JSON log files in `App_Data/logs`, rotated, 31 days kept; not in git, not served.
- [ ] `/health` returns 200 `Healthy` / 503 `Unhealthy` without details.
- [ ] Error-page reference can be found in the log file.
- [ ] No vulnerable packages; 112 tests pass locally and in CI.
