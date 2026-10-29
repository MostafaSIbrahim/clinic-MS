# Slice S5 — Password storage and policy

Roadmap item **S5** (Group 1). Fixes **F03**.

| | |
|---|---|
| Branch | `chore/s5-passwords` (from an up-to-date `main`, after the S3 PR is merged) |
| Behaviour change | 1. Passwords are stored with PBKDF2 (ASP.NET Core Identity format) instead of one SHA-256 round. Existing users sign in as before; their stored hash is upgraded silently at that sign-in. 2. New passwords need **at least 10 characters** and a **confirmation** field (create user, change password, admin reset). 3. **5 wrong passwords lock the account for 15 minutes** (configurable); failed attempts are logged. 4. Admin **"Reset password"** sets a temporary password; the user must choose a new one at next sign-in; a reset also unlocks the account |
| Database | ⚠ **Adds 3 columns to `Users`** (`FailedLoginCount`, `LockoutEndUtc`, `MustChangePassword`) through a new EF migration. Additive only — no data is changed or removed. See Step 9 |
| Size | M |

## Design

| Decision | Choice |
|---|---|
| Hash format | ASP.NET Core Identity v3: PBKDF2-HMAC-SHA512, 100,000 iterations, 16-byte salt, 32-byte key. Implemented with .NET's built-in `Rfc2898DeriveBytes.Pbkdf2` (no new package); tests prove Identity's own `PasswordHasher` reads our hashes and we read its hashes, so a later move to Identity needs no migration |
| Old hashes | Still verified; on a successful sign-in the hash is replaced. Nobody has to reset |
| Lockout | Counter on the user. After `Security:Lockout:MaxFailedAttempts` (5) failures the account is locked for `LockoutMinutes` (15) and the counter restarts. During lockout even the correct password is refused with a clear message. Time-limited, so the only Admin can never be locked out permanently; an Admin reset also unlocks |
| Forced change | `MustChangePassword` on the user → a sign-in claim → the app sends that user to *Change Password* until they change it |
| Logging | Failed attempts and lockouts are logged with the **user id only** (no phone numbers or passwords) |
| Not changed | The demo admin password and the seeded admin are not forced to change (the public demo needs a known password) |

## Affected files

| Action | Path |
|---|---|
| Add | `SafyaClinic/SafyaClinic.Application/Security/PasswordHashing.cs` |
| Add | `SafyaClinic/SafyaClinic.Application/Security/LockoutSettings.cs` |
| Edit | `SafyaClinic/SafyaClinic.Domain/Identity/User.cs` |
| Edit | `SafyaClinic/SafyaClinic.Application/Services/AuthService.cs` (replaced) |
| Edit | `SafyaClinic/SafyaClinic.Application/Services/UserService.cs`, `Interfaces/Services/IUserService.cs` |
| Edit | `SafyaClinic/SafyaClinic.Application/DTOs/Auth/AuthDtos.cs`, `DTOs/Patient/PatientDtos.cs` |
| Edit | `SafyaClinic/SafyaClinic.Infrastructure/Data/DbSeeder.cs` |
| Add | `SafyaClinic/SafyaClinic.Infrastructure/Migrations/<timestamp>_AddPasswordLockout.cs` (+ Designer, snapshot) — generated |
| Add | `SafyaClinic/SafyaClinic.Web/Security/AuthClaims.cs` |
| Edit | `SafyaClinic/SafyaClinic.Web/Program.cs`, `appsettings.json` |
| Edit | `SafyaClinic/SafyaClinic.Web/Controllers/AuthController.cs`, `UsersController.cs` |
| Edit | `SafyaClinic/SafyaClinic.Web/Views/Auth/ChangePassword.cshtml` (replaced), `Views/Users/Create.cshtml`, `Views/Users/Index.cshtml` |
| Add | `SafyaClinic/SafyaClinic.Web/Views/Users/ResetPassword.cshtml` |
| Delete | `SafyaClinic/tests/SafyaClinic.Tests/Application/AuthServicePasswordTests.cs` (replaced by the next file) |
| Add | `SafyaClinic/tests/SafyaClinic.Tests/Application/PasswordHashingTests.cs` |
| Edit | `SafyaClinic/tests/SafyaClinic.Tests/Application/AuthServiceLoginTests.cs` (replaced), `Infrastructure/DbSeederTests.cs` |
| Add | `Docs/implementation/S5-password-storage-and-policy.md` |

## Risks

| Risk | Mitigation |
|---|---|
| Existing users cannot sign in | Old format still verified (test); upgrade happens only after a successful check; manual sign-in with an existing user |
| Migration does more than intended (history drift, F19) | Step 9 checks the generated file before anything runs; database backup first |
| Lockout used to block a colleague | Time-limited (15 min); Admin reset unlocks; attempts logged |
| Hosted databases | The app applies migrations at start-up (existing behaviour), so publishing adds the 3 columns there too — additive, existing rows get `0 / NULL / false` |

## Rollback

Revert the PR's merge commit. The three extra columns can stay (the old code ignores them). Hashes already upgraded to the new format would **not** verify with the old code, so after a rollback those users need an admin to set their password again — prefer fixing forward. To remove the columns as well: `dotnet ef database update <previous migration name>` (ask me first; that is a destructive operation).

---

## Steps

PowerShell, from `D:\MyDevWork\clinic-MS`.

### Step 0 — Branch

```powershell
git switch main
git pull
git switch -c chore/s5-passwords
```

### Step 1 — Hashing and lockout settings (Application)

**1a.** Create `SafyaClinic/SafyaClinic.Application/Security/PasswordHashing.cs`:

