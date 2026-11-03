using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using SafyaClinic.Application.Security;
using SafyaClinic.Domain.Identity;
using SafyaClinic.Infrastructure.Data;
using SafyaClinic.Infrastructure.Security;
using Xunit;

namespace SafyaClinic.Tests.Infrastructure;

/// <summary>F4d: admins manage groups; built-in groups keep their rules; access follows at once.</summary>
public class PermissionGroupServiceTests
{
    private sealed record Setup(SafyaDbContext Context, int AdminId, int DoctorId);

    private static async Task<Setup> SeedAsync()
    {
        var context = new SafyaDbContext(new DbContextOptionsBuilder<SafyaDbContext>()
            .UseInMemoryDatabase($"groups-{Guid.NewGuid()}")
            .Options);
        await DbSeeder.SeedAsync(context, new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { [DbSeeder.AdminPasswordSettingKey] = "Groups-Test-Password-1" })
            .Build());
        var adminId = await context.Users.Where(u => u.PhoneNumber == "0100").Select(u => u.Id).SingleAsync();

        var doctor = new User { FullName = "Doctor", PhoneNumber = "0161", PasswordHash = "x", SecurityStamp = "s" };
        context.Users.Add(doctor);
        await context.SaveChangesAsync();
        var doctorRoleId = await context.Roles.Where(r => r.RoleName == SystemGroups.DoctorRole).Select(r => r.Id).SingleAsync();
        context.UserRoles.Add(new UserRole { UserId = doctor.Id, RoleId = doctorRoleId });
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();
        return new Setup(context, adminId, doctor.Id);
    }

    private static PermissionGroupService Service(SafyaDbContext context)
    {
        context.ChangeTracker.Clear();   // each call is a new request in the app
        return new PermissionGroupService(context);
    }

    private static Task<IReadOnlySet<string>> PermissionsOf(SafyaDbContext context, int userId)
    {
        context.ChangeTracker.Clear();
        return new PermissionStore(context).GetPermissionsAsync(userId);
    }

    private static Task<int> BuiltIn(SafyaDbContext context, string role) =>
        context.PermissionGroups.Where(g => g.SystemKey == role).Select(g => g.Id).SingleAsync();

    [Fact]
    public async Task Admin_creates_a_group_grants_a_permission_and_adds_a_user_who_gains_it()
    {
        var s = await SeedAsync();
        Assert.DoesNotContain(Permissions.Payments.Collect, await PermissionsOf(s.Context, s.DoctorId));

        var created = await Service(s.Context).CreateGroupAsync("Billing helpers", "Doctors who also collect", s.AdminId);
        Assert.True(created.IsSuccess);
        Assert.True((await Service(s.Context).UpdateGroupAsync(
            created.Data, "Billing helpers", null, new[] { Permissions.Payments.Collect }, s.AdminId)).IsSuccess);
        Assert.True((await Service(s.Context).AddMemberAsync(created.Data, s.DoctorId, s.AdminId)).IsSuccess);

        Assert.Contains(Permissions.Payments.Collect, await PermissionsOf(s.Context, s.DoctorId));
    }

    [Fact]
    public async Task Removing_a_member_takes_the_access_away()
    {
        var s = await SeedAsync();
        var groupId = (await Service(s.Context).CreateGroupAsync("Billing", null, s.AdminId)).Data;
        await Service(s.Context).UpdateGroupAsync(groupId, "Billing", null, new[] { Permissions.Payments.Collect }, s.AdminId);
        await Service(s.Context).AddMemberAsync(groupId, s.DoctorId, s.AdminId);

        Assert.True((await Service(s.Context).RemoveMemberAsync(groupId, s.DoctorId, s.AdminId)).IsSuccess);

        Assert.DoesNotContain(Permissions.Payments.Collect, await PermissionsOf(s.Context, s.DoctorId));
    }

    [Fact]
    public async Task Removing_a_permission_from_a_built_in_group_affects_its_role_members()
    {
        var s = await SeedAsync();
        var doctorsId = await BuiltIn(s.Context, SystemGroups.DoctorRole);
        var keep = (await Service(s.Context).GetGroupAsync(doctorsId))!.Permissions
            .Where(code => code != Permissions.Patients.EditMedical);

        Assert.True((await Service(s.Context).UpdateGroupAsync(doctorsId, "ignored", null, keep, s.AdminId)).IsSuccess);

        var permissions = await PermissionsOf(s.Context, s.DoctorId);
        Assert.DoesNotContain(Permissions.Patients.EditMedical, permissions);
        Assert.Contains(Permissions.Patients.View, permissions);
        Assert.Equal("Doctors", (await Service(s.Context).GetGroupAsync(doctorsId))!.Name);   // built-in names stay
    }

    [Fact]
    public async Task Administrators_always_keep_every_permission()
    {
        var s = await SeedAsync();
        var adminsId = await BuiltIn(s.Context, SystemGroups.AdminRole);

        var result = await Service(s.Context).UpdateGroupAsync(adminsId, "Administrators", "All", Array.Empty<string>(), s.AdminId);

        Assert.True(result.IsSuccess);
        Assert.Equal(PermissionCatalogue.All.Count, (await Service(s.Context).GetGroupAsync(adminsId))!.Permissions.Count);
    }

    [Fact]
    public async Task Built_in_groups_cannot_be_deleted_or_given_direct_members()
    {
        var s = await SeedAsync();
        var doctorsId = await BuiltIn(s.Context, SystemGroups.DoctorRole);

        Assert.False((await Service(s.Context).DeleteGroupAsync(doctorsId, s.AdminId)).IsSuccess);
        Assert.False((await Service(s.Context).AddMemberAsync(doctorsId, s.AdminId, s.AdminId)).IsSuccess);
        Assert.True(await s.Context.PermissionGroups.AnyAsync(g => g.Id == doctorsId));
    }

    [Fact]
    public async Task Custom_groups_can_be_deleted()
    {
        var s = await SeedAsync();
        var groupId = (await Service(s.Context).CreateGroupAsync("Temporary", null, s.AdminId)).Data;

        Assert.True((await Service(s.Context).DeleteGroupAsync(groupId, s.AdminId)).IsSuccess);

        Assert.False(await s.Context.PermissionGroups.AnyAsync(g => g.Id == groupId));
    }

    [Fact]
    public async Task Group_names_are_unique_ignoring_case()
    {
        var s = await SeedAsync();
        await Service(s.Context).CreateGroupAsync("Billing", null, s.AdminId);

        Assert.False((await Service(s.Context).CreateGroupAsync("billing", null, s.AdminId)).IsSuccess);
        Assert.False((await Service(s.Context).CreateGroupAsync("doctors", null, s.AdminId)).IsSuccess);
        Assert.False((await Service(s.Context).CreateGroupAsync("  ", null, s.AdminId)).IsSuccess);
    }

    [Fact]
    public async Task Unknown_permissions_are_refused()
    {
        var s = await SeedAsync();
        var groupId = (await Service(s.Context).CreateGroupAsync("Billing", null, s.AdminId)).Data;

        var result = await Service(s.Context).UpdateGroupAsync(groupId, "Billing", null, new[] { "Payments.Everything" }, s.AdminId);

        Assert.False(result.IsSuccess);
        Assert.Empty((await Service(s.Context).GetGroupAsync(groupId))!.Permissions);
    }

    [Fact]
    public async Task Built_in_group_lists_its_role_members()
    {
        var s = await SeedAsync();

        var doctors = await Service(s.Context).GetGroupAsync(await BuiltIn(s.Context, SystemGroups.DoctorRole));

        var member = Assert.Single(doctors!.Members);
        Assert.Equal(s.DoctorId, member.UserId);
        Assert.True(member.ViaRole);
    }
}
