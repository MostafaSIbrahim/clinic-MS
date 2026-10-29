# Slice S1 — Remove and rotate committed secrets

Roadmap item **S1** (Group 1 — immediate safety). Fixes F02 (part) and F31 (part).

| | |
|---|---|
| Branch | `chore/s1-secrets` (from an up-to-date `main`, after the Word-documents PR is merged) |
| Behaviour change | **Fresh databases only:** the initial administrator's password now comes from configuration instead of source code. Existing databases (local, hosted test, demo) already have users, so nothing changes for them |
| Size | XS–S |

## Scope

In scope:
1. **Rotate** the Web Deploy password of the hosted test site (`safyaclinic.runasp.net`, site83728) — owner action in the hosting panel.
2. Stop tracking the Web Deploy settings file and the committed publish output folder `SafyaClinicPublish/`, and ignore both.
3. `DbSeeder` reads the bootstrap admin password from configuration (`Seed:AdminPassword`) and fails with a clear message if a fresh database has no users and no password is configured.
4. Tests for the seeder (EF Core in-memory provider).
5. Update the demo deployment template and guide.

Out of scope (later slices): the `Jwt:*` settings and JWT generator — removed together in **S2**, because login currently throws if `Jwt:Key` is missing; the committed upload image — **S3**; rewriting git history.

**Note on history.** Untracking files removes them from `main` from now on; earlier commits still contain them. That is why the password must be **rotated**, not just removed. If the repository is or was public, treat the old Web Deploy password as known to others.

## Affected files

| Action | Path |
|---|---|
| Untrack + delete locally | `safyaclinic.runasp.net-WebDeploy.publishSettings` |
| Untrack (keep local copy) | `SafyaClinicPublish/` (137 files) |
| Edit | `.gitignore` (root) |
| Edit | `SafyaClinic/SafyaClinic.Infrastructure/Data/DbSeeder.cs` |
| Edit | `SafyaClinic/SafyaClinic.Web/Program.cs` (one line) |
| Edit | `SafyaClinic/tests/SafyaClinic.Tests/SafyaClinic.Tests.csproj` (add in-memory provider) |
| Add | `SafyaClinic/tests/SafyaClinic.Tests/Infrastructure/DbSeederTests.cs` |
| Edit | `SafyaClinic/docs/demo-appsettings.template.json` |
| Edit | `SafyaClinic/docs/MonsterASP-Demo-First-Publish.md` (bootstrap account section) |
| Add | `Docs/implementation/F1-test-project-and-ci.md`, `Docs/implementation/S1-remove-and-rotate-secrets.md` (the guides) |
| Optional | `SafyaClinic/SafyaClinic.Web/SafyaClinic.Web.csproj` (`UserSecretsId`, step 6) |

## Risks

| Risk | Mitigation |
|---|---|
| Publishing from Visual Studio fails after rotation | Expected: re-enter the new password in the publish profile once (step 1) |
| A brand-new environment starts without `Seed:AdminPassword` | Startup stops with a message naming the setting — intentional, so no environment gets a known default password by accident |
| Someone relied on `SafyaClinicPublish/` in git as a deployment source | Web Deploy publishes from the project, not from that folder; your local copy stays on disk |
| In-memory provider behaves differently from SQL Server | Used only for seeder logic (insert/exists checks), not for SQL-specific behaviour |

## Rollback

Revert the PR's merge commit. Rotation cannot (and should not) be undone — keep the new password.

---

## Steps

PowerShell, from `D:\MyDevWork\clinic-MS`.

### Step 0 — Branch

```powershell
git switch main
git pull
git switch -c chore/s1-secrets
```

### Step 1 — Rotate the Web Deploy password (owner action)

1. Sign in to the MonsterASP control panel → site **site83728** (`safyaclinic.runasp.net`) → Web Deploy / FTP account → **change password**. Use a new, random password stored only in your password manager.
2. If the repository is or ever was public, do the same for the demo site **site89078** as a precaution.
3. In Visual Studio: *Publish* → profile `site83728-WebDeploy` → *Edit* → enter the new password → *Save*. (It is stored encrypted in the ignored `.pubxml.user` file.)

**Check:** a publish to the test site succeeds with the new password. (You can postpone the actual publish until this PR is merged, but confirm the old password no longer works in the panel.)

### Step 2 — Stop tracking the deploy settings and the publish output

```powershell
git rm --cached "safyaclinic.runasp.net-WebDeploy.publishSettings"
Remove-Item "safyaclinic.runasp.net-WebDeploy.publishSettings"
git rm -r --cached SafyaClinicPublish
```

