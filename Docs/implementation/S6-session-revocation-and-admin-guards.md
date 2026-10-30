# Slice S6 — Session revocation and admin guards

Roadmap item **S6** (Group 1). Fixes **F04** (and A3: admin lock-out risks).

| | |
|---|---|
| Branch | `chore/s6-sessions` (from an up-to-date `main`, after PR #8 is merged) |
| Behaviour change | 1. A **deactivated** user's open session ends within about a minute (next page they open after that). 2. A user is also signed out when their **roles change** or their **password is reset or changed**, so new permissions apply at their next sign-in. 3. After changing their own password a user signs in again with the new one. 4. An Admin **cannot deactivate themselves**, and nobody can deactivate or remove the Admin role from the **last active Admin**. 5. Activate/Deactivate now shows a success or error message. 6. Everyone is signed out **once** when this version is deployed (old cookies have no stamp) |
| Database | ⚠ **Adds 1 column** `Users.SecurityStamp` (`nvarchar(64)`, empty for existing users until their next sign-in). Additive only |
| Size | S |

## Design

| Decision | Choice |
|---|---|
| Revocation | Each user has a random **security stamp**, copied into the sign-in cookie. On requests, the cookie's stamp is compared with the database (only active users have one). Different or missing → the cookie is rejected and deleted |
| Cost | The database is checked at most **once per minute per signed-in browser**; the check time is stored in the cookie itself |
| Stamp renewed on | deactivate / activate, role assigned / removed, password reset by Admin, password changed by the user |
| Role changes | The simplest safe behaviour: the user is signed out and gets the new roles at the next sign-in (claims are not edited in place) |
| Own password change | Renews the stamp, so the user is signed out and asked to sign in with the new password (this also replaces S5's cookie re-issue code) |
| Admin guards | In `UserService`, so every caller gets them: no self-deactivation; the last active user with the `Admin` role cannot be deactivated or lose that role |

## Affected files

| Action | Path |
|---|---|
| Add | `SafyaClinic/SafyaClinic.Application/Security/SecurityStamps.cs` |
| Edit | `SafyaClinic/SafyaClinic.Domain/Identity/User.cs` |
| Edit | `SafyaClinic/SafyaClinic.Infrastructure/Data/Configurations/UserConfiguration.cs`, `Data/DbSeeder.cs` |
| Add | `SafyaClinic/SafyaClinic.Infrastructure/Migrations/<timestamp>_AddSecurityStamp.cs` (+ Designer, snapshot) — generated |
| Edit | `SafyaClinic/SafyaClinic.Application/DTOs/Auth/AuthDtos.cs` |
| Edit | `SafyaClinic/SafyaClinic.Application/Interfaces/Services/IAuthService.cs`, `IUserService.cs` |
| Edit | `SafyaClinic/SafyaClinic.Application/Services/AuthService.cs`, `UserService.cs` |
| Edit | `SafyaClinic/SafyaClinic.Web/Security/AuthClaims.cs` |
| Add | `SafyaClinic/SafyaClinic.Web/Security/SessionValidator.cs` |
| Edit | `SafyaClinic/SafyaClinic.Web/Program.cs` |
| Edit | `SafyaClinic/SafyaClinic.Web/Controllers/AuthController.cs`, `UsersController.cs` |
| Edit | `SafyaClinic/SafyaClinic.Web/Views/Auth/Login.cshtml`, `Views/Users/Index.cshtml` (F35) |
| Add | `SafyaClinic/tests/SafyaClinic.Tests/Web/Security/SessionValidatorTests.cs` |
| Add | `SafyaClinic/tests/SafyaClinic.Tests/Application/UserServiceAdminGuardTests.cs` |
| Edit | `SafyaClinic/tests/SafyaClinic.Tests/Application/AuthServiceLoginTests.cs` |
| Add | `Docs/implementation/S6-session-revocation-and-admin-guards.md` |

## Risks

| Risk | Mitigation |
|---|---|
| Users signed out unexpectedly | Happens only on the listed events, plus once at deployment; announce the one-time sign-out |
| Extra database reads | One primary-key read per signed-in browser per minute |
| Admin locks everyone out | Self-deactivation and last-Admin removal are blocked in the service (tests) |
| Migration does more than intended | Step 6 checks the generated file before anything runs; backup first |

## Rollback

Revert the PR's merge commit. The `SecurityStamp` column can stay (old code ignores it). Users get signed out once more (their cookies then carry an unknown claim, which old code simply ignores — no lock-out).

---

## Steps

PowerShell, from `D:\MyDevWork\clinic-MS`.

### Step 0 — Branch

```powershell
git switch main
git pull
git switch -c chore/s6-sessions
```

### Step 1 — Stamp on the user

**1a.** Create `SafyaClinic/SafyaClinic.Application/Security/SecurityStamps.cs`:

```csharp
namespace SafyaClinic.Application.Security;

/// <summary>Random value that changes whenever a user's sessions must end (S6).</summary>
public static class SecurityStamps
{
    public static string New() => Guid.NewGuid().ToString("N");
}
```

**1b.** `SafyaClinic/SafyaClinic.Domain/Identity/User.cs` — after the `MustChangePassword` line add:

```csharp
        public string SecurityStamp { get; set; } = string.Empty;   // changes end all sessions of this user
```

(No random default here on purpose: the seeded user in `SafyaDbContext` would get a new value every time a migration is generated.)

**1c.** `SafyaClinic/SafyaClinic.Infrastructure/Data/Configurations/UserConfiguration.cs` — after the `PasswordHash` line add:

```csharp
            builder.Property(u => u.SecurityStamp).IsRequired().HasMaxLength(64);
```

**1d.** `SafyaClinic/SafyaClinic.Infrastructure/Data/DbSeeder.cs` — in `SeedAdminAsync`, after `PasswordHash = PasswordHashing.Hash(initialPassword),` add:

```csharp
            SecurityStamp = SecurityStamps.New(),
```

### Step 2 — Application: stamp in sign-in, password change and user management

**2a.** `DTOs/Auth/AuthDtos.cs` — in `LoginResponse`, after `MustChangePassword`, add:

```csharp
    public string SecurityStamp { get; init; } = string.Empty;
```

**2b.** `Interfaces/Services/IAuthService.cs` — add inside the interface:

```csharp
    /// <summary>The user's current security stamp, or null if the user is missing or inactive (S6).</summary>
    Task<string?> GetActiveSecurityStampAsync(int userId);
```

**2c.** `Services/AuthService.cs`:

In `LoginAsync`, replace:

```csharp
        user.FailedLoginCount = 0;
        user.LockoutEndUtc = null;
        user.LastLoginAt = now;
```

with:

```csharp
        user.FailedLoginCount = 0;
        user.LockoutEndUtc = null;
        user.LastLoginAt = now;
        if (string.IsNullOrEmpty(user.SecurityStamp))
            user.SecurityStamp = SecurityStamps.New();   // users created before S6
```

and in the `new LoginResponse { … }`, after `MustChangePassword = user.MustChangePassword` add a comma and:

```csharp
            SecurityStamp = user.SecurityStamp
```

In `ChangePasswordAsync`, replace:

```csharp
        user.MustChangePassword = false;
```

with:

```csharp
        user.MustChangePassword = false;
        user.SecurityStamp = SecurityStamps.New();   // ends every session, including this one
```

Add this method after `GetCurrentUserAsync` (after its closing `}`):

```csharp

    public Task<string?> GetActiveSecurityStampAsync(int userId) =>
        _uow.Users
            .Query()
            .Where(u => u.Id == userId && u.IsActive)
            .Select(u => (string?)u.SecurityStamp)
            .FirstOrDefaultAsync();
```

**2d.** `Interfaces/Services/IUserService.cs` — replace

```csharp
    Task<ServiceResult> SetUserActiveAsync(int userId, bool isActive);
```

with

```csharp
    Task<ServiceResult> SetUserActiveAsync(int userId, bool isActive, int actingUserId);
```

**2e.** `Services/UserService.cs`:

(1) In `CreateUserAsync`, in `new User { … }`, after `PasswordHash = PasswordHashing.Hash(request.Password),` add:

```csharp
            SecurityStamp = SecurityStamps.New(),
```

(2) In `ResetPasswordAsync`, replace:

```csharp
        user.LockoutEndUtc = null;        // a reset also unlocks the account
```

with:

```csharp
        user.LockoutEndUtc = null;        // a reset also unlocks the account
        user.SecurityStamp = SecurityStamps.New();   // ends the user's open sessions
```

(3) Replace the whole `SetUserActiveAsync` method:

```csharp
    public async Task<ServiceResult> SetUserActiveAsync(int userId, bool isActive)
    {
        var affectedRows = await _uow.Users
            .Query()
            .Where(u => u.Id == userId)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(u => u.IsActive, isActive));

        return affectedRows == 0
            ? ServiceResult.Failure("User not found.")
            : ServiceResult.Success();
    }
```

with:

```csharp
    private const string AdminRoleName = "Admin";
    private const string LastAdminMessage =
        "This is the last active administrator. Give another active user the Admin role first.";

    public async Task<ServiceResult> SetUserActiveAsync(int userId, bool isActive, int actingUserId)
    {
        var user = await _uow.Users.GetByIdAsync(userId);
        if (user is null)
            return ServiceResult.Failure("User not found.");

        if (!isActive)
        {
            if (userId == actingUserId)
                return ServiceResult.Failure("You cannot deactivate your own account.");
            if (await IsLastActiveAdminAsync(userId))
                return ServiceResult.Failure(LastAdminMessage);
        }

        if (user.IsActive == isActive)
            return ServiceResult.Success(isActive ? "User is already active." : "User is already inactive.");

        user.IsActive = isActive;
        user.SecurityStamp = SecurityStamps.New();   // a deactivated user's open sessions end
        _uow.Users.Update(user);
        await _uow.SaveChangesAsync();

        return ServiceResult.Success(isActive ? "User activated." : "User deactivated and signed out.");
    }
```

(4) In `AssignRoleAsync`, replace the end of the method:

```csharp
            AssignedBy = assignedBy
        });
        await _uow.SaveChangesAsync();
        return ServiceResult.Success("Role assigned.");
```

with:

```csharp
            AssignedBy = assignedBy
        });
        await RenewStampAsync(userId);   // the user signs in again and gets the new role
        await _uow.SaveChangesAsync();
        return ServiceResult.Success("Role assigned. The user will be asked to sign in again.");
```

(5) Replace the whole `RemoveRoleAsync` method:

```csharp
    public async Task<ServiceResult> RemoveRoleAsync(int userId, int roleId)
    {
        var affectedRows = await _uow.UserRoles
            .Query()
            .Where(userRole => userRole.UserId == userId && userRole.RoleId == roleId)
            .ExecuteDeleteAsync();

        return affectedRows == 0
            ? ServiceResult.Failure("User does not have this role.")
            : ServiceResult.Success("Role removed.");
    }
```

with:

```csharp
    public async Task<ServiceResult> RemoveRoleAsync(int userId, int roleId)
    {
        var userRole = await _uow.UserRoles
            .Query(asNoTracking: false)
            .Include(ur => ur.Role)
            .FirstOrDefaultAsync(ur => ur.UserId == userId && ur.RoleId == roleId);

        if (userRole is null)
            return ServiceResult.Failure("User does not have this role.");

        if (userRole.Role.RoleName == AdminRoleName && await IsLastActiveAdminAsync(userId))
            return ServiceResult.Failure(LastAdminMessage);

        _uow.UserRoles.Delete(userRole);
        await RenewStampAsync(userId);   // the removed role stops working at once
        await _uow.SaveChangesAsync();
        return ServiceResult.Success("Role removed. The user will be asked to sign in again.");
    }

    private async Task<bool> IsLastActiveAdminAsync(int userId)
    {
        var activeAdminIds = await _uow.UserRoles
            .Query()
            .Where(ur => ur.Role.RoleName == AdminRoleName && ur.User.IsActive)
            .Select(ur => ur.UserId)
            .Distinct()
            .ToListAsync();

        return activeAdminIds.Count == 1 && activeAdminIds[0] == userId;
    }

    private async Task RenewStampAsync(int userId)
    {
        var user = await _uow.Users.GetByIdAsync(userId);
        if (user is null) return;

        user.SecurityStamp = SecurityStamps.New();
        _uow.Users.Update(user);
    }
```

### Step 3 — Web: validate the stamp

**3a.** Replace the whole `SafyaClinic/SafyaClinic.Web/Security/AuthClaims.cs` with:

```csharp
namespace SafyaClinic.Web.Security;

public static class AuthClaims
{
    /// <summary>Present (value "true") while the user must choose a new password (S5).</summary>
    public const string MustChangePassword = "safya:must_change_password";

    /// <summary>The user's security stamp at sign-in; a different stamp in the database ends the session (S6).</summary>
    public const string SecurityStamp = "safya:security_stamp";
}
```

**3b.** Create `SafyaClinic/SafyaClinic.Web/Security/SessionValidator.cs`:

```csharp
using System.Globalization;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using SafyaClinic.Application.Interfaces.Services;

namespace SafyaClinic.Web.Security;

/// <summary>
/// Ends a signed-in session when the user is deactivated or their security stamp has changed
/// (password change or reset, role change). The database is checked at most once per
/// <see cref="CheckInterval"/> for each browser session (S6).
/// </summary>
public static class SessionValidator
{
    public static readonly TimeSpan CheckInterval = TimeSpan.FromMinutes(1);
    public const string LastCheckKey = "safya.stamp_checked";

    public static async Task ValidateAsync(CookieValidatePrincipalContext context)
    {
        var now = DateTimeOffset.UtcNow;
        if (context.Properties.Items.TryGetValue(LastCheckKey, out var text)
            && DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var lastCheck)
            && now - lastCheck < CheckInterval)
        {
            return;
        }

        var stamp = context.Principal?.FindFirstValue(AuthClaims.SecurityStamp);
        if (!int.TryParse(context.Principal?.FindFirstValue(ClaimTypes.NameIdentifier), out var userId)
            || string.IsNullOrEmpty(stamp))
        {
            await RejectAsync(context);   // e.g. a cookie issued before S6
            return;
        }

        var auth = context.HttpContext.RequestServices.GetRequiredService<IAuthService>();
        var current = await auth.GetActiveSecurityStampAsync(userId);
        if (current != stamp)
        {
            await RejectAsync(context);   // deactivated, or stamp renewed
            return;
        }

        context.Properties.Items[LastCheckKey] = now.ToString("O", CultureInfo.InvariantCulture);
        context.ShouldRenew = true;      // store the check time in the cookie
    }

    private static async Task RejectAsync(CookieValidatePrincipalContext context)
    {
        context.RejectPrincipal();
        await context.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    }
}
```

**3c.** `Program.cs` — in `.AddCookie(options => { … })`, after the line

```csharp
        options.Cookie.SameSite  = SameSiteMode.Strict;
```

add:

```csharp
        options.Events.OnValidatePrincipal = SessionValidator.ValidateAsync;   // S6
```

(`using SafyaClinic.Web.Security;` is already there from S5.)

### Step 4 — Controllers and login page

**4a.** `Controllers/AuthController.cs`

In the `Login` POST, replace:

```csharp
            new(ClaimTypes.Name, data.FullName)
        };
```

with:

```csharp
            new(ClaimTypes.Name, data.FullName),
            new(AuthClaims.SecurityStamp, data.SecurityStamp)
        };
```

In the `ChangePassword` POST, replace **everything after** the `if (!result.IsSuccess) { … }` block — that is, the `// Re-issue the sign-in cookie…` block (it appears **twice** in the merged S5 code; remove both) and the final `return RedirectWithSuccess(…);` — with:

```csharp
        // The change renewed the security stamp, which ends this session too (S6).
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        TempData["Success"] = "Password changed. Sign in with your new password.";
        return RedirectToAction(nameof(Login));
```

**4b.** `Controllers/UsersController.cs` — replace the `ToggleActive` body:

```csharp
        await _userService.SetUserActiveAsync(id, isActive);
        return RedirectToAction(nameof(Index));
```

with:

```csharp
        var result = await _userService.SetUserActiveAsync(id, isActive, CurrentUserId);
        if (result.IsSuccess) Success(result.Message);
        else Error(result.Errors.FirstOrDefault() ?? "Could not change the user's status.");
        return RedirectToAction(nameof(Index));
```

**4b-2.** `Views/Users/Index.cshtml` — **F35 fix** (found while testing S6: *Activate* never worked). In the `ToggleActive` form replace

```cshtml
<input type="hidden" name="isActive" value="@(!u.IsActive)" />
```

with

```cshtml
<input type="hidden" name="isActive" value="@(u.IsActive ? "false" : "true")" />
```

Razor drops an attribute whose value is `false` and writes `value="value"` when it is `true`, so the form always posted "deactivate". A string value avoids that.

**4c.** `Views/Auth/Login.cshtml` — the sign-in page has no flash area (it uses the anonymous part of the layout). Directly **above** the line

```cshtml
            <form asp-controller="Auth" asp-action="Login" asp-route-returnUrl="@ViewBag.ReturnUrl" method="post">
```

add:

```cshtml
            @if (TempData["Success"] is string signInMessage)
            {
                <div class="alert alert-success py-2" role="status">@signInMessage</div>
            }
```

### Step 5 — Tests

**5a.** Create `SafyaClinic/tests/SafyaClinic.Tests/Web/Security/SessionValidatorTests.cs`:

```csharp
using System.Globalization;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using SafyaClinic.Application.DTOs.Auth;
using SafyaClinic.Application.DTOs.Common;
using SafyaClinic.Application.DTOs.Patient;
using SafyaClinic.Application.Interfaces.Services;
using SafyaClinic.Web.Security;
using Xunit;

namespace SafyaClinic.Tests.Web.Security;

/// <summary>S6 / F04: open sessions end when a user is deactivated or their stamp changes.</summary>
public class SessionValidatorTests
{
    private const int UserId = 7;

    private sealed class StampSource : IAuthService
    {
        public string? Stamp { get; set; }
        public int Calls { get; private set; }

        public Task<string?> GetActiveSecurityStampAsync(int userId)
        {
            Calls++;
            return Task.FromResult(userId == UserId ? Stamp : null);
        }

        public Task<ServiceResult<LoginResponse>> LoginAsync(LoginRequest request) => throw new NotSupportedException();
        public Task<ServiceResult> ChangePasswordAsync(int userId, ChangePasswordRequest request) => throw new NotSupportedException();
        public Task<ServiceResult<UserDto>> GetCurrentUserAsync(int userId) => throw new NotSupportedException();
    }

    private static CookieValidatePrincipalContext Context(StampSource source, string? stampClaim, DateTimeOffset? lastCheck = null)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme).AddCookie();
        services.AddSingleton<IAuthService>(source);
        var httpContext = new DefaultHttpContext { RequestServices = services.BuildServiceProvider() };

        var claims = new List<Claim> { new(ClaimTypes.NameIdentifier, UserId.ToString()) };
        if (stampClaim is not null) claims.Add(new Claim(AuthClaims.SecurityStamp, stampClaim));
        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme));

        var properties = new AuthenticationProperties();
        if (lastCheck is not null)
            properties.Items[SessionValidator.LastCheckKey] = lastCheck.Value.ToString("O", CultureInfo.InvariantCulture);

        var scheme = new AuthenticationScheme(CookieAuthenticationDefaults.AuthenticationScheme, null, typeof(CookieAuthenticationHandler));
        return new CookieValidatePrincipalContext(httpContext, scheme, new CookieAuthenticationOptions(),
            new AuthenticationTicket(principal, properties, scheme.Name));
    }

    [Fact]
    public async Task Matching_stamp_keeps_the_session_and_records_the_check()
    {
        var context = Context(new StampSource { Stamp = "abc" }, "abc");

        await SessionValidator.ValidateAsync(context);

        Assert.NotNull(context.Principal);
        Assert.True(context.ShouldRenew);
        Assert.True(context.Properties.Items.ContainsKey(SessionValidator.LastCheckKey));
    }

    [Fact]
    public async Task Changed_stamp_ends_the_session()
    {
        var context = Context(new StampSource { Stamp = "new" }, "old");

        await SessionValidator.ValidateAsync(context);

        Assert.Null(context.Principal);
    }

    [Fact]
    public async Task Deactivated_user_is_signed_out()
    {
        var context = Context(new StampSource { Stamp = null }, "abc");

        await SessionValidator.ValidateAsync(context);

        Assert.Null(context.Principal);
    }

    [Fact]
    public async Task Cookie_without_a_stamp_is_signed_out()
    {
        var context = Context(new StampSource { Stamp = "abc" }, stampClaim: null);

        await SessionValidator.ValidateAsync(context);

        Assert.Null(context.Principal);
    }

    [Fact]
    public async Task Recently_checked_session_does_not_hit_the_database()
    {
        var source = new StampSource { Stamp = "new" };
        var context = Context(source, "old", lastCheck: DateTimeOffset.UtcNow.AddSeconds(-10));

        await SessionValidator.ValidateAsync(context);

        Assert.Equal(0, source.Calls);
        Assert.NotNull(context.Principal);
    }

    [Fact]
    public async Task Check_runs_again_after_the_interval()
    {
        var source = new StampSource { Stamp = "new" };
        var context = Context(source, "old",
            lastCheck: DateTimeOffset.UtcNow - SessionValidator.CheckInterval - TimeSpan.FromSeconds(1));

        await SessionValidator.ValidateAsync(context);

        Assert.Equal(1, source.Calls);
        Assert.Null(context.Principal);
    }
}
```

**5b.** Create `SafyaClinic/tests/SafyaClinic.Tests/Application/UserServiceAdminGuardTests.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using SafyaClinic.Application.Services;
using SafyaClinic.Domain.Identity;
using SafyaClinic.Infrastructure.Data;
using SafyaClinic.Infrastructure.Repositories;
using Xunit;

namespace SafyaClinic.Tests.Application;

/// <summary>S6 / A3: no self-deactivation, the last Admin is protected, changes renew the stamp.</summary>
public class UserServiceAdminGuardTests
{
    private sealed record Setup(SafyaDbContext Context, int AdminId, int DoctorId, int AdminRoleId, int ReceptionRoleId);

    private static async Task<Setup> SeedAsync()
    {
        var context = new SafyaDbContext(new DbContextOptionsBuilder<SafyaDbContext>()
            .UseInMemoryDatabase($"guards-{Guid.NewGuid()}")
            .Options);
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { [DbSeeder.AdminPasswordSettingKey] = "Guard-Test-Password-1" })
            .Build();
        await DbSeeder.SeedAsync(context, configuration);

        var doctor = new User { FullName = "Doctor", PhoneNumber = "0199", PasswordHash = "x", SecurityStamp = "doctor-stamp" };
        context.Users.Add(doctor);
        await context.SaveChangesAsync();
        var doctorRoleId = await context.Roles.Where(r => r.RoleName == "Doctor").Select(r => r.Id).SingleAsync();
        context.UserRoles.Add(new UserRole { UserId = doctor.Id, RoleId = doctorRoleId });
        await context.SaveChangesAsync();

        var adminId = await context.Users.Where(u => u.PhoneNumber == "0100").Select(u => u.Id).SingleAsync();
        var adminRoleId = await context.Roles.Where(r => r.RoleName == "Admin").Select(r => r.Id).SingleAsync();
        var receptionRoleId = await context.Roles.Where(r => r.RoleName == "Reception").Select(r => r.Id).SingleAsync();
        context.ChangeTracker.Clear();
        return new Setup(context, adminId, doctor.Id, adminRoleId, receptionRoleId);
    }

    private static UserService Service(SafyaDbContext context)
    {
        context.ChangeTracker.Clear();   // each call is a new request in the app
        return new UserService(new UnitOfWork(context));
    }

    private static Task<User> Load(SafyaDbContext context, int id) =>
        context.Users.AsNoTracking().SingleAsync(u => u.Id == id);

    [Fact]
    public async Task Admin_cannot_deactivate_their_own_account()
    {
        var s = await SeedAsync();

        var result = await Service(s.Context).SetUserActiveAsync(s.AdminId, isActive: false, actingUserId: s.AdminId);

        Assert.False(result.IsSuccess);
        Assert.True((await Load(s.Context, s.AdminId)).IsActive);
    }

    [Fact]
    public async Task Last_active_admin_cannot_be_deactivated()
    {
        var s = await SeedAsync();

        var result = await Service(s.Context).SetUserActiveAsync(s.AdminId, isActive: false, actingUserId: s.DoctorId);

        Assert.False(result.IsSuccess);
        Assert.True((await Load(s.Context, s.AdminId)).IsActive);
    }

    [Fact]
    public async Task Deactivating_a_user_renews_their_stamp()
    {
        var s = await SeedAsync();

        var result = await Service(s.Context).SetUserActiveAsync(s.DoctorId, isActive: false, actingUserId: s.AdminId);

        Assert.True(result.IsSuccess);
        var doctor = await Load(s.Context, s.DoctorId);
        Assert.False(doctor.IsActive);
        Assert.NotEqual("doctor-stamp", doctor.SecurityStamp);
    }

    [Fact]
    public async Task Admin_role_cannot_be_removed_from_the_last_admin()
    {
        var s = await SeedAsync();

        var result = await Service(s.Context).RemoveRoleAsync(s.AdminId, s.AdminRoleId);

        Assert.False(result.IsSuccess);
        Assert.True(await s.Context.UserRoles.AnyAsync(ur => ur.UserId == s.AdminId && ur.RoleId == s.AdminRoleId));
    }

    [Fact]
    public async Task Admin_role_can_be_removed_when_another_active_admin_exists()
    {
        var s = await SeedAsync();
        await Service(s.Context).AssignRoleAsync(s.DoctorId, s.AdminRoleId, s.AdminId);

        var result = await Service(s.Context).RemoveRoleAsync(s.AdminId, s.AdminRoleId);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task Role_change_renews_the_stamp()
    {
        var s = await SeedAsync();

        var result = await Service(s.Context).AssignRoleAsync(s.DoctorId, s.ReceptionRoleId, s.AdminId);

        Assert.True(result.IsSuccess);
        Assert.NotEqual("doctor-stamp", (await Load(s.Context, s.DoctorId)).SecurityStamp);
    }
}
```

**5c.** `SafyaClinic/tests/SafyaClinic.Tests/Application/AuthServiceLoginTests.cs` — add these two tests at the end of the class (before its final `}`):

```csharp

    [Fact]
    public async Task Sign_in_returns_the_users_security_stamp()
    {
        await using var context = await SeededContextAsync();

        var result = await SignIn(context, AdminPassword);

        Assert.False(string.IsNullOrEmpty(result.Data!.SecurityStamp));
        Assert.Equal((await Admin(context)).SecurityStamp, result.Data.SecurityStamp);
    }

    [Fact]
    public async Task Password_change_renews_the_stamp()
    {
        await using var context = await SeededContextAsync();
        var before = await Admin(context);

        context.ChangeTracker.Clear();
        var change = await Auth(context).ChangePasswordAsync(before.Id,
            new ChangePasswordRequest { CurrentPassword = AdminPassword, NewPassword = "Brand-new-pass-1", ConfirmPassword = "Brand-new-pass-1" });

        Assert.True(change.IsSuccess);
        Assert.NotEqual(before.SecurityStamp, (await Admin(context)).SecurityStamp);
    }
```

**Check:**

```powershell
dotnet build SafyaClinic\SafyaClinic.sln
dotnet test SafyaClinic\SafyaClinic.sln
```

Expected **Passed: 76** (62 + 6 session + 6 guards + 2 sign-in), Failed: 0. If anything fails, send me the output — do not change the tests to make them pass.

### Step 6 — Database migration (needs your approval)

⚠ **Do not run the app until 6b is checked.**

**6a.** Generate:

```powershell
Push-Location SafyaClinic\SafyaClinic.Web
dotnet ef migrations add AddSecurityStamp --project ..\SafyaClinic.Infrastructure
Pop-Location
```

**6b.** In `…_AddSecurityStamp.cs`, `Up` must contain **only** an `AddColumn<string>` named `SecurityStamp` on `Users` with `type: "nvarchar(64)"`, `maxLength: 64`, `nullable: false`, `defaultValue: ""` (an `UpdateData` for seeded user `Id = 1` is acceptable), and `Down` only the matching `DropColumn`. Anything else → stop and send me the file.

**6c.** If it matches: backup, then start the app (it applies the migration):

```powershell
powershell -ExecutionPolicy Bypass -File .assessment\backup-db.ps1
dotnet run --project SafyaClinic\SafyaClinic.Web --launch-profile http
```

Doing 6c is your explicit approval to add this column to the **dev** database.

### Step 7 — Manual check

Use two browsers (or a normal and a private window). You need a test user (e.g. the one from S5) and the Admin.

1. **One-time sign-out.** If you were signed in before starting the new version, the first page load sends you to the sign-in page. Sign in as Admin.
2. **Deactivation ends a session.** Window B: sign in as the test user and open a page. Window A (Admin): deactivate that user → green message. Wait about a minute, click any link in window B → sign-in page; signing in is refused.
3. **Role change.** Reactivate the user, sign in as them in B. In A, assign them another role → within a minute B is sent to sign-in; after signing in the new role's menu appears.
4. **Own password change.** In B, Change Password → you are sent to the sign-in page with *"Password changed. Sign in with your new password."*; the new password works.
5. **Guards.** As Admin, press Deactivate on **your own** row → red *"You cannot deactivate your own account."* If you are the only Admin, removing the Admin role from yourself → red *"…last active administrator…"*.

Afterwards deactivate the test user.

### Step 8 — Commit, push, PR

```powershell
git add SafyaClinic/SafyaClinic.Application SafyaClinic/SafyaClinic.Domain SafyaClinic/SafyaClinic.Infrastructure SafyaClinic/SafyaClinic.Web SafyaClinic/tests Docs/implementation
git reset SafyaClinic/SafyaClinic.Web/SafyaClinic.Web.csproj.user
git status
git commit -m "S6: security stamp ends sessions on deactivation, role and password changes; protect self and last Admin"
git push -u origin chore/s6-sessions
```

Open the PR and send me the number and CI result. Do not merge before review.

---

## Hosted servers (when you next publish)

The app adds the column at start-up. Take a database backup first (test site first). Everyone is signed out once after publishing — tell users. Then repeat checks 2 and 5.

## Acceptance criteria

- [ ] A deactivated user's open session ends within about a minute; they cannot sign in.
- [ ] Role assignment/removal and password reset/change sign the user out; new roles apply at the next sign-in.
- [ ] An Admin cannot deactivate themselves; the last active Admin cannot be deactivated or lose the Admin role.
- [ ] Activate/Deactivate shows success/error feedback, and **Activate** works (F35).
- [ ] The migration only adds `Users.SecurityStamp`.
- [ ] 76 tests pass locally and in CI.
