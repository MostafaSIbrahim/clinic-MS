# Slice F4b-1 — Permission checks: the machinery and the access baseline

Roadmap item **F4**, slice b, part 1 (Group 2). Builds on F4a (catalogue, tables, system groups).

| | |
|---|---|
| Branch | `chore/f4b1-permission-checks` (from an up-to-date `main`, after PR #19 is merged) |
| Behaviour change | **None.** Every controller still uses its role-based policy. This slice adds: 1. `[RequirePermission(…)]`, the attribute F4b-2 will put on controllers and actions; 2. the code that decides whether the signed-in user has a permission, from their roles' system groups and (from F4d) direct group membership; 3. an **access-matrix baseline**, a committed file that lists who can open every controller action today. A test fails if that list ever changes, so F4b-2 can swap every check and prove nobody's access moved |
| Database | None |
| Size | M |

## Design

| Decision | Choice |
|---|---|
| Attribute | `[RequirePermission(Permissions.Records.Lock)]` is an `[Authorize]` with policy `perm:Records.Lock`. Several attributes on a controller and an action must **all** be met, the same as today's attributes |
| Policies | A policy provider builds `perm:` policies on demand. Every other policy name (`AdminOnly`, `ClinicalStaff`, …) is looked up as before, so old and new attributes work side by side during F4b-2 |
| Effective permissions | The grants of every system group whose role the user has, **plus** groups the user is a direct member of (`UserGroups`, empty until F4d). Inactive users have none |
| Cost per request | Loaded at most once per request, and cached for **1 minute** per user and security stamp. A role change renews the stamp, so it applies at once (as today); a group change in F4d applies within a minute, the same interval the session check already uses |
| Role policies | Moved from `Program.cs` into `LegacyRolePolicies`, with the same names and roles. The access-matrix test reads them, and F4b-2 deletes them |
| Access baseline | `access-matrix.txt` lists one line per action, for example `Patients.Timeline GET: Admin Doctor`. The test writes it the first time, you commit it, and I review it against the F4a appendix. After that, any difference fails the test and is written to `access-matrix.actual.txt` for comparison |

## Affected files

| Action | Path |
|---|---|
| Add | `SafyaClinic/SafyaClinic.Application/Security/IPermissionStore.cs` |
| Add | `SafyaClinic/SafyaClinic.Infrastructure/Security/PermissionStore.cs` |
| Edit | `SafyaClinic/SafyaClinic.Infrastructure/DependencyInjection/InfrastructureServiceExtensions.cs` |
| Add | `SafyaClinic/SafyaClinic.Web/Security/PermissionAuthorization.cs` |
| Add | `SafyaClinic/SafyaClinic.Web/Security/CurrentPermissions.cs` |
| Add | `SafyaClinic/SafyaClinic.Web/Security/LegacyRolePolicies.cs` |
| Edit | `SafyaClinic/SafyaClinic.Web/Program.cs` |
| Add | `SafyaClinic/tests/SafyaClinic.Tests/Infrastructure/PermissionStoreTests.cs` |
| Add | `SafyaClinic/tests/SafyaClinic.Tests/Web/Security/PermissionAuthorizationTests.cs` |
| Add | `SafyaClinic/tests/SafyaClinic.Tests/Web/Security/AccessMatrixTests.cs` |
| Add | `SafyaClinic/tests/SafyaClinic.Tests/Web/Security/access-matrix.txt` (written by the test) |
| Edit | `.gitignore` |
| Add | `Docs/implementation/F4b1-permission-checks-infrastructure.md` |

## Risks

| Risk | Mitigation |
|---|---|
| Moving the policies out of `Program.cs` changes one | Same names and role lists, copied exactly; the baseline (reviewed against the F4a appendix) would show any difference |
| Adding the policy provider breaks existing policies | It delegates every non-`perm:` name to the default provider; a test covers it, and the manual check signs in with each role |
| The baseline is wrong and gets frozen | I review the file in the PR before it is merged |

## Rollback

Revert the PR's merge commit. No data changes.

---

## Steps

Run these in PowerShell from `D:\MyDevWork\clinic-MS`.

### Step 0 — Two review fixes on PR #19 (F4a), then merge it

**0a.** Switch back to the F4a branch:

```powershell
git switch chore/f4a-permission-groups
```

**0b.** In `SafyaClinic/SafyaClinic.Infrastructure/Migrations/20261102225429_PermissionGroups.cs`, find this line:

```csharp
                defaultValue: "none");
```

and change it to:

```csharp
                defaultValue: "None");
```

The enum value is `None`. The update right after the column sets every existing row correctly, and the app always writes the value itself. So this only matters for rows inserted outside the app, but the script should be right for the hosted databases.

**0c.** In `SafyaClinic/SafyaClinic.Application/Services/UserService.cs`, inside `GetDoctorsAsync`, change `/// F4a: clinicians are marked …` to `// F4a: clinicians are marked …`. A `///` comment inside a method isn't valid documentation.

**0d.** Build, test, commit and push:

```powershell
dotnet test SafyaClinic\SafyaClinic.sln
git add SafyaClinic
git commit -m "F4a review: ClinicianType column default 'None'; comment style"
git push
```

Expected: 166 passed. When CI is green, **merge PR #19**.

Your dev database keeps the old `'none'` default on that column. Nothing reads it, so leave it.

### Step 1 — Branch

```powershell
git switch main
git pull
git switch -c chore/f4b1-permission-checks
```

### Step 2 — Reading a user's permissions

**2a.** Create `SafyaClinic/SafyaClinic.Application/Security/IPermissionStore.cs`:

```csharp
namespace SafyaClinic.Application.Security;

/// <summary>F4b: reads a user's effective permissions from the database.</summary>
public interface IPermissionStore
{
    /// <summary>
    /// The grants of every system group whose role the user has, plus groups the user is a
    /// direct member of. Empty for unknown or inactive users.
    /// </summary>
    Task<IReadOnlySet<string>> GetPermissionsAsync(int userId, CancellationToken cancellationToken = default);
}
```

**2b.** Create `SafyaClinic/SafyaClinic.Infrastructure/Security/PermissionStore.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using SafyaClinic.Application.Security;
using SafyaClinic.Infrastructure.Data;

namespace SafyaClinic.Infrastructure.Security;

/// <summary>F4b: effective permissions from the groups of the user's roles and direct memberships (F4d).</summary>
public sealed class PermissionStore : IPermissionStore
{
    private static readonly IReadOnlySet<string> None = new HashSet<string>();
    private readonly SafyaDbContext _db;

    public PermissionStore(SafyaDbContext db) => _db = db;

    public async Task<IReadOnlySet<string>> GetPermissionsAsync(int userId, CancellationToken cancellationToken = default)
    {
        if (!await _db.Users.AnyAsync(u => u.Id == userId && u.IsActive, cancellationToken))
            return None;

        var roleNames = await _db.UserRoles
            .Where(ur => ur.UserId == userId)
            .Select(ur => ur.Role.RoleName)
            .ToListAsync(cancellationToken);

        var codes = await _db.GroupPermissions
            .Where(gp => (gp.Group.SystemKey != null && roleNames.Contains(gp.Group.SystemKey!))
                         || gp.Group.Members.Any(m => m.UserId == userId))
            .Select(gp => gp.PermissionCode)
            .Distinct()
            .ToListAsync(cancellationToken);

        return codes.ToHashSet(StringComparer.Ordinal);
    }
}
```

**2c.** `SafyaClinic/SafyaClinic.Infrastructure/DependencyInjection/InfrastructureServiceExtensions.cs`:

- Add with the other `using` lines:

  ```csharp
  using SafyaClinic.Application.Security;
  using SafyaClinic.Infrastructure.Security;
  ```

- After `services.AddScoped<IUnitOfWork, UnitOfWork>();`, add:

  ```csharp
          services.AddScoped<IPermissionStore, PermissionStore>();   // F4b
  ```

### Step 3 — The attribute, policies and handler (Web)

**3a.** Create `SafyaClinic/SafyaClinic.Web/Security/PermissionAuthorization.cs`:

```csharp
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;

namespace SafyaClinic.Web.Security;

/// <summary>F4b: a permission's policy name is "perm:" followed by the permission code.</summary>
public static class PermissionPolicy
{
    public const string Prefix = "perm:";

    public static string For(string code) => Prefix + code;
}

public sealed class PermissionRequirement : IAuthorizationRequirement
{
    public PermissionRequirement(string code) => Code = code;

    public string Code { get; }
}

/// <summary>
/// F4b: <c>[RequirePermission(Permissions.Records.Lock)]</c> on a controller or action.
/// Like [Authorize], every attribute on the controller and the action must be met.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true, Inherited = true)]
public sealed class RequirePermissionAttribute : AuthorizeAttribute
{
    public RequirePermissionAttribute(string code) : base(PermissionPolicy.For(code)) => Code = code;

    public string Code { get; }
}

/// <summary>F4b: builds "perm:" policies on demand; every other policy name is looked up as before.</summary>
public sealed class PermissionPolicyProvider : DefaultAuthorizationPolicyProvider
{
    public PermissionPolicyProvider(IOptions<AuthorizationOptions> options) : base(options) { }

    public override async Task<AuthorizationPolicy?> GetPolicyAsync(string policyName)
    {
        if (!policyName.StartsWith(PermissionPolicy.Prefix, StringComparison.Ordinal))
            return await base.GetPolicyAsync(policyName);

        return new AuthorizationPolicyBuilder()
            .RequireAuthenticatedUser()
            .AddRequirements(new PermissionRequirement(policyName[PermissionPolicy.Prefix.Length..]))
            .Build();
    }
}

/// <summary>F4b: met when the signed-in user's groups grant the permission.</summary>
public sealed class PermissionHandler : AuthorizationHandler<PermissionRequirement>
{
    private readonly ICurrentPermissions _permissions;

    public PermissionHandler(ICurrentPermissions permissions) => _permissions = permissions;

    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context, PermissionRequirement requirement)
    {
        if (await _permissions.HasAsync(requirement.Code))
            context.Succeed(requirement);
    }
}
```

**3b.** Create `SafyaClinic/SafyaClinic.Web/Security/CurrentPermissions.cs`:

```csharp
using System.Security.Claims;
using Microsoft.Extensions.Caching.Memory;
using SafyaClinic.Application.Security;

namespace SafyaClinic.Web.Security;

/// <summary>F4b: the signed-in user's permissions for the current request.</summary>
public interface ICurrentPermissions
{
    Task<bool> HasAsync(string code);
}

/// <summary>
/// Read at most once per request and cached for <see cref="CacheDuration"/> per user and
/// security stamp: a role change (which renews the stamp) applies at once, a group change
/// (F4d) within a minute.
/// </summary>
public sealed class CurrentPermissions : ICurrentPermissions
{
    public static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(1);
    private static readonly IReadOnlySet<string> None = new HashSet<string>();

    private readonly IHttpContextAccessor _http;
    private readonly IPermissionStore _store;
    private readonly IMemoryCache _cache;
    private IReadOnlySet<string>? _loaded;

    public CurrentPermissions(IHttpContextAccessor http, IPermissionStore store, IMemoryCache cache)
    {
        _http = http;
        _store = store;
        _cache = cache;
    }

    public async Task<bool> HasAsync(string code) => (await LoadAsync()).Contains(code);

    private async Task<IReadOnlySet<string>> LoadAsync()
    {
        if (_loaded is not null) return _loaded;

        var user = _http.HttpContext?.User;
        if (user?.Identity?.IsAuthenticated != true
            || !int.TryParse(user.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
        {
            return _loaded = None;
        }

        var key = $"permissions:{userId}:{user.FindFirstValue(AuthClaims.SecurityStamp)}";
        _loaded = await _cache.GetOrCreateAsync(key, entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = CacheDuration;
            return _store.GetPermissionsAsync(userId);
        }) ?? None;
        return _loaded;
    }
}
```

**3c.** Create `SafyaClinic/SafyaClinic.Web/Security/LegacyRolePolicies.cs`:

```csharp
namespace SafyaClinic.Web.Security;

/// <summary>
/// The role-based policies in use before permissions (moved unchanged from Program.cs).
/// F4b-2 replaces every use with [RequirePermission] and deletes this class.
/// </summary>
public static class LegacyRolePolicies
{
    public static readonly IReadOnlyDictionary<string, string[]> All = new Dictionary<string, string[]>
    {
        ["PaymentStaff"] = new[] { "Admin", "Reception", "ReceptionAdmin" },
        ["PaymentReports"] = new[] { "Admin", "Reception", "ReceptionAdmin", "Doctor" },
        ["AdminOnly"] = new[] { "Admin" },
        ["DoctorOrAdmin"] = new[] { "Admin", "Doctor" },
        ["ReceptionOrAdmin"] = new[] { "Admin", "Reception" },
        ["NutritionTeam"] = new[] { "Admin", "Nutritionist" },
        ["ClinicalStaff"] = new[] { "Admin", "Doctor", "Nutritionist", "Reception" },
    };
}
```

**3d.** `SafyaClinic/SafyaClinic.Web/Program.cs`:

- Add with the other `using` lines at the top:

  ```csharp
  using Microsoft.AspNetCore.Authorization;
  ```

- Replace the whole block from `builder.Services.AddAuthorizationBuilder()` down to and including the line ending `.AddPolicy("ClinicalStaff", … "Reception"));` with:

```csharp
// ── Authorization ────────────────────────────────────────────
// Role-based policies until F4b-2 moves every controller to [RequirePermission].
builder.Services.AddAuthorization(options =>
{
    foreach (var (name, roles) in LegacyRolePolicies.All)
        options.AddPolicy(name, policy => policy.RequireRole(roles));
});
// Permissions (F4b): "perm:<code>" policies are built on demand and checked against the user's groups.
builder.Services.AddSingleton<IAuthorizationPolicyProvider, PermissionPolicyProvider>();
builder.Services.AddScoped<IAuthorizationHandler, PermissionHandler>();
builder.Services.AddScoped<ICurrentPermissions, CurrentPermissions>();
```

(The comment line `// Payment access: financial reporting and collection have separate permissions.` above the old block goes too.)

**Check:** `dotnet build SafyaClinic\SafyaClinic.sln` gives 0 errors.

### Step 4 — Tests

**4a.** Create `SafyaClinic/tests/SafyaClinic.Tests/Infrastructure/PermissionStoreTests.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using SafyaClinic.Application.Security;
using SafyaClinic.Domain.Identity;
using SafyaClinic.Infrastructure.Data;
using SafyaClinic.Infrastructure.Security;
using Xunit;

namespace SafyaClinic.Tests.Infrastructure;

/// <summary>F4b: effective permissions come from the groups of the user's roles and direct memberships.</summary>
public class PermissionStoreTests
{
    private static async Task<SafyaDbContext> SeededAsync()
    {
        var context = new SafyaDbContext(new DbContextOptionsBuilder<SafyaDbContext>()
            .UseInMemoryDatabase($"store-{Guid.NewGuid()}")
            .Options);
        await DbSeeder.SeedAsync(context, new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { [DbSeeder.AdminPasswordSettingKey] = "Store-Test-Password-1" })
            .Build());
        return context;
    }

    private static async Task<int> UserAsync(SafyaDbContext context, string phone, bool active, params string[] roles)
    {
        var user = new User { FullName = "User " + phone, PhoneNumber = phone, PasswordHash = "x", SecurityStamp = "s", IsActive = active };
        context.Users.Add(user);
        await context.SaveChangesAsync();
        foreach (var role in roles)
        {
            var roleId = await context.Roles.Where(r => r.RoleName == role).Select(r => r.Id).SingleAsync();
            context.UserRoles.Add(new UserRole { UserId = user.Id, RoleId = roleId });
        }
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();
        return user.Id;
    }

    private static IEnumerable<string> GroupOf(string role) =>
        SystemGroups.All.Single(g => g.RoleName == role).Permissions;

    [Fact]
    public async Task A_role_gives_the_permissions_of_its_group()
    {
        await using var context = await SeededAsync();
        var userId = await UserAsync(context, "0151", true, SystemGroups.ReceptionRole);

        var permissions = await new PermissionStore(context).GetPermissionsAsync(userId);

        Assert.Equal(GroupOf(SystemGroups.ReceptionRole).OrderBy(c => c), permissions.OrderBy(c => c));
    }

    [Fact]
    public async Task Several_roles_give_the_union()
    {
        await using var context = await SeededAsync();
        var userId = await UserAsync(context, "0152", true, SystemGroups.DoctorRole, SystemGroups.NutritionistRole);

        var permissions = await new PermissionStore(context).GetPermissionsAsync(userId);

        var expected = GroupOf(SystemGroups.DoctorRole).Union(GroupOf(SystemGroups.NutritionistRole));
        Assert.Equal(expected.OrderBy(c => c), permissions.OrderBy(c => c));
    }

    [Fact]
    public async Task Direct_group_membership_counts()
    {
        await using var context = await SeededAsync();
        var userId = await UserAsync(context, "0153", true);
        var groupId = await context.PermissionGroups
            .Where(g => g.SystemKey == SystemGroups.ReceptionAdminRole).Select(g => g.Id).SingleAsync();
        context.UserGroups.Add(new UserGroup { UserId = userId, GroupId = groupId });
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        var permissions = await new PermissionStore(context).GetPermissionsAsync(userId);

        Assert.Equal(GroupOf(SystemGroups.ReceptionAdminRole).OrderBy(c => c), permissions.OrderBy(c => c));
    }

    [Fact]
    public async Task Inactive_users_have_no_permissions()
    {
        await using var context = await SeededAsync();
        var userId = await UserAsync(context, "0154", false, SystemGroups.AdminRole);

        Assert.Empty(await new PermissionStore(context).GetPermissionsAsync(userId));
    }
}
```

**4b.** Create `SafyaClinic/tests/SafyaClinic.Tests/Web/Security/PermissionAuthorizationTests.cs`:

```csharp
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Infrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using SafyaClinic.Application.Security;
using SafyaClinic.Web.Security;
using Xunit;

namespace SafyaClinic.Tests.Web.Security;

/// <summary>F4b: permission policies, the handler and the per-request permission cache.</summary>
public class PermissionAuthorizationTests
{
    private sealed class FixedPermissions : ICurrentPermissions
    {
        private readonly HashSet<string> _codes;
        public FixedPermissions(params string[] codes) => _codes = codes.ToHashSet();
        public Task<bool> HasAsync(string code) => Task.FromResult(_codes.Contains(code));
    }

    private sealed class CountingStore : IPermissionStore
    {
        public int Calls { get; private set; }

        public Task<IReadOnlySet<string>> GetPermissionsAsync(int userId, CancellationToken cancellationToken = default)
        {
            Calls++;
            return Task.FromResult<IReadOnlySet<string>>(new HashSet<string> { Permissions.Patients.View });
        }
    }

    private static ClaimsPrincipal SignedIn(string userId, string stamp) =>
        new(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, userId),
            new Claim(AuthClaims.SecurityStamp, stamp)
        }, "test"));

    private static CurrentPermissions Current(ClaimsPrincipal user, IPermissionStore store, IMemoryCache cache) =>
        new(new HttpContextAccessor { HttpContext = new DefaultHttpContext { User = user } }, store, cache);

    [Fact]
    public async Task Permission_policies_are_built_on_demand()
    {
        var provider = new PermissionPolicyProvider(Options.Create(new AuthorizationOptions()));

        var policy = await provider.GetPolicyAsync(PermissionPolicy.For(Permissions.Users.Manage));

        var requirement = Assert.Single(policy!.Requirements.OfType<PermissionRequirement>());
        Assert.Equal(Permissions.Users.Manage, requirement.Code);
        Assert.Contains(policy.Requirements, r => r is DenyAnonymousAuthorizationRequirement);
    }

    [Fact]
    public async Task Named_policies_still_work()
    {
        var options = new AuthorizationOptions();
        options.AddPolicy("AdminOnly", p => p.RequireRole("Admin"));
        var provider = new PermissionPolicyProvider(Options.Create(options));

        var policy = await provider.GetPolicyAsync("AdminOnly");

        Assert.Contains(policy!.Requirements, r => r is RolesAuthorizationRequirement);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Handler_succeeds_only_with_the_permission(bool granted)
    {
        var requirement = new PermissionRequirement(Permissions.Records.Lock);
        var context = new AuthorizationHandlerContext(new[] { requirement }, new ClaimsPrincipal(), null);
        var handler = new PermissionHandler(granted ? new FixedPermissions(Permissions.Records.Lock) : new FixedPermissions());

        await handler.HandleAsync(context);

        Assert.Equal(granted, context.HasSucceeded);
    }

    [Fact]
    public async Task Anonymous_users_have_no_permissions()
    {
        var store = new CountingStore();

        var has = await Current(new ClaimsPrincipal(new ClaimsIdentity()), store, new MemoryCache(new MemoryCacheOptions()))
            .HasAsync(Permissions.Patients.View);

        Assert.False(has);
        Assert.Equal(0, store.Calls);
    }

    [Fact]
    public async Task Permissions_are_cached_per_user_and_stamp()
    {
        var store = new CountingStore();
        var cache = new MemoryCache(new MemoryCacheOptions());

        var request = Current(SignedIn("7", "stamp-1"), store, cache);
        Assert.True(await request.HasAsync(Permissions.Patients.View));
        Assert.False(await request.HasAsync(Permissions.Users.Manage));
        Assert.True(await Current(SignedIn("7", "stamp-1"), store, cache).HasAsync(Permissions.Patients.View));
        Assert.Equal(1, store.Calls);

        await Current(SignedIn("7", "stamp-2"), store, cache).HasAsync(Permissions.Patients.View);   // stamp renewed
        Assert.Equal(2, store.Calls);
    }
}
```

**4c.** Create `SafyaClinic/tests/SafyaClinic.Tests/Web/Security/AccessMatrixTests.cs`:

```csharp
using System.Reflection;
using System.Runtime.CompilerServices;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using SafyaClinic.Application.Security;
using SafyaClinic.Web.Controllers;
using SafyaClinic.Web.Security;
using Xunit;

namespace SafyaClinic.Tests.Web.Security;

/// <summary>
/// F4b: who can open each controller action, worked out from its [Authorize] and
/// [RequirePermission] attributes. The approved list is access-matrix.txt; moving to
/// permissions (F4b-2) must leave it unchanged. Checks inside actions are not covered here.
/// </summary>
public class AccessMatrixTests
{
    private static readonly string[] Roles =
    {
        SystemGroups.AdminRole, SystemGroups.DoctorRole, SystemGroups.NutritionistRole,
        SystemGroups.ReceptionRole, SystemGroups.ReceptionAdminRole
    };

    private static IEnumerable<Type> Controllers =>
        typeof(BaseController).Assembly.GetTypes()
            .Where(t => typeof(ControllerBase).IsAssignableFrom(t) && !t.IsAbstract);

    public static IReadOnlyList<string> Compute()
    {
        var lines = new List<string>();
        foreach (var controller in Controllers)
        {
            var name = controller.Name.EndsWith("Controller") ? controller.Name[..^"Controller".Length] : controller.Name;
            var actions = controller.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .Where(m => !m.IsSpecialName && m.GetCustomAttribute<NonActionAttribute>() is null);
            foreach (var action in actions)
            {
                var verbs = action.GetCustomAttributes<HttpMethodAttribute>()
                    .SelectMany(a => a.HttpMethods).Distinct().OrderBy(v => v).ToList();
                var verb = verbs.Count == 0 ? "ANY" : string.Join("/", verbs);
                lines.Add($"{name}.{action.Name} {verb}: {Access(controller, action)}");
            }
        }
        return lines.OrderBy(l => l, StringComparer.Ordinal).ToList();
    }

    private static string Access(Type controller, MethodInfo action)
    {
        if (controller.GetCustomAttributes<AllowAnonymousAttribute>(true).Any()
            || action.GetCustomAttributes<AllowAnonymousAttribute>(true).Any())
            return "anyone";

        var rules = controller.GetCustomAttributes(true).OfType<IAuthorizeData>()
            .Concat(action.GetCustomAttributes(true).OfType<IAuthorizeData>())
            .ToList();
        if (rules.Count == 0) return "anyone";
        if (rules.All(r => string.IsNullOrEmpty(r.Policy) && string.IsNullOrEmpty(r.Roles))) return "signed-in";

        IEnumerable<string> allowed = Roles;
        foreach (var rule in rules)
        {
            if (!string.IsNullOrEmpty(rule.Policy))
                allowed = allowed.Intersect(RolesFor(rule.Policy));
            if (!string.IsNullOrEmpty(rule.Roles))
                allowed = allowed.Intersect(rule.Roles.Split(',').Select(r => r.Trim()));
        }

        var list = allowed.ToList();
        return list.Count == 0 ? "nobody" : string.Join(" ", list);
    }

    private static IEnumerable<string> RolesFor(string policy)
    {
        if (policy.StartsWith(PermissionPolicy.Prefix, StringComparison.Ordinal))
        {
            var code = policy[PermissionPolicy.Prefix.Length..];
            return SystemGroups.All.Where(g => g.Permissions.Contains(code)).Select(g => g.RoleName);
        }

        Assert.True(LegacyRolePolicies.All.ContainsKey(policy), $"Unknown policy '{policy}'.");
        return LegacyRolePolicies.All[policy];
    }

    private static string BaselinePath([CallerFilePath] string source = "") =>
        Path.Combine(Path.GetDirectoryName(source)!, "access-matrix.txt");

    [Fact]
    public void Access_matches_the_approved_baseline()
    {
        var path = BaselinePath();
        var actual = string.Join("\n", Compute()) + "\n";

        if (!File.Exists(path))
        {
            File.WriteAllText(path, actual);
            Assert.Fail($"No baseline yet. It has been written to {path}: review it and commit it.");
        }

        var expected = File.ReadAllText(path).Replace("\r\n", "\n");
        if (expected != actual)
        {
            File.WriteAllText(Path.ChangeExtension(path, ".actual.txt"), actual);
            var changed = actual.Split('\n').Except(expected.Split('\n'));
            Assert.Fail("Access changed. New or different lines (full list in access-matrix.actual.txt):\n"
                        + string.Join("\n", changed));
        }
    }

    [Fact]
    public void Every_required_permission_is_in_the_catalogue()
    {
        var known = PermissionCatalogue.All.Select(p => p.Code).ToHashSet();
        var used = Controllers.SelectMany(c => c.GetCustomAttributes<RequirePermissionAttribute>(true)
                .Concat(c.GetMethods().SelectMany(m => m.GetCustomAttributes<RequirePermissionAttribute>(true))))
            .Select(a => a.Code);

        Assert.All(used, code => Assert.Contains(code, known));
    }
}
```

**4d.** Add this line at the end of the root `.gitignore`:

```
# F4b: written by AccessMatrixTests when access differs from the baseline
access-matrix.actual.txt
```

**4e.** Run the tests **twice**:

```powershell
dotnet test SafyaClinic\SafyaClinic.sln
dotnet test SafyaClinic\SafyaClinic.sln
```

- **First run:** 1 failure, `No baseline yet. It has been written to …access-matrix.txt`. This is expected: the test has just written today's access list.
- **Second run:** **Passed: 178** (166 + 12), Failed: 0.

Anything else fails → send me the output.

**4f.** Open `SafyaClinic\tests\SafyaClinic.Tests\Web\Security\access-matrix.txt` and skim it. Spot checks:
- `Auth.Login GET: anyone`
- `Dashboard.Index ANY: signed-in`
- `Patients.Timeline GET: Admin Doctor`
- `Reservations.CheckIn POST: Admin Reception`
- `Nutrition.CreatePackage GET: Admin`
- `Payments.Report GET: Admin Doctor Reception ReceptionAdmin`

I'll review the whole file in the PR.

### Step 5 — Manual check

```powershell
dotnet run --project SafyaClinic\SafyaClinic.Web --launch-profile http
```

Sign in as Admin, then as a Reception user. For each, open the Dashboard, Patients, Reservations and Payments. Everything behaves exactly as before; this slice changes no access.

### Step 6 — Commit, push, PR

```powershell
git add SafyaClinic/SafyaClinic.Application SafyaClinic/SafyaClinic.Infrastructure SafyaClinic/SafyaClinic.Web SafyaClinic/tests .gitignore Docs
git status
git commit -m "F4b-1: RequirePermission attribute, permission policies and handler, cached effective permissions, access-matrix baseline"
git push -u origin chore/f4b1-permission-checks
```

`git status` must show `access-matrix.txt` staged and **no** `access-matrix.actual.txt`. Open the PR and send me the number and the CI result. Don't merge before review.

## Acceptance criteria

- [ ] `[RequirePermission]` exists; `perm:` policies are built on demand; existing named policies still work.
- [ ] A user's permissions = their roles' system groups ∪ direct groups; inactive users have none; loaded once per request and cached for a minute per user and stamp.
- [ ] `access-matrix.txt` is committed and matches today's access (reviewed).
- [ ] No visible change for any user.
- [ ] 178 tests pass locally and in CI.

## Next: F4b-2

F4b-2 replaces every `[Authorize(Policy/Roles)]` with `[RequirePermission]` following the F4a appendix, then the role checks inside controllers (`IsAdmin`, `IsReception`, …) and `ReservationRules` with permission checks. After that it deletes `LegacyRolePolicies`. `access-matrix.txt` must not change.
