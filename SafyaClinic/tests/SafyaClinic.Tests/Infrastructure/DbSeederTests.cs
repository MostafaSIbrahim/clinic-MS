using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using SafyaClinic.Application.Services;
using SafyaClinic.Domain.Identity;
using SafyaClinic.Infrastructure.Data;
using Xunit;
using SafyaClinic.Application.Security;

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
        Assert.Equal(PasswordCheck.Success, PasswordHashing.Verify(TestPassword, admin.PasswordHash));
        var administratorsId = await context.PermissionGroups
            .Where(g => g.SystemKey == SystemGroups.AdminRole).Select(g => g.Id).SingleAsync();
        Assert.True(await context.UserGroups.AnyAsync(m => m.UserId == admin.Id && m.GroupId == administratorsId));
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

    [Fact]
    public async Task A_permission_new_to_the_catalogue_is_granted_once_to_its_built_in_groups()
    {
        await using var context = NewContext();
        await DbSeeder.SeedAsync(context, Config(TestPassword));

        // A database from before P1b: the permission and its grants don't exist yet.
        var doctorsId = await context.PermissionGroups.Where(g => g.SystemKey == SystemGroups.DoctorRole).Select(g => g.Id).SingleAsync();
        context.GroupPermissions.RemoveRange(await context.GroupPermissions.Where(p => p.PermissionCode == Permissions.Schedule.ManageOwn).ToListAsync());
        context.Permissions.Remove(await context.Permissions.SingleAsync(p => p.Code == Permissions.Schedule.ManageOwn));
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        await DbSeeder.SeedAsync(context, Config(null));
        Assert.True(await context.GroupPermissions.AnyAsync(p => p.GroupId == doctorsId && p.PermissionCode == Permissions.Schedule.ManageOwn));

        // An admin removes it later: the next start-up leaves it removed.
        context.GroupPermissions.RemoveRange(await context.GroupPermissions
            .Where(p => p.GroupId == doctorsId && p.PermissionCode == Permissions.Schedule.ManageOwn).ToListAsync());
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        await DbSeeder.SeedAsync(context, Config(null));
        Assert.False(await context.GroupPermissions.AnyAsync(p => p.GroupId == doctorsId && p.PermissionCode == Permissions.Schedule.ManageOwn));
    }

    [Fact]
    public void Roles_tables_are_no_longer_part_of_the_model()
    {
        var model = new SafyaDbContext(new DbContextOptionsBuilder<SafyaDbContext>()
            .UseSqlServer("Server=model-only;Database=ModelOnly;Integrated Security=True").Options).Model;

        var tables = model.GetEntityTypes().Select(e => e.GetTableName()).ToList();

        Assert.DoesNotContain("Roles", tables);
        Assert.DoesNotContain("UserRoles", tables);
        Assert.Contains("UserGroups", tables);
    }

    [Fact]
    public async Task Seeding_twice_creates_one_admin_and_the_built_in_groups_once()
    {
        await using var context = NewContext();

        await DbSeeder.SeedAsync(context, Config(TestPassword));
        await DbSeeder.SeedAsync(context, Config(TestPassword));

        Assert.Equal(1, await context.Users.CountAsync());
        Assert.Equal(SystemGroups.All.Count, await context.PermissionGroups.CountAsync(g => g.SystemKey != null));
    }
}