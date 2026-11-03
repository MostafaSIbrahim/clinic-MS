using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using SafyaClinic.Application.Security;
using SafyaClinic.Domain.Identity;
using SafyaClinic.Infrastructure.Data;
using SafyaClinic.Infrastructure.Security;
using Xunit;

namespace SafyaClinic.Tests.Infrastructure;

/// <summary>F4b/F4d: effective permissions are the union of the user's groups.</summary>
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
            await TestData.AddToBuiltInGroupAsync(context, user.Id, role);
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