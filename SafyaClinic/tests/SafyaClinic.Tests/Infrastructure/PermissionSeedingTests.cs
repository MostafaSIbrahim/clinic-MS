using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using SafyaClinic.Application.Security;
using SafyaClinic.Domain.Identity;
using SafyaClinic.Infrastructure.Data;
using Xunit;

namespace SafyaClinic.Tests.Infrastructure;

/// <summary>F4a: the seeder copies the catalogue and creates one group per role, once.</summary>
public class PermissionSeedingTests
{
    private static SafyaDbContext NewContext() =>
        new(new DbContextOptionsBuilder<SafyaDbContext>()
            .UseInMemoryDatabase($"permissions-{Guid.NewGuid()}")
            .Options);

    private static readonly IConfiguration Config = new ConfigurationBuilder()
        .AddInMemoryCollection(new Dictionary<string, string?> { [DbSeeder.AdminPasswordSettingKey] = "Seed-Test-Password-1" })
        .Build();

    private static Task<int> GroupId(SafyaDbContext context, string roleName) =>
        context.PermissionGroups.Where(g => g.SystemKey == roleName).Select(g => g.Id).SingleAsync();

    [Fact]
    public async Task Seeder_copies_the_catalogue_and_creates_the_system_groups()
    {
        await using var context = NewContext();

        await DbSeeder.SeedAsync(context, Config);

        Assert.Equal(PermissionCatalogue.All.Count, await context.Permissions.CountAsync());
        Assert.Equal(SystemGroups.All.Count, await context.PermissionGroups.CountAsync());
        foreach (var definition in SystemGroups.All)
        {
            var group = await context.PermissionGroups
                .Include(g => g.GroupPermissions)
                .SingleAsync(g => g.SystemKey == definition.RoleName);
            Assert.Equal(definition.Name, group.Name);
            Assert.Equal(definition.Permissions.OrderBy(c => c),
                group.GroupPermissions.Select(p => p.PermissionCode).OrderBy(c => c));
        }
    }

    [Fact]
    public async Task Seeding_again_adds_nothing()
    {
        await using var context = NewContext();
        await DbSeeder.SeedAsync(context, Config);
        var permissions = await context.Permissions.CountAsync();
        var groups = await context.PermissionGroups.CountAsync();
        var grants = await context.GroupPermissions.CountAsync();
        context.ChangeTracker.Clear();

        await DbSeeder.SeedAsync(context, Config);

        Assert.Equal(permissions, await context.Permissions.CountAsync());
        Assert.Equal(groups, await context.PermissionGroups.CountAsync());
        Assert.Equal(grants, await context.GroupPermissions.CountAsync());
    }

    [Fact]
    public async Task Removed_grants_stay_removed_except_for_administrators()
    {
        await using var context = NewContext();
        await DbSeeder.SeedAsync(context, Config);
        var doctorsId = await GroupId(context, SystemGroups.DoctorRole);
        var adminsId = await GroupId(context, SystemGroups.AdminRole);
        context.GroupPermissions.RemoveRange(await context.GroupPermissions
            .Where(p => (p.GroupId == doctorsId || p.GroupId == adminsId) && p.PermissionCode == Permissions.Patients.EditMedical)
            .ToListAsync());
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        await DbSeeder.SeedAsync(context, Config);

        Assert.False(await context.GroupPermissions.AnyAsync(p => p.GroupId == doctorsId && p.PermissionCode == Permissions.Patients.EditMedical));
        Assert.True(await context.GroupPermissions.AnyAsync(p => p.GroupId == adminsId && p.PermissionCode == Permissions.Patients.EditMedical));
    }

    [Fact]
    public void Clinician_type_is_stored_as_text()
    {
        var model = new SafyaDbContext(new DbContextOptionsBuilder<SafyaDbContext>()
            .UseSqlServer("Server=model-only;Database=ModelOnly;Integrated Security=True").Options).Model;

        var property = model.FindEntityType(typeof(User))!.FindProperty(nameof(User.ClinicianType))!;

        Assert.Equal(typeof(string), property.GetProviderClrType());
        Assert.Equal(20, property.GetMaxLength());
    }
}