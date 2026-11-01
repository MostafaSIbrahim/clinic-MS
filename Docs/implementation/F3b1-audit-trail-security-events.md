# Slice F3b-1 — Audit trail: table, writer and security events

Roadmap item **F3**, second half, part 1 (Group 2). Covers the audit part of **F23** and R26 for sign-in and user management. Part 2 (**F3b-2**) adds medical-record and payment events.

| | |
|---|---|
| Branch | `chore/f3b1-audit` (from an up-to-date `main`, after PR #14 is merged) |
| Behaviour change | **No visible change.** The app now writes one row to `AuditLogs` for each: sign-in succeeded / failed / locked out / refused while locked; password changed; password reset by an admin; user created; user activated / deactivated; role assigned / removed. Each row records **who** (user id), **what** (action, entity, id, small details), **when** (UTC), **from where** (IP) and the **request id** (same as logs and error page). Passwords and phone numbers are never written |
| Database | ⚠ **Reshapes the unused `AuditLogs` table** (no code has ever written to it; it is empty). A migration drops its old untyped columns and adds typed ones plus three indexes. Step 7 checks it is empty first |
| Size | M |

## Design

| Decision | Choice |
|---|---|
| Table | `AuditLogs(Id, OccurredAtUtc, UserId?, Action(64), EntityType(64), EntityId?, Details(4000, JSON), IpAddress(45), RequestId(64))`; indexes on time, (EntityType, EntityId), (UserId, time). No foreign key to `Users`, so entries survive user changes |
| Writer | `IAuditTrail.RecordAsync(action, entityType, entityId, details, actingUserId)` adds the row to the **same unit of work** as the change; the service's `SaveChangesAsync` saves both together — no audit row for a change that did not happen, and no change without its audit row |
| Who / where | `IAuditContext` (Web implementation reads the signed-in user, IP and request id from the current request). At sign-in the actor is the user signing in; for a failed sign-in it stays empty |
| Action names | Constants in `AuditActions` (`SignIn.Succeeded`, `User.Deactivated`, …) |
| Privacy | Details are small objects chosen per action (attempt number, role id…). No passwords, hashes, phone numbers or free text |
| Reading | No screen yet; an Admin audit view comes with the permission UI (F4d). Until then: SQL (`SELECT … FROM AuditLogs ORDER BY Id DESC`) |

## Affected files

| Action | Path |
|---|---|
| Edit | `SafyaClinic/SafyaClinic.Domain/Entities/Audit/AuditLog.cs` (replaced) |
| Edit | `SafyaClinic/SafyaClinic.Domain/Interfaces/Repositories/IUnitOfWork.cs`, `SafyaClinic.Infrastructure/Repositories/UnitOfWork.cs` |
| Add | `SafyaClinic/SafyaClinic.Infrastructure/Data/Configurations/AuditLogConfiguration.cs` |
| Add | `SafyaClinic/SafyaClinic.Infrastructure/Migrations/<timestamp>_ReshapeAuditLogs.cs` (+ Designer, snapshot) — generated |
| Add | `SafyaClinic/SafyaClinic.Application/Common/Auditing/Audit.cs` |
| Edit | `SafyaClinic/SafyaClinic.Application/Services/AuthService.cs`, `UserService.cs` |
| Add | `SafyaClinic/SafyaClinic.Web/Security/HttpAuditContext.cs` |
| Edit | `SafyaClinic/SafyaClinic.Web/Program.cs` |
| Add | `SafyaClinic/tests/SafyaClinic.Tests/Application/AuditTrailTests.cs` |
| Add | `Docs/implementation/F3b1-audit-trail-security-events.md` |

## Risks

| Risk | Mitigation |
|---|---|
| The migration drops data | The table has never been written to; Step 7 counts its rows on each database first and stops if any exist |
| Audit write fails and blocks the action | It is part of the same save: if saving fails, the action fails too (and is logged) — intended for an audit trail |
| Table growth | ~ a few hundred rows a day for a clinic; indexed; archiving can come later |

## Rollback

Revert the PR's merge commit. The reshaped table can stay (old code never used it). To restore the old shape: `dotnet ef database update AddSecurityStamp` — ask me first.

---

## Steps

PowerShell, from `D:\MyDevWork\clinic-MS`.

### Step 0 — Branch

```powershell
git switch main
git pull
git switch -c chore/f3b1-audit
```

### Step 1 — Entity and configuration

**1a.** Replace the whole `SafyaClinic/SafyaClinic.Domain/Entities/Audit/AuditLog.cs` with:

```csharp
using SafyaClinic.Domain.Entities.Common;

namespace SafyaClinic.Domain.Entities.Audit
{
    /// <summary>One security- or money-relevant event (F3b). Written by IAuditTrail; never updated.</summary>
    public class AuditLog : BaseEntity
    {
        public DateTime OccurredAtUtc { get; set; }
        public int? UserId { get; set; }                         // who did it; null when unknown (failed sign-in)
        public string Action { get; set; } = string.Empty;       // e.g. "User.Deactivated"
        public string EntityType { get; set; } = string.Empty;   // e.g. "User"
        public int? EntityId { get; set; }
        public string? Details { get; set; }                     // small JSON object; never passwords
        public string? IpAddress { get; set; }
        public string? RequestId { get; set; }                   // matches the logs and the error page
    }
}
```

**1b.** Create `SafyaClinic/SafyaClinic.Infrastructure/Data/Configurations/AuditLogConfiguration.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SafyaClinic.Domain.Entities.Audit;

namespace SafyaClinic.Infrastructure.Data.Configurations
{
    public class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
    {
        public void Configure(EntityTypeBuilder<AuditLog> builder)
        {
            builder.ToTable("AuditLogs");
            builder.HasKey(a => a.Id);
            builder.Property(a => a.Action).IsRequired().HasMaxLength(64);
            builder.Property(a => a.EntityType).IsRequired().HasMaxLength(64);
            builder.Property(a => a.Details).HasMaxLength(4000);
            builder.Property(a => a.IpAddress).HasMaxLength(45);
            builder.Property(a => a.RequestId).HasMaxLength(64);
            builder.HasIndex(a => a.OccurredAtUtc);
            builder.HasIndex(a => new { a.EntityType, a.EntityId });
            builder.HasIndex(a => new { a.UserId, a.OccurredAtUtc });
        }
    }
}
```

**1c.** Repository access:

- `SafyaClinic/SafyaClinic.Domain/Interfaces/Repositories/IUnitOfWork.cs` — add `using SafyaClinic.Domain.Entities.Audit;` at the top, and after the line `IRepository<PaymentAdjustment> PaymentAdjustments { get; }` add:

  ```csharp

      // ── Audit ─────────────────────────────────────────────────
      IRepository<AuditLog> AuditLogs { get; }
  ```

- `SafyaClinic/SafyaClinic.Infrastructure/Repositories/UnitOfWork.cs` — add `using SafyaClinic.Domain.Entities.Audit;`; after `private IRepository<PaymentAdjustment>? _paymentAdjustments;` add

  ```csharp
      private IRepository<AuditLog>? _auditLogs;
  ```

  and after the line `public IRepository<PaymentAdjustment> PaymentAdjustments => …;` add

  ```csharp
      public IRepository<AuditLog> AuditLogs => _auditLogs ??= new GenericRepository<AuditLog>(_context);
  ```

### Step 2 — The audit writer (Application)

Create `SafyaClinic/SafyaClinic.Application/Common/Auditing/Audit.cs`:

```csharp
using System.Text.Json;
using SafyaClinic.Domain.Entities.Audit;
using SafyaClinic.Domain.Interfaces.Repositories;

namespace SafyaClinic.Application.Common.Auditing;

/// <summary>Action names written to AuditLogs.Action (F3b).</summary>
public static class AuditActions
{
    public const string SignInSucceeded = "SignIn.Succeeded";
    public const string SignInFailed = "SignIn.Failed";
    public const string SignInLockedOut = "SignIn.LockedOut";
    public const string SignInRefusedWhileLocked = "SignIn.RefusedWhileLocked";
    public const string PasswordChanged = "User.PasswordChanged";
    public const string PasswordReset = "User.PasswordReset";
    public const string UserCreated = "User.Created";
    public const string UserActivated = "User.Activated";
    public const string UserDeactivated = "User.Deactivated";
    public const string RoleAssigned = "User.RoleAssigned";
    public const string RoleRemoved = "User.RoleRemoved";
}

/// <summary>Who is acting and from where — supplied by the web layer.</summary>
public interface IAuditContext
{
    int? UserId { get; }
    string? IpAddress { get; }
    string? RequestId { get; }
}

public interface IAuditTrail
{
    /// <summary>
    /// Adds an audit entry to the current unit of work. It is saved by the caller's next
    /// SaveChangesAsync, together with the change it describes.
    /// </summary>
    Task RecordAsync(string action, string entityType, int? entityId, object? details = null, int? actingUserId = null);
}

public sealed class AuditTrail : IAuditTrail
{
    private const int MaxDetailsLength = 4000;

    private readonly IUnitOfWork _uow;
    private readonly IAuditContext _context;

    public AuditTrail(IUnitOfWork uow, IAuditContext context)
    {
        _uow = uow;
        _context = context;
    }

    public Task RecordAsync(string action, string entityType, int? entityId, object? details = null, int? actingUserId = null)
    {
        var json = details is null ? null : JsonSerializer.Serialize(details);
        if (json is { Length: > MaxDetailsLength }) json = json[..MaxDetailsLength];

        return _uow.AuditLogs.AddAsync(new AuditLog
        {
            OccurredAtUtc = DateTime.UtcNow,
            UserId = actingUserId ?? _context.UserId,
            Action = action,
            EntityType = entityType,
            EntityId = entityId,
            Details = json,
            IpAddress = _context.IpAddress,
            RequestId = _context.RequestId
        });
    }
}

/// <summary>Used when no audit trail is supplied (some tests).</summary>
public sealed class NoAuditTrail : IAuditTrail
{
    public static readonly NoAuditTrail Instance = new();

    public Task RecordAsync(string action, string entityType, int? entityId, object? details = null, int? actingUserId = null) =>
        Task.CompletedTask;
}
```

### Step 3 — Record the events in `AuthService`

File: `SafyaClinic/SafyaClinic.Application/Services/AuthService.cs`.

**3a.** Add `using SafyaClinic.Application.Common.Auditing;`.

**3b.** Fields and constructor — replace

```csharp
    private readonly LockoutSettings _lockout;

    public AuthService(IUnitOfWork uow, ILogger<AuthService> logger, LockoutSettings lockout)
    {
        _uow = uow;
        _logger = logger;
        _lockout = lockout;
    }
```

with

```csharp
    private readonly LockoutSettings _lockout;
    private readonly IAuditTrail _audit;

    public AuthService(IUnitOfWork uow, ILogger<AuthService> logger, LockoutSettings lockout, IAuditTrail? audit = null)
    {
        _uow = uow;
        _logger = logger;
        _lockout = lockout;
        _audit = audit ?? NoAuditTrail.Instance;
    }
```

**3c.** Unknown account — replace

```csharp
            _logger.LogWarning("Sign-in failed: no active account for the given phone number.");
            return ServiceResult<LoginResponse>.Failure(InvalidCredentials);
```

with

```csharp
            _logger.LogWarning("Sign-in failed: no active account for the given phone number.");
            await _audit.RecordAsync(AuditActions.SignInFailed, "User", null, new { reason = "no active account" });
            await _uow.SaveChangesAsync();
            return ServiceResult<LoginResponse>.Failure(InvalidCredentials);
```

**3d.** Refused while locked — replace

```csharp
            var minutes = (int)Math.Ceiling((lockedUntil - now).TotalMinutes);
```

with

```csharp
            await _audit.RecordAsync(AuditActions.SignInRefusedWhileLocked, "User", user.Id, new { lockedUntilUtc = lockedUntil });
            await _uow.SaveChangesAsync();
            var minutes = (int)Math.Ceiling((lockedUntil - now).TotalMinutes);
```

**3e.** Success — replace

```csharp
        user.LastLoginAt = now;
```

with

```csharp
        user.LastLoginAt = now;
        await _audit.RecordAsync(AuditActions.SignInSucceeded, "User", user.Id, actingUserId: user.Id);
```

**3f.** Password changed — in `ChangePasswordAsync`, replace

```csharp
        user.SecurityStamp = SecurityStamps.New();   // ends every session, including this one
```

with

```csharp
        user.SecurityStamp = SecurityStamps.New();   // ends every session, including this one
        await _audit.RecordAsync(AuditActions.PasswordChanged, "User", user.Id, actingUserId: user.Id);
```

**3g.** Failed attempts and lockout — at the **very end of the file**, in `RecordFailedAttemptAsync` (the last method; the final `}` closes the class), replace

```csharp
        _uow.Users.Update(user);
        await _uow.SaveChangesAsync();
    }
}
```

with

```csharp
        await _audit.RecordAsync(
            user.LockoutEndUtc is not null && user.FailedLoginCount == 0 ? AuditActions.SignInLockedOut : AuditActions.SignInFailed,
            "User", user.Id,
            new { attempt = user.FailedLoginCount, lockedUntilUtc = user.LockoutEndUtc });

        _uow.Users.Update(user);
        await _uow.SaveChangesAsync();
    }
}
```

(Do **not** use `ChangePasswordAsync` — it has similar lines. The lockout branch resets the counter to 0 and sets the end time, which is how the two cases are told apart.)

### Step 4 — Record the events in `UserService`

File: `SafyaClinic/SafyaClinic.Application/Services/UserService.cs`.

**4a.** Add `using SafyaClinic.Application.Common.Auditing;` and replace

```csharp
    private readonly IUnitOfWork _uow;

    public UserService(IUnitOfWork uow) => _uow = uow;
```

with

```csharp
    private readonly IUnitOfWork _uow;
    private readonly IAuditTrail _audit;

    public UserService(IUnitOfWork uow, IAuditTrail? audit = null)
    {
        _uow = uow;
        _audit = audit ?? NoAuditTrail.Instance;
    }
```

**4b.** `CreateUserAsync` — find the `// Assign roles` comment and insert **directly above it**:

```csharp
        await _audit.RecordAsync(AuditActions.UserCreated, "User", user.Id, new { roleIds = requestedRoleIds }, createdByUserId);

```

(It is saved with the role assignments a few lines later.)

**4c.** `ResetPasswordAsync` — replace

```csharp
        user.SecurityStamp = SecurityStamps.New();   // ends the user's open sessions
```

with

```csharp
        user.SecurityStamp = SecurityStamps.New();   // ends the user's open sessions
        await _audit.RecordAsync(AuditActions.PasswordReset, "User", user.Id);
```

**4d.** `SetUserActiveAsync` — replace

```csharp
        user.SecurityStamp = SecurityStamps.New();   // a deactivated user's open sessions end
```

with

```csharp
        user.SecurityStamp = SecurityStamps.New();   // a deactivated user's open sessions end
        await _audit.RecordAsync(isActive ? AuditActions.UserActivated : AuditActions.UserDeactivated,
            "User", user.Id, actingUserId: actingUserId);
```

**4e.** `AssignRoleAsync` — replace

```csharp
        await RenewStampAsync(userId);   // the user signs in again and gets the new role
```

with

```csharp
        await RenewStampAsync(userId);   // the user signs in again and gets the new role
        await _audit.RecordAsync(AuditActions.RoleAssigned, "User", userId, new { roleId }, assignedBy);
```

**4f.** `RemoveRoleAsync` — replace

```csharp
        await RenewStampAsync(userId);   // the removed role stops working at once
```

with

```csharp
        await RenewStampAsync(userId);   // the removed role stops working at once
        await _audit.RecordAsync(AuditActions.RoleRemoved, "User", userId, new { roleId, role = userRole.Role.RoleName });
```

### Step 5 — Web: who and where

**5a.** Create `SafyaClinic/SafyaClinic.Web/Security/HttpAuditContext.cs`:

```csharp
using System.Security.Claims;
using SafyaClinic.Application.Common.Auditing;

namespace SafyaClinic.Web.Security;

/// <summary>Audit context from the current request: signed-in user, client IP, request id (F3b).</summary>
public sealed class HttpAuditContext : IAuditContext
{
    private readonly IHttpContextAccessor _accessor;

    public HttpAuditContext(IHttpContextAccessor accessor) => _accessor = accessor;

    private HttpContext? Http => _accessor.HttpContext;

    public int? UserId =>
        int.TryParse(Http?.User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;

    public string? IpAddress => Http?.Connection.RemoteIpAddress?.ToString();

    public string? RequestId => Http?.TraceIdentifier;
}
```

**5b.** `Program.cs` — add `using SafyaClinic.Application.Common.Auditing;`, and after the health-check registration (`builder.Services.AddHealthChecks()…;`) add:

```csharp

// ── Audit trail (F3b): saved together with the change it describes ──
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<IAuditContext, HttpAuditContext>();
builder.Services.AddScoped<IAuditTrail, AuditTrail>();
```

### Step 6 — Tests

Create `SafyaClinic/tests/SafyaClinic.Tests/Application/AuditTrailTests.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using SafyaClinic.Application.Common.Auditing;
using SafyaClinic.Application.DTOs.Auth;
using SafyaClinic.Application.DTOs.Patient;
using SafyaClinic.Application.Security;
using SafyaClinic.Application.Services;
using SafyaClinic.Domain.Entities.Audit;
using SafyaClinic.Domain.Identity;
using SafyaClinic.Infrastructure.Data;
using SafyaClinic.Infrastructure.Repositories;
using Xunit;

namespace SafyaClinic.Tests.Application;

/// <summary>F3b: security events are written to AuditLogs with who, what, where — never secrets.</summary>
public class AuditTrailTests
{
    private const string AdminPhone = "0100";
    private const string AdminPassword = "Audit-Test-Password-1";

    private sealed class FixedContext : IAuditContext
    {
        public int? UserId { get; init; }
        public string? IpAddress => "10.0.0.1";
        public string? RequestId => "REQ-1";
    }

    private static async Task<SafyaDbContext> SeededAsync()
    {
        var context = new SafyaDbContext(new DbContextOptionsBuilder<SafyaDbContext>()
            .UseInMemoryDatabase($"audit-{Guid.NewGuid()}").Options);
        await DbSeeder.SeedAsync(context, new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { [DbSeeder.AdminPasswordSettingKey] = AdminPassword })
            .Build());
        context.ChangeTracker.Clear();
        return context;
    }

    private static AuthService Auth(SafyaDbContext context, int? actingUserId = null)
    {
        context.ChangeTracker.Clear();   // each call is a new request in the app
        var uow = new UnitOfWork(context);
        return new AuthService(uow, NullLogger<AuthService>.Instance,
            new LockoutSettings { MaxFailedAttempts = 3, LockoutMinutes = 15 },
            new AuditTrail(uow, new FixedContext { UserId = actingUserId }));
    }

    private static UserService Users(SafyaDbContext context, int actingUserId)
    {
        context.ChangeTracker.Clear();
        var uow = new UnitOfWork(context);
        return new UserService(uow, new AuditTrail(uow, new FixedContext { UserId = actingUserId }));
    }

    private static Task<List<AuditLog>> EntriesAsync(SafyaDbContext context) =>
        context.AuditLogs.AsNoTracking().OrderBy(a => a.Id).ToListAsync();

    private static Task<int> AdminIdAsync(SafyaDbContext context) =>
        context.Users.Where(u => u.PhoneNumber == AdminPhone).Select(u => u.Id).SingleAsync();

    [Fact]
    public async Task Successful_sign_in_is_recorded_with_who_and_where()
    {
        await using var context = await SeededAsync();
        var adminId = await AdminIdAsync(context);

        await Auth(context).LoginAsync(new LoginRequest { PhoneNumber = AdminPhone, Password = AdminPassword });

        var entry = Assert.Single(await EntriesAsync(context));
        Assert.Equal(AuditActions.SignInSucceeded, entry.Action);
        Assert.Equal(adminId, entry.EntityId);
        Assert.Equal(adminId, entry.UserId);
        Assert.Equal("10.0.0.1", entry.IpAddress);
        Assert.Equal("REQ-1", entry.RequestId);
    }

    [Fact]
    public async Task Wrong_password_is_recorded_without_the_password()
    {
        await using var context = await SeededAsync();

        await Auth(context).LoginAsync(new LoginRequest { PhoneNumber = AdminPhone, Password = "wrong-password-xyz" });

        var entry = Assert.Single(await EntriesAsync(context));
        Assert.Equal(AuditActions.SignInFailed, entry.Action);
        Assert.Null(entry.UserId);
        Assert.DoesNotContain("wrong-password-xyz", entry.Details ?? "");
    }

    [Fact]
    public async Task Unknown_account_is_recorded_without_the_phone_number()
    {
        await using var context = await SeededAsync();

        await Auth(context).LoginAsync(new LoginRequest { PhoneNumber = "0999999", Password = "whatever-123" });

        var entry = Assert.Single(await EntriesAsync(context));
        Assert.Equal(AuditActions.SignInFailed, entry.Action);
        Assert.Null(entry.EntityId);
        Assert.DoesNotContain("0999999", entry.Details ?? "");
    }

    [Fact]
    public async Task Lockout_is_recorded()
    {
        await using var context = await SeededAsync();
        for (var i = 0; i < 3; i++)
            await Auth(context).LoginAsync(new LoginRequest { PhoneNumber = AdminPhone, Password = "wrong-password" });

        var actions = (await EntriesAsync(context)).Select(e => e.Action).ToList();

        Assert.Equal(new[] { AuditActions.SignInFailed, AuditActions.SignInFailed, AuditActions.SignInLockedOut }, actions);
    }

    [Fact]
    public async Task Deactivation_is_recorded_with_the_acting_admin()
    {
        await using var context = await SeededAsync();
        var adminId = await AdminIdAsync(context);
        var doctor = new User { FullName = "Doctor", PhoneNumber = "0301", PasswordHash = "x", SecurityStamp = "s" };
        context.Users.Add(doctor);
        await context.SaveChangesAsync();

        await Users(context, adminId).SetUserActiveAsync(doctor.Id, isActive: false, actingUserId: adminId);

        var entry = Assert.Single(await EntriesAsync(context));
        Assert.Equal(AuditActions.UserDeactivated, entry.Action);
        Assert.Equal(doctor.Id, entry.EntityId);
        Assert.Equal(adminId, entry.UserId);
    }

    [Fact]
    public async Task Password_reset_is_recorded_without_the_password()
    {
        await using var context = await SeededAsync();
        var adminId = await AdminIdAsync(context);

        await Users(context, adminId).ResetPasswordAsync(new ResetPasswordRequest
        {
            UserId = adminId, NewPassword = "Temporary-pass-9", ConfirmPassword = "Temporary-pass-9"
        });

        var entry = Assert.Single(await EntriesAsync(context));
        Assert.Equal(AuditActions.PasswordReset, entry.Action);
        Assert.DoesNotContain("Temporary-pass-9", entry.Details ?? "");
    }

    [Fact]
    public async Task Role_assignment_is_recorded()
    {
        await using var context = await SeededAsync();
        var adminId = await AdminIdAsync(context);
        var receptionRoleId = await context.Roles.Where(r => r.RoleName == "Reception").Select(r => r.Id).SingleAsync();

        await Users(context, adminId).AssignRoleAsync(adminId, receptionRoleId, adminId);

        var entry = Assert.Single(await EntriesAsync(context));
        Assert.Equal(AuditActions.RoleAssigned, entry.Action);
        Assert.Contains(receptionRoleId.ToString(), entry.Details);
    }
}
```

**Check:**

```powershell
dotnet build SafyaClinic\SafyaClinic.sln
dotnet test SafyaClinic\SafyaClinic.sln
```

Expected **Passed: 119** (112 + 7), Failed: 0. If anything fails, send me the output — do not change the tests to make them pass.

### Step 7 — Migration (needs your approval)

⚠ **Do not run the app until 7c.**

**7a.** Confirm the table is empty (read-only):

```powershell
$cfg = Get-Content SafyaClinic\SafyaClinic.Web\appsettings.json -Raw | ConvertFrom-Json
$conn = New-Object System.Data.SqlClient.SqlConnection $cfg.ConnectionStrings.SafyaClinicDb
$conn.Open(); $cmd = $conn.CreateCommand(); $cmd.CommandText = "SELECT COUNT(*) FROM AuditLogs"
"AuditLogs rows: " + $cmd.ExecuteScalar(); $conn.Close()
```

Expected `AuditLogs rows: 0`. If it is not 0, stop and tell me.

**7b.** Generate and check:

```powershell
Push-Location SafyaClinic\SafyaClinic.Web
dotnet ef migrations add ReshapeAuditLogs --project ..\SafyaClinic.Infrastructure
Pop-Location
```

In `…_ReshapeAuditLogs.cs`, **every** operation in `Up` must name `table: "AuditLogs"`:

- `DropColumn` for `TableName`, `RecordId`, `OldValues`, `NewValues`, `PerformedBy`, `PerformedAt`, `IPAddress`
- `AlterColumn` for `Action` (to `nvarchar(64)`)
- `AddColumn` for `OccurredAtUtc`, `UserId`, `EntityType`, `EntityId`, `Details`, `IpAddress`, `RequestId`
- `CreateIndex` × 3

(The exact order may differ; EF may also rename instead of drop+add for similar names — both fine.) Anything touching **another table** → stop and send me the file.

**7c.** Back up and start the app (it applies the migration in Development):

```powershell
powershell -ExecutionPolicy Bypass -File .assessment\backup-db.ps1
dotnet run --project SafyaClinic\SafyaClinic.Web --launch-profile http
```

Doing 7c is your explicit approval to reshape `AuditLogs` on the **dev** database. Test and demo get it through the deployment script (S8), where the same row-count check applies.

### Step 8 — Manual check

With the app running:

1. Sign out; sign in with a wrong password once, then correctly.
2. As Admin: deactivate and reactivate a test user; assign and remove a role; reset their password.
3. Read the trail (read-only):

   ```powershell
   $conn = New-Object System.Data.SqlClient.SqlConnection $cfg.ConnectionStrings.SafyaClinicDb
   $conn.Open(); $cmd = $conn.CreateCommand()
   $cmd.CommandText = "SELECT TOP 20 Id, OccurredAtUtc, UserId, Action, EntityType, EntityId, Details, IpAddress, RequestId FROM AuditLogs ORDER BY Id DESC"
   $r = $cmd.ExecuteReader(); $t = New-Object System.Data.DataTable; $t.Load($r); $t | Format-Table -AutoSize; $conn.Close()
   ```

   Each action appears once, with your user id, `::1` or `127.0.0.1` as IP, and a request id; no passwords anywhere.

### Step 9 — Commit, push, PR

```powershell
git add SafyaClinic/SafyaClinic.Domain SafyaClinic/SafyaClinic.Infrastructure SafyaClinic/SafyaClinic.Application SafyaClinic/SafyaClinic.Web SafyaClinic/tests Docs
git status
git commit -m "F3b-1: typed AuditLogs; audit trail saved with each change; sign-in, password and user-management events"
git push -u origin chore/f3b1-audit
```

Open the PR and send me the number and CI result. Do not merge before review.

## Acceptance criteria

- [ ] `AuditLogs` has the typed columns and indexes; the migration touches only that table.
- [ ] Each listed event writes exactly one row with action, entity, actor, time, IP and request id.
- [ ] No passwords, hashes or phone numbers in `Details`.
- [ ] A failed save writes neither the change nor its audit row (same unit of work).
- [ ] 119 tests pass locally and in CI.