```csharp
using System.Security.Cryptography;
using System.Text;

namespace SafyaClinic.Application.Security;

/// <summary>
/// Password hashing in the ASP.NET Core Identity v3 format (PBKDF2-HMAC-SHA512,
/// 100,000 iterations, 128-bit salt, 256-bit key), so stored hashes stay compatible
/// with Identity's PasswordHasher if the app moves to it later (slice S5).
/// Also verifies the old "salt.sha256" format so existing users can still sign in;
/// <see cref="PasswordCheck.SuccessRehashNeeded"/> tells the caller to store a new hash.
/// </summary>
public static class PasswordHashing
{
    public const int MinimumLength = 10;

    private const byte FormatMarker = 0x01;     // Identity v3
    private const uint PrfSha512 = 2;           // KeyDerivationPrf.HMACSHA512
    private const int Iterations = 100_000;
    private const int SaltSize = 16;
    private const int KeySize = 32;
    private const int HeaderSize = 13;          // marker + prf + iterations + salt length

    public static string Hash(string password)
    {
        ArgumentNullException.ThrowIfNull(password);

        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var key = Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations, HashAlgorithmName.SHA512, KeySize);

        var output = new byte[HeaderSize + SaltSize + KeySize];
        output[0] = FormatMarker;
        WriteUInt32(output, 1, PrfSha512);
        WriteUInt32(output, 5, Iterations);
        WriteUInt32(output, 9, SaltSize);
        salt.CopyTo(output, HeaderSize);
        key.CopyTo(output, HeaderSize + SaltSize);
        return Convert.ToBase64String(output);
    }

    public static PasswordCheck Verify(string? password, string? storedHash)
    {
        if (string.IsNullOrEmpty(password) || string.IsNullOrEmpty(storedHash))
            return PasswordCheck.Failed;

        // Base64 never contains '.', the old format always does.
        if (storedHash.Contains('.'))
            return VerifyLegacy(password, storedHash) ? PasswordCheck.SuccessRehashNeeded : PasswordCheck.Failed;

        try
        {
            var bytes = Convert.FromBase64String(storedHash);
            if (bytes.Length < HeaderSize || bytes[0] != FormatMarker) return PasswordCheck.Failed;

            var prf = ReadUInt32(bytes, 1);
            var iterations = (int)ReadUInt32(bytes, 5);
            var saltLength = (int)ReadUInt32(bytes, 9);
            HashAlgorithmName? algorithm = prf switch
            {
                0 => HashAlgorithmName.SHA1,
                1 => HashAlgorithmName.SHA256,
                2 => HashAlgorithmName.SHA512,
                _ => null
            };
            if (algorithm is null || iterations <= 0 || saltLength < 16) return PasswordCheck.Failed;

            var keyLength = bytes.Length - HeaderSize - saltLength;
            if (keyLength < 16) return PasswordCheck.Failed;

            var salt = bytes.AsSpan(HeaderSize, saltLength).ToArray();
            var expected = bytes.AsSpan(HeaderSize + saltLength, keyLength).ToArray();
            var actual = Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations, algorithm.Value, keyLength);

            if (!CryptographicOperations.FixedTimeEquals(actual, expected)) return PasswordCheck.Failed;

            return prf == PrfSha512 && iterations >= Iterations
                ? PasswordCheck.Success
                : PasswordCheck.SuccessRehashNeeded;
        }
        catch (FormatException)
        {
            return PasswordCheck.Failed;
        }
    }

    /// <summary>Returns a message for the user, or null when the new password is acceptable.</summary>
    public static string? Validate(string? password, string? confirmation)
    {
        if (string.IsNullOrEmpty(password) || password.Length < MinimumLength)
            return $"The password must be at least {MinimumLength} characters.";
        if (password != confirmation)
            return "The password and its confirmation do not match.";
        return null;
    }

    // Format used before S5: base64(salt) + "." + base64(SHA-256(password + salt)).
    private static bool VerifyLegacy(string password, string storedHash)
    {
        var parts = storedHash.Split('.');
        if (parts.Length != 2) return false;
        try
        {
            var salt = Convert.FromBase64String(parts[0]);
            var expected = Convert.FromBase64String(parts[1]);
            var actual = SHA256.HashData(Encoding.UTF8.GetBytes(password).Concat(salt).ToArray());
            return CryptographicOperations.FixedTimeEquals(actual, expected);
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private static void WriteUInt32(byte[] buffer, int offset, uint value)
    {
        buffer[offset] = (byte)(value >> 24);
        buffer[offset + 1] = (byte)(value >> 16);
        buffer[offset + 2] = (byte)(value >> 8);
        buffer[offset + 3] = (byte)value;
    }

    private static uint ReadUInt32(byte[] buffer, int offset) =>
        (uint)(buffer[offset] << 24 | buffer[offset + 1] << 16 | buffer[offset + 2] << 8 | buffer[offset + 3]);
}

public enum PasswordCheck
{
    Failed,
    Success,
    SuccessRehashNeeded
}
```

**1b.** Create `SafyaClinic/SafyaClinic.Application/Security/LockoutSettings.cs`:

```csharp
namespace SafyaClinic.Application.Security;

/// <summary>Sign-in lockout (S5). Bound from configuration section "Security:Lockout".</summary>
public sealed class LockoutSettings
{
    public const string SectionName = "Security:Lockout";

    public int MaxFailedAttempts { get; set; } = 5;
    public int LockoutMinutes { get; set; } = 15;
}
```

### Step 2 — User fields (Domain)

In `SafyaClinic/SafyaClinic.Domain/Identity/User.cs`, after the line

