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