The `.publishSettings` file holds the **old** password and is no longer needed (Visual Studio uses the profile from step 1), so it is deleted locally too. `SafyaClinicPublish` stays on your disk.

Add these lines to the root `.gitignore`:

```
# Deployment credentials and publish output (S1)
*.publishSettings
SafyaClinicPublish/
```

**Check:** `git status` shows 1 + 137 deleted files and a modified `.gitignore`; the `SafyaClinicPublish` folder still exists in File Explorer.

### Step 3 — Seeder reads the admin password from configuration

In `SafyaClinic/SafyaClinic.Infrastructure/Data/DbSeeder.cs`:

**3a.** Add this `using` at the top with the others:

```csharp
using Microsoft.Extensions.Configuration;
```

**3b.** Replace the `SeedAsync` signature and its `SeedAdminAsync` call:

```csharp
    public static async Task SeedAsync(SafyaDbContext context, IConfiguration configuration)
    {
        await SeedRolesAsync(context);
        await SeedAdminAsync(context, configuration);
        await SeedAnalysisTypesAsync(context);
```

(Leave the remaining lines of `SeedAsync` unchanged.)

**3c.** Replace the beginning of `SeedAdminAsync`, from its signature down to and including the `PasswordHash = …` line, with:

```csharp
    public const string AdminPasswordSettingKey = "Seed:AdminPassword";

    private static async Task SeedAdminAsync(SafyaDbContext context, IConfiguration configuration)
    {
        if (await context.Users.AnyAsync()) return;

        var initialPassword = configuration[AdminPasswordSettingKey];
        if (string.IsNullOrWhiteSpace(initialPassword))
        {
            throw new InvalidOperationException(
                $"The database has no users and '{AdminPasswordSettingKey}' is not configured. " +
                "Set it (user-secrets locally, application settings on the server) to create " +
                "the initial administrator, then remove it after the first sign-in.");
        }

        var admin = new User
        {
            FullName = "System Administrator",
            Email = "admin@safya.com",
            PhoneNumber = "0100",
            PasswordHash = AuthService.HashPassword(initialPassword),
```

Everything after `PasswordHash` (`IsActive`, `CreatedAt`, saving, role assignment) stays exactly as it is.

**Check:** searching `DbSeeder.cs` for the old literal password finds nothing.

### Step 4 — Pass configuration from `Program.cs`

In `SafyaClinic/SafyaClinic.Web/Program.cs`, change:

```csharp
   await DbSeeder.SeedAsync(db);
```

to:

```csharp
    await DbSeeder.SeedAsync(db, app.Configuration);
```

### Step 5 — Seeder tests

**5a.** In `SafyaClinic/tests/SafyaClinic.Tests/SafyaClinic.Tests.csproj`, add to the `PackageReference` item group:

```xml
    <PackageReference Include="Microsoft.EntityFrameworkCore.InMemory" Version="8.0.28" />
```

**5b.** Create `SafyaClinic/tests/SafyaClinic.Tests/Infrastructure/DbSeederTests.cs` (capital **I** in the folder name):

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using SafyaClinic.Application.Services;
using SafyaClinic.Domain.Identity;
using SafyaClinic.Infrastructure.Data;
using Xunit;

namespace SafyaClinic.Tests.Infrastructure;

public class DbSeederTests
{
    private const string TestPassword = "Seed-Test-Password-1";

    private static SafyaDbContext NewContext() =>
        new(new DbContextOptionsBuilder<SafyaDbContext>()
            .UseInMemoryDatabase($"seeder-{Guid.NewGuid()}")
            .Options);