```csharp
        public DateTime? LastLoginAt { get; set; }
```

add:

```csharp
        public int FailedLoginCount { get; set; }            // consecutive failures since the last success or lockout
        public DateTime? LockoutEndUtc { get; set; }         // sign-in refused until this time
        public bool MustChangePassword { get; set; }         // set by an admin reset
```

### Step 3 — DTOs

**3a.** `SafyaClinic/SafyaClinic.Application/DTOs/Auth/AuthDtos.cs`:

In `LoginResponse`, after the `Roles` line, add:

```csharp
    public bool MustChangePassword { get; init; }
```

In `ChangePasswordRequest`, after `NewPassword`, add:

```csharp
    public string ConfirmPassword { get; init; } = string.Empty;
```

**3b.** `SafyaClinic/SafyaClinic.Application/DTOs/Patient/PatientDtos.cs`:

In `CreateUserRequest`, after the `Password` line, add:

```csharp
    public string ConfirmPassword { get; init; } = string.Empty;
```

After the closing `}` of `CreateUserRequest`, add:

```csharp

public class ResetPasswordRequest
{
    public int UserId { get; init; }
    public string NewPassword { get; init; } = string.Empty;
    public string ConfirmPassword { get; init; } = string.Empty;
}
```

### Step 4 — `AuthService` (replace the whole file)

Replace the entire content of `SafyaClinic/SafyaClinic.Application/Services/AuthService.cs` with:

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SafyaClinic.Application.DTOs.Auth;
using SafyaClinic.Application.DTOs.Common;
using SafyaClinic.Application.DTOs.Patient;
using SafyaClinic.Application.Interfaces.Services;
using SafyaClinic.Application.Security;
using SafyaClinic.Domain.Identity;
using SafyaClinic.Domain.Interfaces.Repositories;

namespace SafyaClinic.Application.Services;

public class AuthService : IAuthService
{
    private const string InvalidCredentials = "Invalid phone number or password.";

    private readonly IUnitOfWork _uow;
    private readonly ILogger<AuthService> _logger;
    private readonly LockoutSettings _lockout;

    public AuthService(IUnitOfWork uow, ILogger<AuthService> logger, LockoutSettings lockout)
    {
        _uow = uow;
        _logger = logger;
        _lockout = lockout;
    }

    public async Task<ServiceResult<LoginResponse>> LoginAsync(LoginRequest request)
    {
        var user = await _uow.Users.FirstOrDefaultAsync(
            u => u.PhoneNumber == request.PhoneNumber && u.IsActive);

        if (user is null)
        {
            _logger.LogWarning("Sign-in failed: no active account for the given phone number.");
            return ServiceResult<LoginResponse>.Failure(InvalidCredentials);
        }

        var now = DateTime.UtcNow;
        if (user.LockoutEndUtc is { } lockedUntil && lockedUntil > now)
        {
            _logger.LogWarning("Sign-in refused for user {UserId}: locked until {LockoutEndUtc:u}.", user.Id, lockedUntil);
            var minutes = (int)Math.Ceiling((lockedUntil - now).TotalMinutes);
            return ServiceResult<LoginResponse>.Failure(
                $"This account is locked after too many failed sign-in attempts. Try again in {minutes} minute(s).");
        }

        var check = PasswordHashing.Verify(request.Password, user.PasswordHash);
        if (check == PasswordCheck.Failed)
        {
            await RecordFailedAttemptAsync(user, now);
            return ServiceResult<LoginResponse>.Failure(InvalidCredentials);
        }

        // Old-format or weaker hash: store a current one now that we know the password.
        if (check == PasswordCheck.SuccessRehashNeeded)
            user.PasswordHash = PasswordHashing.Hash(request.Password);

        user.FailedLoginCount = 0;
        user.LockoutEndUtc = null;
        user.LastLoginAt = now;
        _uow.Users.Update(user);
        await _uow.SaveChangesAsync();

        var roleNames = await _uow.UserRoles
            .Query()
            .Where(ur => ur.UserId == user.Id)
            .OrderBy(ur => ur.RoleId)
            .Select(ur => ur.Role.RoleName)
            .ToListAsync();

        return ServiceResult<LoginResponse>.Success(new LoginResponse
        {
            UserId = user.Id,
            FullName = user.FullName,
            ExpiresAt = now.AddHours(8),
            Roles = roleNames,
            MustChangePassword = user.MustChangePassword
        });
    }

    public async Task<ServiceResult> ChangePasswordAsync(int userId, ChangePasswordRequest request)
    {
        var user = await _uow.Users.GetByIdAsync(userId);
        if (user is null)
            return ServiceResult.Failure("User not found.");

        if (PasswordHashing.Verify(request.CurrentPassword, user.PasswordHash) == PasswordCheck.Failed)
            return ServiceResult.Failure("Current password is incorrect.");

        var problem = PasswordHashing.Validate(request.NewPassword, request.ConfirmPassword);
        if (problem is not null)
            return ServiceResult.Failure(problem);

        if (request.NewPassword == request.CurrentPassword)
            return ServiceResult.Failure("Choose a password that is different from the current one.");

        user.PasswordHash = PasswordHashing.Hash(request.NewPassword);
        user.MustChangePassword = false;
        _uow.Users.Update(user);
        await _uow.SaveChangesAsync();

        _logger.LogInformation("User {UserId} changed their password.", user.Id);
        return ServiceResult.Success("Password changed successfully.");
    }

