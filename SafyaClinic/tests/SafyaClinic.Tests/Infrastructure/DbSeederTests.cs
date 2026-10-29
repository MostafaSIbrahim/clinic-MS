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