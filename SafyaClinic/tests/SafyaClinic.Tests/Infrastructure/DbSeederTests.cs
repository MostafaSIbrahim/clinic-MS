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
    public async Task Role_memberships_become_group_memberships_once()
    {
        await using var context = NewContext();
        await DbSeeder.SeedAsync(context, Config(TestPassword));

        // A database from before F4d: memberships exist only as roles.
        context.UserGroups.RemoveRange(await context.UserGroups.ToListAsync());
        var doctor = new User { FullName = "Doctor", PhoneNumber = "0177", PasswordHash = "x" };
        context.Users.Add(doctor);
        await context.SaveChangesAsync();
        var adminId = await context.Users.Where(u => u.PhoneNumber == "0100").Select(u => u.Id).SingleAsync();
        var doctorRoleId = await context.Roles.Where(r => r.RoleName == "Doctor").Select(r => r.Id).SingleAsync();
        var adminRoleId = await context.Roles.Where(r => r.RoleName == "Admin").Select(r => r.Id).SingleAsync();
        context.UserRoles.AddRange(
            new UserRole { UserId = doctor.Id, RoleId = doctorRoleId },
            new UserRole { UserId = adminId, RoleId = adminRoleId });
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        await DbSeeder.SeedAsync(context, Config(null));

        var doctorsId = await context.PermissionGroups
            .Where(g => g.SystemKey == SystemGroups.DoctorRole).Select(g => g.Id).SingleAsync();
        Assert.True(await context.UserGroups.AnyAsync(m => m.UserId == doctor.Id && m.GroupId == doctorsId));

        // Changes made later on the groups screen are kept: the move does not run again.
        context.UserGroups.RemoveRange(await context.UserGroups.Where(m => m.UserId == doctor.Id).ToListAsync());
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        await DbSeeder.SeedAsync(context, Config(null));

        Assert.False(await context.UserGroups.AnyAsync(m => m.UserId == doctor.Id));
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
    public async Task Misnamed_reception_admin_role_is_renamed()
    {
        await using var context = NewContext();
        // Roles already exist, so the seeder skips its role list; the admin seed still needs "Admin".
        context.Roles.AddRange(new Role { RoleName = "Admin" }, new Role { RoleName = "Reception Admin" });
        await context.SaveChangesAsync();

        await DbSeeder.SeedAsync(context, Config(TestPassword));

        Assert.False(await context.Roles.AnyAsync(r => r.RoleName == "Reception Admin"));
        Assert.Equal(1, await context.Roles.CountAsync(r => r.RoleName == DbSeeder.ReceptionAdminRoleName));
    }

    [Fact]
    public async Task Reception_admin_role_is_created_when_missing()
    {
        await using var context = NewContext();

        await DbSeeder.SeedAsync(context, Config(TestPassword));

        Assert.True(await context.Roles.AnyAsync(r => r.RoleName == DbSeeder.ReceptionAdminRoleName));
    }

    [Fact]
    public async Task Seeding_twice_keeps_a_single_reception_admin_role()
    {
        await using var context = NewContext();

        await DbSeeder.SeedAsync(context, Config(TestPassword));
        await DbSeeder.SeedAsync(context, Config(TestPassword));

        Assert.Equal(1, await context.Roles.CountAsync(r => r.RoleName == DbSeeder.ReceptionAdminRoleName));
    }
}