# Slice S2 — Remove vulnerable and unused packages (and the unused JWT)

Roadmap item **S2** (Group 1 — immediate safety). Fixes F07 and A7; completes the JWT part of F02.

| | |
|---|---|
| Branch | `chore/s2-packages` (from an up-to-date `main`, after PR #4 is merged) |
| Behaviour change | **None for users.** Sign-in, sign-out and all role checks work exactly as before. A JWT that was created on every login and never used is no longer created, so the `Jwt:*` settings are no longer needed |
| Size | XS |

## Why

- `Microsoft.AspNetCore.Authentication.Cookies` **2.3.11** is an ASP.NET Core 2.x package. Cookie authentication is already part of .NET 8, so the reference is unnecessary — and it pulls in `System.Security.Cryptography.Xml` 8.0.2 (12 High advisories) and `.Pkcs` 8.0.1 (High + Moderate).
- `AuthService` signs a JWT on every login with `Jwt:Key` and nothing ever reads it; login even **fails** if the key is missing. Removing it removes a committed secret's only purpose.
- `FluentValidation` is referenced but has zero validators.

## Scope

1. Remove 3 package references from `SafyaClinic.Web` and 3 from `SafyaClinic.Application`.
2. Remove the JWT generator from `AuthService` and the `Token` property from `LoginResponse`.
3. Remove the `Jwt` section from `appsettings.json` and the demo template; update the demo guide.
4. Add 2 login tests that prove sign-in works **without** any `Jwt` configuration.
5. Make the CI vulnerable-package step **blocking**.

Out of scope: the hosted servers' own `appsettings.json` (they keep a `Jwt` section, which the app now simply ignores — remove it at your convenience); password hashing (S5).

## Affected files

| Action | Path |
|---|---|
| Edit | `SafyaClinic/SafyaClinic.Web/SafyaClinic.Web.csproj` |
| Edit | `SafyaClinic/SafyaClinic.Application/SafyaClinic.Application.csproj` |
| Edit | `SafyaClinic/SafyaClinic.Application/Services/AuthService.cs` |
| Edit | `SafyaClinic/SafyaClinic.Application/DTOs/Auth/AuthDtos.cs` |
| Edit | `SafyaClinic/SafyaClinic.Web/appsettings.json` |
| Edit | `SafyaClinic/docs/demo-appsettings.template.json` |
| Edit | `SafyaClinic/docs/MonsterASP-Demo-First-Publish.md` (steps 4–5 of the settings section) |
| Add | `SafyaClinic/tests/SafyaClinic.Tests/Application/AuthServiceLoginTests.cs` |
| Edit | `.github/workflows/ci.yml` |
| Add | `Docs/implementation/S2-remove-vulnerable-and-unused-packages.md` (this guide) |

## Risks

| Risk | Mitigation |
|---|---|
| Something else used one of the removed packages | Searched the whole solution: only `AuthService` uses the JWT types; nothing uses FluentValidation; cookie auth comes from the framework. The build will fail immediately if anything was missed |
| Sign-in breaks | New login tests run without any `Jwt` settings; local smoke test |

## Rollback

Revert the PR's merge commit.

---

## Steps

PowerShell, from `D:\MyDevWork\clinic-MS`.

### Step 0 — Branch

```powershell
git switch main
git pull
git switch -c chore/s2-packages
```

### Step 1 — Remove package references

**1a.** `SafyaClinic/SafyaClinic.Web/SafyaClinic.Web.csproj` — delete these three lines:

```xml
    <PackageReference Include="Microsoft.AspNetCore.Authentication.Cookies" Version="2.3.11" />
    <PackageReference Include="Microsoft.IdentityModel.Tokens" Version="8.19.1" />
    <PackageReference Include="System.IdentityModel.Tokens.Jwt" Version="8.19.1" />
```

Keep the `Microsoft.EntityFrameworkCore.Design` reference in the same item group.

**1b.** `SafyaClinic/SafyaClinic.Application/SafyaClinic.Application.csproj` — delete these three lines:

```xml
    <PackageReference Include="FluentValidation" Version="11.8.0" />
    <PackageReference Include="Microsoft.IdentityModel.JsonWebTokens" Version="8.19.1" />
    <PackageReference Include="System.IdentityModel.Tokens.Jwt" Version="8.19.1" />
```

### Step 2 — Remove the JWT from `AuthService`

File: `SafyaClinic/SafyaClinic.Application/Services/AuthService.cs`.

**2a.** Delete these five `using` lines (keep `using System.Text;` — the password helpers use it):

```csharp
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
```

**2b.** Replace the field and constructor:

```csharp
    private readonly IUnitOfWork _uow;
    private readonly IConfiguration _config;

    public AuthService(IUnitOfWork uow, IConfiguration config)
    {
        _uow = uow;
        _config = config;
    }
```

with:

```csharp
    private readonly IUnitOfWork _uow;

    public AuthService(IUnitOfWork uow)
    {
        _uow = uow;
    }
```

**2c.** In `LoginAsync`, delete the line

```csharp
        var token = GenerateJwtToken(user.Id, user.FullName, roleNames, expiry);
```

and, inside `new LoginResponse { … }`, delete the line

```csharp
            Token = token,
```

Keep `var expiry = DateTime.UtcNow.AddHours(8);` and `ExpiresAt = expiry,` — the cookie uses them.

**2d.** Delete the whole `GenerateJwtToken` method — from `private string GenerateJwtToken(` to its closing `}` just before the comment `// Simple PBKDF2 password helpers…`.

### Step 3 — Remove `Token` from the login response

`SafyaClinic/SafyaClinic.Application/DTOs/Auth/AuthDtos.cs`, in `LoginResponse`, delete:

```csharp
    public string Token { get; init; } = string.Empty;
```

### Step 4 — Remove the `Jwt` settings

**4a.** `SafyaClinic/SafyaClinic.Web/appsettings.json` — delete the whole block:

```json
  "Jwt": {
    "Key": "…",
    "Issuer": "SafyaClinic",
    "Audience": "SafyaClinic"
  },
```

The `Logging` block above it ends with `},` and `"AllowedHosts": "*"` follows — the JSON stays valid.

**4b.** `SafyaClinic/docs/demo-appsettings.template.json` — delete its `Jwt` block the same way (the one with `SafyaClinicDemo`).

**4c.** `SafyaClinic/docs/MonsterASP-Demo-First-Publish.md` — in the settings steps, replace step **4** (the "Replace Jwt:Key…" text, the PowerShell key generator block and the "Copy the generated value…" sentence) and step **5** with:

```markdown
4. No JWT settings are needed: the application no longer issues JWTs (slice S2). If an older server appsettings.json still has a Jwt section, it is ignored and can be removed.
5. Keep AllowedHosts as safyaclinic-demo.runasp.net.
```

### Step 5 — Login tests without any JWT configuration

Create `SafyaClinic/tests/SafyaClinic.Tests/Application/AuthServiceLoginTests.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using SafyaClinic.Application.DTOs.Auth;
using SafyaClinic.Application.Services;
using SafyaClinic.Infrastructure.Data;
using SafyaClinic.Infrastructure.Repositories;
using Xunit;

namespace SafyaClinic.Tests.Application;

/// <summary>
/// Sign-in must work with no Jwt settings at all (slice S2).
/// Uses the seeded administrator on an in-memory database.
/// </summary>
public class AuthServiceLoginTests
{
    private const string AdminPhone = "0100";
    private const string AdminPassword = "Login-Test-Password-1";

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

    [Fact]
    public async Task Admin_can_sign_in_without_jwt_configuration()
    {
        await using var context = await SeededContextAsync();
        var service = new AuthService(new UnitOfWork(context));

        var result = await service.LoginAsync(new LoginRequest { PhoneNumber = AdminPhone, Password = AdminPassword });

        Assert.True(result.IsSuccess);
        Assert.Contains("Admin", result.Data!.Roles);
        Assert.True(result.Data.ExpiresAt > DateTime.UtcNow);
    }

    [Fact]
    public async Task Wrong_password_is_rejected()
    {
        await using var context = await SeededContextAsync();
        var service = new AuthService(new UnitOfWork(context));

        var result = await service.LoginAsync(new LoginRequest { PhoneNumber = AdminPhone, Password = "wrong-password" });

        Assert.False(result.IsSuccess);
    }
}
```

### Step 6 — Make the vulnerability check blocking

In `.github/workflows/ci.yml`, replace:

```yaml
      - name: Vulnerable packages (report only until slice S2)
        continue-on-error: true
        run: |
```

with:

```yaml
      - name: Vulnerable packages
        run: |
```

(Keep the two `run` lines below it unchanged.)

### Step 7 — Verify

```powershell
dotnet build SafyaClinic\SafyaClinic.sln
dotnet test SafyaClinic\SafyaClinic.sln
dotnet list SafyaClinic\SafyaClinic.sln package --vulnerable --include-transitive
git grep -n "Jwt" -- SafyaClinic/SafyaClinic.Web SafyaClinic/SafyaClinic.Application SafyaClinic/docs/demo-appsettings.template.json
```

**Expected:**
- Build: 0 errors.
- Tests: **Passed: 27** (25 + 2), Failed: 0.
- Vulnerability list: every project reports *no vulnerable packages*.
- `git grep`: no output.

Smoke test: `dotnet run --project SafyaClinic\SafyaClinic.Web --launch-profile http`, open `http://localhost:5070`, **sign out and sign in again**, open a page that needs a role (e.g. Payments), sign out.

### Step 8 — Commit, push, PR

```powershell
git add SafyaClinic/SafyaClinic.Web/SafyaClinic.Web.csproj SafyaClinic/SafyaClinic.Application/SafyaClinic.Application.csproj
git add SafyaClinic/SafyaClinic.Application/Services/AuthService.cs SafyaClinic/SafyaClinic.Application/DTOs/Auth/AuthDtos.cs
git add SafyaClinic/SafyaClinic.Web/appsettings.json SafyaClinic/docs SafyaClinic/tests/SafyaClinic.Tests .github/workflows/ci.yml Docs/implementation
git status
git commit -m "S2: remove legacy cookie package, unused JWT and FluentValidation; block vulnerable packages in CI"
git push -u origin chore/s2-packages
```

`git status`: only the files listed in *Affected files*; `SafyaClinic.Web.csproj.user` stays unstaged.

Open a PR into `main`, let CI run, send me the PR number and CI result. Do not merge before review.

## Acceptance criteria

- [ ] `dotnet list … --vulnerable --include-transitive` reports no vulnerable packages, locally and in CI; the CI step is now blocking.
- [ ] No `Jwt` settings or JWT code remain in the Web/Application projects or the demo template.
- [ ] 27 tests pass, including sign-in without any `Jwt` configuration.
- [ ] Manual sign-in/sign-out and a role-protected page work as before.