    public async Task<ServiceResult<UserDto>> GetCurrentUserAsync(int userId)
    {
        var user = await _uow.Users
            .Query()
            .Where(u => u.Id == userId)
            .Select(u => new UserDto
            {
                Id = u.Id,
                FullName = u.FullName,
                Email = u.Email,
                PhoneNumber = u.PhoneNumber,
                Specialization = u.Specialization,
                IsActive = u.IsActive,
                LastLoginAt = u.LastLoginAt,
                Roles = u.UserRoles
                    .OrderBy(ur => ur.RoleId)
                    .Select(ur => ur.Role.RoleName)
            })
            .FirstOrDefaultAsync();

        if (user is null)
            return ServiceResult<UserDto>.Failure("User not found.");

        return ServiceResult<UserDto>.Success(user);
    }

    private async Task RecordFailedAttemptAsync(User user, DateTime now)
    {
        user.FailedLoginCount++;
        if (user.FailedLoginCount >= _lockout.MaxFailedAttempts)
        {
            user.FailedLoginCount = 0;
            user.LockoutEndUtc = now.AddMinutes(_lockout.LockoutMinutes);
            _logger.LogWarning("Sign-in failed for user {UserId}; account locked until {LockoutEndUtc:u}.",
                user.Id, user.LockoutEndUtc);
        }
        else
        {
            _logger.LogWarning("Sign-in failed for user {UserId} (attempt {Attempt} of {MaxAttempts}).",
                user.Id, user.FailedLoginCount, _lockout.MaxFailedAttempts);
        }

        _uow.Users.Update(user);
        await _uow.SaveChangesAsync();
    }
}
```

(`HashPassword` / `VerifyPassword` are gone; Step 5 updates their two callers.)

### Step 5 — Users: create, reset; seeder

**5a.** `SafyaClinic/SafyaClinic.Application/Services/UserService.cs`

Add with the `using` lines:

```csharp
using SafyaClinic.Application.Security;
```

At the very start of `CreateUserAsync` (before `// Phone uniqueness check`) add:

```csharp
        var passwordProblem = PasswordHashing.Validate(request.Password, request.ConfirmPassword);
        if (passwordProblem is not null)
            return ServiceResult<UserDto>.Failure(passwordProblem);

```

Replace `PasswordHash = AuthService.HashPassword(request.Password),` with:

```csharp
            PasswordHash = PasswordHashing.Hash(request.Password),
```

Add this method after `CreateUserAsync` (after its closing `}`):

```csharp

    public async Task<ServiceResult> ResetPasswordAsync(ResetPasswordRequest request)
    {
        var problem = PasswordHashing.Validate(request.NewPassword, request.ConfirmPassword);
        if (problem is not null)
            return ServiceResult.Failure(problem);

        var user = await _uow.Users.GetByIdAsync(request.UserId);
        if (user is null)
            return ServiceResult.Failure("User not found.");

        user.PasswordHash = PasswordHashing.Hash(request.NewPassword);
        user.MustChangePassword = true;   // the user picks their own password at next sign-in
        user.FailedLoginCount = 0;
        user.LockoutEndUtc = null;        // a reset also unlocks the account
        _uow.Users.Update(user);
        await _uow.SaveChangesAsync();

        return ServiceResult.Success("Password reset. The user must choose a new password at next sign-in.");
    }
```

**5b.** `SafyaClinic/SafyaClinic.Application/Interfaces/Services/IUserService.cs` — after the `CreateUserAsync` line add:

```csharp
    Task<ServiceResult> ResetPasswordAsync(ResetPasswordRequest request);
```

**5c.** `SafyaClinic/SafyaClinic.Infrastructure/Data/DbSeeder.cs` — add `using SafyaClinic.Application.Security;` with the `using` lines, and replace

```csharp
            PasswordHash = AuthService.HashPassword(initialPassword),
```

with

```csharp
            PasswordHash = PasswordHashing.Hash(initialPassword),
```

### Step 6 — Web: settings, forced change, controllers

**6a.** Create `SafyaClinic/SafyaClinic.Web/Security/AuthClaims.cs`:

```csharp
namespace SafyaClinic.Web.Security;

public static class AuthClaims
{
    /// <summary>Present (value "true") while the user must choose a new password (S5).</summary>
    public const string MustChangePassword = "safya:must_change_password";
}
```

**6b.** `Program.cs` — add with the `using` lines:

```csharp
using SafyaClinic.Application.Security;
using SafyaClinic.Web.Security;
```

After the line `builder.Services.AddSingleton<IFileStorage>(new LocalFileStorage(storageRoot, maxUploadBytes));` add:

```csharp

// ── Sign-in lockout (S5) ─────────────────────────────────────
builder.Services.AddSingleton(
    builder.Configuration.GetSection(LockoutSettings.SectionName).Get<LockoutSettings>() ?? new LockoutSettings());
```

Replace:

```csharp
app.UseAuthorization();
```

with:

```csharp
app.UseAuthorization();

// A user whose password was reset must choose a new one before using the app (S5).
app.Use(async (context, next) =>
{
    if (context.User.HasClaim(AuthClaims.MustChangePassword, "true")
        && !context.Request.Path.StartsWithSegments("/Auth")
        && !context.Request.Path.StartsWithSegments("/Home/Error"))
    {
        context.Response.Redirect("/Auth/ChangePassword");
        return;
    }
    await next();
});
```

**6c.** `appsettings.json` — after the `"FileStorage": { … },` block add:

```json
  "Security": {
    "Lockout": {
      "MaxFailedAttempts": 5,
      "LockoutMinutes": 15
    }
  },
```

**6d.** `Controllers/AuthController.cs`

Add with the `using` lines:

```csharp
using SafyaClinic.Web.Security;
```