    private static IConfiguration Config(string? adminPassword) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [DbSeeder.AdminPasswordSettingKey] = adminPassword
            })
            .Build();

    [Fact]
    public async Task Empty_database_without_configured_password_fails_clearly()
    {
        await using var context = NewContext();

        var error = await Assert.ThrowsAsync<InvalidOperationException>(
            () => DbSeeder.SeedAsync(context, Config(null)));

        Assert.Contains(DbSeeder.AdminPasswordSettingKey, error.Message);
        Assert.False(await context.Users.AnyAsync());
    }

    [Fact]
    public async Task Empty_database_creates_admin_with_configured_password()
    {
        await using var context = NewContext();

        await DbSeeder.SeedAsync(context, Config(TestPassword));

        var admin = await context.Users.SingleAsync();
        Assert.True(AuthService.VerifyPassword(TestPassword, admin.PasswordHash));
        var adminRoleId = await context.Roles.Where(r => r.RoleName == "Admin").Select(r => r.Id).SingleAsync();
        Assert.True(await context.UserRoles.AnyAsync(ur => ur.UserId == admin.Id && ur.RoleId == adminRoleId));
    }

    [Fact]
    public async Task Existing_users_are_left_unchanged_and_no_password_is_needed()
    {
        await using var context = NewContext();
        context.Users.Add(new User { FullName = "Existing", PhoneNumber = "0111", PasswordHash = "x" });
        await context.SaveChangesAsync();

        await DbSeeder.SeedAsync(context, Config(null));

        var user = await context.Users.SingleAsync();
        Assert.Equal("Existing", user.FullName);
    }
}
```

**Check:**

```powershell
dotnet test SafyaClinic\SafyaClinic.sln
```

Expected **Passed: 25** (22 + 3), Failed: 0. If the package version `8.0.28` cannot be restored, tell me the error — do not change the EF Core version of the application projects.

### Step 6 (optional) — Local setting for fresh databases

Only needed if you create a new empty local database. Your current `SafyaClinicDb` already has users.

```powershell
dotnet user-secrets init --project SafyaClinic\SafyaClinic.Web
dotnet user-secrets set "Seed:AdminPassword" "<choose-a-password>" --project SafyaClinic\SafyaClinic.Web
```

`init` adds a `UserSecretsId` line to `SafyaClinic.Web.csproj` (commit that line; the secret itself is stored outside the repository in your Windows profile).

### Step 7 — Demo deployment files

**7a.** In `SafyaClinic/docs/demo-appsettings.template.json`, add a top-level section after `"ConnectionStrings"` (mind the commas):

```json
  "Seed": {
    "AdminPassword": "REPLACE_WITH_THE_DEMO_ADMIN_PASSWORD"
  },
```

**7b.** In `SafyaClinic/docs/MonsterASP-Demo-First-Publish.md`, replace the bootstrap account lines (around lines 118–121) with:

```markdown
On a fresh empty database, DbSeeder creates the initial administrator from the server setting `Seed:AdminPassword` (see demo-appsettings.template.json). Startup stops with a clear message if the database is empty and the setting is missing.

- Sign-in phone: 0100
- Initial password: Admin@123 (set as Seed:AdminPassword on the demo server; published intentionally for the public demo)
```

(The previous text said *email*; sign-in uses the **phone number**. Keeping the published demo password follows the decision of 2026-10-26.)

### Step 8 — Verify the whole slice

```powershell
dotnet build SafyaClinic\SafyaClinic.sln
dotnet test SafyaClinic\SafyaClinic.sln
git grep -n "Admin@123" -- SafyaClinic/SafyaClinic.Infrastructure SafyaClinic/SafyaClinic.Web
git grep -n "userPWD"
```

**Expected:** build OK; 25 tests pass; both `git grep` commands print **nothing** (the demo guide in `SafyaClinic/docs` intentionally still mentions the demo password).

Smoke test: run the app (`dotnet run --project SafyaClinic\SafyaClinic.Web --launch-profile http`), sign in as before, open the dashboard. Nothing should look different.

### Step 9 — Commit, push, PR

```powershell
git add .gitignore SafyaClinic/SafyaClinic.Infrastructure/Data/DbSeeder.cs SafyaClinic/SafyaClinic.Web/Program.cs
git add SafyaClinic/tests/SafyaClinic.Tests SafyaClinic/docs/demo-appsettings.template.json SafyaClinic/docs/MonsterASP-Demo-First-Publish.md
git add Docs/implementation
git add SafyaClinic/SafyaClinic.Web/SafyaClinic.Web.csproj   # only if you did step 6
git status
git commit -m "S1: stop tracking deploy credentials and publish output; seed admin password from configuration"
git push -u origin chore/s1-secrets
```

`git status` before committing: deletions of the `.publishSettings` file and 137 `SafyaClinicPublish` files are staged (from step 2); not staged: `SafyaClinic.Web.csproj.user`.

Open a PR into `main`; let CI run; **do not merge** until reviewed. Send me the PR number and the CI result.

## Acceptance criteria

- [ ] Old Web Deploy password no longer works (owner confirmation); Visual Studio publish profile uses the new one.
- [ ] `safyaclinic.runasp.net-WebDeploy.publishSettings` and `SafyaClinicPublish/` are not tracked on `main` and are ignored.
- [ ] No hard-coded admin password in application source (`git grep` checks in step 8 return nothing).
- [ ] Fresh empty database: admin is created from `Seed:AdminPassword`; without it, startup fails with a message naming the setting (covered by tests).
- [ ] Existing databases start exactly as before (local smoke test).
- [ ] 25 tests pass locally and in CI.
- [ ] Demo template and guide document the new setting and the correct sign-in phone.