In the `Login` POST, replace:

```csharp
        claims.AddRange(data.Roles.Select(r => new Claim(ClaimTypes.Role, r)));
```

with:

```csharp
        claims.AddRange(data.Roles.Select(r => new Claim(ClaimTypes.Role, r)));
        if (data.MustChangePassword)
            claims.Add(new Claim(AuthClaims.MustChangePassword, "true"));
```

and replace:

```csharp
        if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
            return Redirect(returnUrl);
```

with:

```csharp
        if (data.MustChangePassword)
            return RedirectToAction(nameof(ChangePassword));

        if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
            return Redirect(returnUrl);
```

In the `ChangePassword` POST, replace:

```csharp
        return RedirectWithSuccess("Password changed successfully.", "Index", "Dashboard");
```

with:

```csharp
        // Re-issue the sign-in cookie without the "must change password" marker.
        if (User.HasClaim(AuthClaims.MustChangePassword, "true"))
        {
            var identity = new ClaimsIdentity(
                User.Claims.Where(c => c.Type != AuthClaims.MustChangePassword),
                CookieAuthenticationDefaults.AuthenticationScheme);

            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                new ClaimsPrincipal(identity),
                new AuthenticationProperties { IsPersistent = true, ExpiresUtc = DateTimeOffset.UtcNow.AddHours(8) });
        }

        return RedirectWithSuccess("Password changed successfully.", "Index", "Dashboard");
```

**6e.** `Controllers/UsersController.cs` — add after the `Create` POST action (after its closing `}`):

```csharp

    [HttpGet]
    public async Task<IActionResult> ResetPassword(int id)
    {
        var user = await _userService.GetUserByIdAsync(id);
        if (!user.IsSuccess) return NotFound();

        ViewBag.UserName = user.Data!.FullName;
        return View(new ResetPasswordRequest { UserId = id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ResetPassword(ResetPasswordRequest model)
    {
        var result = await _userService.ResetPasswordAsync(model);
        if (!result.IsSuccess)
        {
            ViewBag.UserName = (await _userService.GetUserByIdAsync(model.UserId)).Data?.FullName;
            ApplyErrors(result);
            return View(model);
        }

        return RedirectWithSuccess(result.Message, nameof(Index));
    }
```

### Step 7 — Views

**7a.** Replace the whole `Views/Auth/ChangePassword.cshtml` with:

```cshtml
@model SafyaClinic.Application.DTOs.Auth.ChangePasswordRequest
@using SafyaClinic.Web.Security
@{
    ViewData["Title"] = "Change Password";
    var mustChange = User.HasClaim(AuthClaims.MustChangePassword, "true");
}

<div class="row justify-content-center">
    <div class="col-md-5">
        <div class="card">
            <div class="card-header"><i class="bi bi-key me-2" aria-hidden="true"></i>Change Password</div>
            <div class="card-body">
                @if (mustChange)
                {
                    <div class="alert alert-warning py-2" role="status">
                        Your password was reset by an administrator. Choose a new password to continue.
                    </div>
                }
                <form asp-controller="Auth" asp-action="ChangePassword" method="post">
                    @Html.AntiForgeryToken()
                    <div asp-validation-summary="ModelOnly" class="alert alert-danger py-2" role="alert"></div>

                    <div class="mb-3">
                        <label asp-for="CurrentPassword" class="form-label">Current password</label>
                        <input asp-for="CurrentPassword" type="password" class="form-control" autocomplete="current-password" required />
                    </div>
                    <div class="mb-3">
                        <label asp-for="NewPassword" class="form-label">New password</label>
                        <input asp-for="NewPassword" type="password" class="form-control" autocomplete="new-password"
                               minlength="10" required aria-describedby="newPasswordHelp" />
                        <div id="newPasswordHelp" class="form-text">At least 10 characters.</div>
                    </div>
                    <div class="mb-3">
                        <label asp-for="ConfirmPassword" class="form-label">Confirm new password</label>
                        <input asp-for="ConfirmPassword" type="password" class="form-control" autocomplete="new-password" required />
                    </div>

                    <button type="submit" class="btn btn-primary">
                        <i class="bi bi-check-lg me-1" aria-hidden="true"></i>Update Password
                    </button>
                </form>
            </div>
        </div>
    </div>
</div>

@section Scripts {
    @{ await Html.RenderPartialAsync("_ValidationScriptsPartial"); }
}
```

**7b.** `Views/Users/Create.cshtml` — replace the password block:

```cshtml
                <div class="col-md-6">
                    <label asp-for="Password" class="form-label">Password *</label>
                    <input asp-for="Password" type="password" class="form-control" required />
                </div>
```

with:

```cshtml
                <div class="col-md-6">
                    <label asp-for="Password" class="form-label">Password *</label>
                    <input asp-for="Password" type="password" class="form-control" autocomplete="new-password"
                           minlength="10" required aria-describedby="passwordHelp" />
                    <div id="passwordHelp" class="form-text">At least 10 characters.</div>
                </div>
                <div class="col-md-6">
                    <label asp-for="ConfirmPassword" class="form-label">Confirm password *</label>
                    <input asp-for="ConfirmPassword" type="password" class="form-control" autocomplete="new-password" required />
                </div>
```

**7c.** `Views/Users/Index.cshtml` — find the **Activate / Deactivate** form (`<form asp-action="ToggleActive" …>`). Directly after its closing `</form>` (the first `</form>` after the `Activate` text) add:

```cshtml
                                    <a asp-action="ResetPassword" asp-route-id="@u.Id" class="btn btn-sm btn-outline-secondary ms-1">
                                        <i class="bi bi-key" aria-hidden="true"></i> Reset password
                                    </a>
```

**7d.** Create `Views/Users/ResetPassword.cshtml`:

```cshtml
@model ResetPasswordRequest
@{
    ViewData["Title"] = "Reset Password";
}

<div class="row justify-content-center">
    <div class="col-md-5">
        <div class="card">
            <div class="card-header"><i class="bi bi-key me-2" aria-hidden="true"></i>Reset password — @ViewBag.UserName</div>
            <div class="card-body">
                <p class="text-muted small">
                    Set a temporary password and give it to the user. They must choose their own password at their next sign-in.
                    This also unlocks the account.
                </p>
                <form asp-action="ResetPassword" method="post">
                    @Html.AntiForgeryToken()
                    <input type="hidden" asp-for="UserId" />
                    <div asp-validation-summary="ModelOnly" class="alert alert-danger py-2" role="alert"></div>

                    <div class="mb-3">
                        <label asp-for="NewPassword" class="form-label">Temporary password</label>
                        <input asp-for="NewPassword" type="password" class="form-control" autocomplete="new-password"
                               minlength="10" required aria-describedby="tempPasswordHelp" />
                        <div id="tempPasswordHelp" class="form-text">At least 10 characters.</div>
                    </div>
                    <div class="mb-3">
                        <label asp-for="ConfirmPassword" class="form-label">Confirm temporary password</label>
                        <input asp-for="ConfirmPassword" type="password" class="form-control" autocomplete="new-password" required />
                    </div>

                    <button type="submit" class="btn btn-primary"><i class="bi bi-check-lg me-1" aria-hidden="true"></i>Reset password</button>
                    <a asp-action="Index" class="btn btn-outline-secondary">Cancel</a>
                </form>
            </div>
        </div>
    </div>
</div>
```

### Step 8 — Tests

**8a.** Remove the old characterization tests (they called the removed helpers):

```powershell
git rm SafyaClinic/tests/SafyaClinic.Tests/Application/AuthServicePasswordTests.cs
```

**8b.** Create `SafyaClinic/tests/SafyaClinic.Tests/Application/PasswordHashingTests.cs`:

```csharp
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using SafyaClinic.Application.Security;
using Xunit;

namespace SafyaClinic.Tests.Application;

/// <summary>S5 / F03: Identity-compatible PBKDF2; old hashes still verify and get upgraded.</summary>
public class PasswordHashingTests
{
    private const string Password = "Correct-horse-1";

    [Fact]
    public void Hash_then_verify_succeeds()
    {
        Assert.Equal(PasswordCheck.Success, PasswordHashing.Verify(Password, PasswordHashing.Hash(Password)));
    }

    [Fact]
    public void Wrong_password_fails()
    {
        Assert.Equal(PasswordCheck.Failed, PasswordHashing.Verify("wrong-password", PasswordHashing.Hash(Password)));
    }

    [Theory]
    [InlineData("not-a-valid-hash")]
    [InlineData("")]
    [InlineData("AAAA")]
    [InlineData("x.y.z")]
    public void Invalid_stored_hash_fails(string storedHash)
    {
        Assert.Equal(PasswordCheck.Failed, PasswordHashing.Verify(Password, storedHash));
    }

    [Fact]
    public void Hashes_are_salted()
    {
        Assert.NotEqual(PasswordHashing.Hash(Password), PasswordHashing.Hash(Password));
    }

    [Fact]
    public void Old_format_verifies_and_asks_for_a_new_hash()
    {
        var salt = RandomNumberGenerator.GetBytes(16);
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(Password).Concat(salt).ToArray());
        var legacy = Convert.ToBase64String(salt) + "." + Convert.ToBase64String(hash);

        Assert.Equal(PasswordCheck.SuccessRehashNeeded, PasswordHashing.Verify(Password, legacy));
        Assert.Equal(PasswordCheck.Failed, PasswordHashing.Verify("wrong-password", legacy));
    }

    [Fact]
    public void Identity_password_hasher_accepts_our_hashes()
    {
        var identity = new PasswordHasher<object>();

        Assert.Equal(PasswordVerificationResult.Success,
            identity.VerifyHashedPassword(new object(), PasswordHashing.Hash(Password), Password));
    }

    [Fact]
    public void Identity_hashes_are_accepted_and_weaker_ones_are_upgraded()
    {
        var strong = new PasswordHasher<object>().HashPassword(new object(), Password);
        var weak = new PasswordHasher<object>(Options.Create(new PasswordHasherOptions { IterationCount = 10_000 }))
            .HashPassword(new object(), Password);

        Assert.Equal(PasswordCheck.Success, PasswordHashing.Verify(Password, strong));
        Assert.Equal(PasswordCheck.SuccessRehashNeeded, PasswordHashing.Verify(Password, weak));
    }

    [Theory]
    [InlineData("short", "short")]
    [InlineData("long-enough-1", "different-12")]
    [InlineData("", "")]
    public void Weak_or_mismatched_new_passwords_are_refused(string password, string confirmation)
    {
        Assert.NotNull(PasswordHashing.Validate(password, confirmation));
    }

    [Fact]
    public void Ten_character_matching_password_is_accepted()
    {
        Assert.Null(PasswordHashing.Validate("0123456789", "0123456789"));
    }
}
```

**8c.** Replace the whole `SafyaClinic/tests/SafyaClinic.Tests/Application/AuthServiceLoginTests.cs` with:

```csharp
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using SafyaClinic.Application.DTOs.Auth;
using SafyaClinic.Application.DTOs.Patient;
using SafyaClinic.Application.Security;
using SafyaClinic.Application.Services;
using SafyaClinic.Infrastructure.Data;
using SafyaClinic.Infrastructure.Repositories;
using Xunit;

namespace SafyaClinic.Tests.Application;

/// <summary>
/// S2: sign-in works with no Jwt settings. S5: lockout, hash upgrade, admin reset.
/// Uses the seeded administrator on an in-memory database.
/// </summary>
public class AuthServiceLoginTests
{
    private const string AdminPhone = "0100";
    private const string AdminPassword = "Login-Test-Password-1";
    private const int MaxAttempts = 3;

    private static async Task<SafyaDbContext> SeededContextAsync()
    {
        var context = new SafyaDbContext(new DbContextOptionsBuilder<SafyaDbContext>()
            .UseInMemoryDatabase($"login-{Guid.NewGuid()}")
            .Options);

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [DbSeeder.AdminPasswordSettingKey] = AdminPassword
            })
            .Build();

        await DbSeeder.SeedAsync(context, configuration);

        // A real request starts with an empty change tracker; the seeder left
        // its entities tracked, which would clash with the login's Update().
        context.ChangeTracker.Clear();
        return context;
    }

    private static AuthService Auth(SafyaDbContext context) =>
        new(new UnitOfWork(context), NullLogger<AuthService>.Instance,
            new LockoutSettings { MaxFailedAttempts = MaxAttempts, LockoutMinutes = 15 });

    // In the app every sign-in is a new request with a fresh DbContext; mimic that here.
    private static Task<SafyaClinic.Application.DTOs.Common.ServiceResult<LoginResponse>> SignIn(
        SafyaDbContext context, string password)
    {
        context.ChangeTracker.Clear();
        return Auth(context).LoginAsync(new LoginRequest { PhoneNumber = AdminPhone, Password = password });
    }

    private static Task<SafyaClinic.Domain.Identity.User> Admin(SafyaDbContext context) =>
        context.Users.AsNoTracking().SingleAsync(u => u.PhoneNumber == AdminPhone);

    [Fact]
    public async Task Admin_can_sign_in_without_jwt_configuration()
    {
        await using var context = await SeededContextAsync();

        var result = await SignIn(context, AdminPassword);

        Assert.True(result.IsSuccess);
        Assert.Contains("Admin", result.Data!.Roles);
        Assert.True(result.Data.ExpiresAt > DateTime.UtcNow);
        Assert.False(result.Data.MustChangePassword);
    }

    [Fact]
    public async Task Wrong_password_is_rejected()
    {
        await using var context = await SeededContextAsync();

        var result = await SignIn(context, "wrong-password");

        Assert.False(result.IsSuccess);
        Assert.Equal(1, (await Admin(context)).FailedLoginCount);
    }

    [Fact]
    public async Task Repeated_failures_lock_the_account_even_for_the_right_password()
    {
        await using var context = await SeededContextAsync();
        for (var i = 0; i < MaxAttempts; i++)
            await SignIn(context, "wrong-password");

        var result = await SignIn(context, AdminPassword);

        Assert.False(result.IsSuccess);
        Assert.Contains("locked", result.Errors.First());
        Assert.NotNull((await Admin(context)).LockoutEndUtc);
    }

    [Fact]
    public async Task Expired_lockout_allows_sign_in_and_clears_the_counter()
    {
        await using var context = await SeededContextAsync();
        var admin = await context.Users.SingleAsync(u => u.PhoneNumber == AdminPhone);
        admin.LockoutEndUtc = DateTime.UtcNow.AddMinutes(-1);
        admin.FailedLoginCount = 2;
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        var result = await SignIn(context, AdminPassword);

        Assert.True(result.IsSuccess);
        var stored = await Admin(context);
        Assert.Equal(0, stored.FailedLoginCount);
        Assert.Null(stored.LockoutEndUtc);
    }

    [Fact]
    public async Task Old_hash_is_upgraded_at_sign_in()
    {
        await using var context = await SeededContextAsync();
        var salt = RandomNumberGenerator.GetBytes(16);
        var legacy = Convert.ToBase64String(salt) + "." + Convert.ToBase64String(
            SHA256.HashData(Encoding.UTF8.GetBytes(AdminPassword).Concat(salt).ToArray()));
        var admin = await context.Users.SingleAsync(u => u.PhoneNumber == AdminPhone);
        admin.PasswordHash = legacy;
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        var result = await SignIn(context, AdminPassword);

        Assert.True(result.IsSuccess);
        var stored = await Admin(context);
        Assert.DoesNotContain(".", stored.PasswordHash);
        Assert.Equal(PasswordCheck.Success, PasswordHashing.Verify(AdminPassword, stored.PasswordHash));
    }

    [Fact]
    public async Task Admin_reset_forces_a_new_password_at_next_sign_in()
    {
        await using var context = await SeededContextAsync();
        var adminId = (await Admin(context)).Id;
        const string temporary = "Temporary-pass-1";
        const string chosen = "Brand-new-pass-1";

        var reset = await new UserService(new UnitOfWork(context)).ResetPasswordAsync(
            new ResetPasswordRequest { UserId = adminId, NewPassword = temporary, ConfirmPassword = temporary });
        context.ChangeTracker.Clear();
        var firstSignIn = await SignIn(context, temporary);
        var change = await Auth(context).ChangePasswordAsync(adminId,
            new ChangePasswordRequest { CurrentPassword = temporary, NewPassword = chosen, ConfirmPassword = chosen });
        var secondSignIn = await SignIn(context, chosen);

        Assert.True(reset.IsSuccess);
        Assert.True(firstSignIn.Data!.MustChangePassword);
        Assert.True(change.IsSuccess);
        Assert.False(secondSignIn.Data!.MustChangePassword);
    }

    [Fact]
    public async Task Short_new_password_is_refused()
    {
        await using var context = await SeededContextAsync();
        var adminId = (await Admin(context)).Id;

        var change = await Auth(context).ChangePasswordAsync(adminId,
            new ChangePasswordRequest { CurrentPassword = AdminPassword, NewPassword = "short", ConfirmPassword = "short" });

        Assert.False(change.IsSuccess);
    }
}
```

**8d.** `SafyaClinic/tests/SafyaClinic.Tests/Infrastructure/DbSeederTests.cs` — add `using SafyaClinic.Application.Security;` with the `using` lines, and replace

```csharp
        Assert.True(AuthService.VerifyPassword(TestPassword, admin.PasswordHash));
```

with

```csharp
        Assert.Equal(PasswordCheck.Success, PasswordHashing.Verify(TestPassword, admin.PasswordHash));
```

**Check (build and tests — no database needed yet):**

```powershell
dotnet build SafyaClinic\SafyaClinic.sln
dotnet test SafyaClinic\SafyaClinic.sln
```

Expected **Passed: 62** (47 − 4 removed + 14 hashing + 5 new sign-in), Failed: 0. If anything fails, send me the output — do not change the tests to make them pass.

### Step 9 — Database migration (needs your approval)

⚠ This is the first slice that changes the database schema. **Do not run the app until 9b is checked.**

**9a.** Generate the migration (this only writes files; it does not touch the database):

```powershell
Push-Location SafyaClinic\SafyaClinic.Web
dotnet tool restore
dotnet ef migrations add AddPasswordLockout --project ..\SafyaClinic.Infrastructure
Pop-Location
```

**9b.** Open `SafyaClinic\SafyaClinic.Infrastructure\Migrations\<timestamp>_AddPasswordLockout.cs`. The `Up` method must contain **only**:

- `AddColumn<int>` `FailedLoginCount` on `Users` (`nullable: false, defaultValue: 0`)
- `AddColumn<DateTime>` `LockoutEndUtc` on `Users` (`nullable: true`)
- `AddColumn<bool>` `MustChangePassword` on `Users` (`nullable: false, defaultValue: false`)
- *(acceptable)* an `UpdateData` for the seeded user `Id = 1` on `Users`

and `Down` only the three matching `DropColumn` calls (plus the matching `UpdateData`, if present). **If you see anything else** (other tables, `AlterColumn`, `DropIndex`, renames …) — stop and send me the file; that would be the history drift from F19.

**9c.** If 9b matches: back up the dev database, then apply by starting the app (it migrates at start-up):

```powershell
powershell -ExecutionPolicy Bypass -File .assessment\backup-db.ps1
dotnet run --project SafyaClinic\SafyaClinic.Web --launch-profile http
```

Doing 9c is your explicit approval to add these three columns to the **dev** database. The test and demo databases get them the next time you publish (Hosted servers, below).

### Step 10 — Manual check

With the app running (`http://localhost:5070`):

1. **Existing user still signs in** (e.g. your admin) → works. *(Their hash is upgraded silently.)* Sign out and in again → still works.
2. **Policy.** Users → Add User: a 9-character password, or a confirmation that differs → refused with a clear message. A 10+ character matching password → user created.
3. **Lockout.** Sign out; for that new user enter a wrong password 5 times → on the 6th try even the correct password shows *"This account is locked…"*. The PowerShell window shows `Sign-in failed for user {id}` warnings — no phone numbers or passwords.
4. **Reset unlocks and forces a change.** As Admin → Users → **Reset password** for that user → set a temporary password. Sign in as that user with it → you land on *Change Password* with the yellow notice; try opening `/Dashboard` → you are sent back. Change the password → Dashboard opens normally.
5. **Change password (normal).** As any user, Change Password with the wrong current password → refused; with a short new one → refused.

Afterwards deactivate the test user you created.

### Step 11 — Commit, push, PR

```powershell
git add SafyaClinic/SafyaClinic.Application SafyaClinic/SafyaClinic.Domain SafyaClinic/SafyaClinic.Infrastructure SafyaClinic/SafyaClinic.Web SafyaClinic/tests Docs/implementation
git reset SafyaClinic/SafyaClinic.Web/SafyaClinic.Web.csproj.user
git status
git commit -m "S5: PBKDF2 password hashing with upgrade on sign-in; 10-char policy with confirmation; lockout; admin reset forcing change"
git push -u origin chore/s5-passwords
```

`git status` should show the migration's three files (`…_AddPasswordLockout.cs`, `…Designer.cs`, `SafyaDbContextModelSnapshot.cs`). Open the PR, send me the number and CI result. Do not merge before review.

---

## Hosted servers (when you next publish)

The app adds the three columns itself at start-up. Before publishing, take a database backup from the MonsterASP panel (test site first). After publishing: sign in with an existing account (must work), and check that a reset user is sent to *Change Password*. The demo admin keeps its public password.

## Acceptance criteria

- [ ] Existing users sign in unchanged; their hash is upgraded to the new format at that sign-in.
- [ ] New/changed/reset passwords need ≥ 10 characters and a matching confirmation.
- [ ] 5 failed attempts lock the account for 15 minutes (configurable); the correct password is refused during lockout; it works after expiry; attempts are logged without personal data.
- [ ] Admin reset sets a temporary password, unlocks the account and forces a change before any other page opens.
- [ ] The migration only adds the three `Users` columns.
- [ ] 62 tests pass locally and in CI.
