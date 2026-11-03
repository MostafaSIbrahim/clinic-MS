using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using SafyaClinic.Application.Security;
using SafyaClinic.Application.Services;
using SafyaClinic.Domain.Enums;
using SafyaClinic.Domain.Identity;
using SafyaClinic.Infrastructure.Data;
using SafyaClinic.Infrastructure.Repositories;
using Xunit;

namespace SafyaClinic.Tests.Application;

/// <summary>S6 / A3 (F4d: administrators are the Administrators group): no self-deactivation, the last Admin is protected, changes renew the stamp.</summary>
public class UserServiceAdminGuardTests
{
    private sealed record Setup(SafyaDbContext Context, int AdminId, int DoctorId);

    private static async Task<Setup> SeedAsync()
    {
        var context = new SafyaDbContext(new DbContextOptionsBuilder<SafyaDbContext>()
            .UseInMemoryDatabase($"guards-{Guid.NewGuid()}")
            .Options);
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { [DbSeeder.AdminPasswordSettingKey] = "Guard-Test-Password-1" })
            .Build();
        await DbSeeder.SeedAsync(context, configuration);

        var doctor = new User { FullName = "Doctor", PhoneNumber = "0199", PasswordHash = "x", SecurityStamp = "doctor-stamp" };
        context.Users.Add(doctor);
        await context.SaveChangesAsync();
        await TestData.AddToBuiltInGroupAsync(context, doctor.Id, SystemGroups.DoctorRole);

        var adminId = await context.Users.Where(u => u.PhoneNumber == "0100").Select(u => u.Id).SingleAsync();
        context.ChangeTracker.Clear();
        return new Setup(context, adminId, doctor.Id);
    }

    private static UserService Service(SafyaDbContext context)
    {
        context.ChangeTracker.Clear();   // each call is a new request in the app
        return new UserService(new UnitOfWork(context));
    }

    private static Task<User> Load(SafyaDbContext context, int id) =>
        context.Users.AsNoTracking().SingleAsync(u => u.Id == id);

    [Fact]
    public async Task Admin_cannot_deactivate_their_own_account()
    {
        var s = await SeedAsync();

        var result = await Service(s.Context).SetUserActiveAsync(s.AdminId, isActive: false, actingUserId: s.AdminId);

        Assert.False(result.IsSuccess);
        Assert.True((await Load(s.Context, s.AdminId)).IsActive);
    }

    [Fact]
    public async Task Last_active_admin_cannot_be_deactivated()
    {
        var s = await SeedAsync();

        var result = await Service(s.Context).SetUserActiveAsync(s.AdminId, isActive: false, actingUserId: s.DoctorId);

        Assert.False(result.IsSuccess);
        Assert.True((await Load(s.Context, s.AdminId)).IsActive);
    }

    [Fact]
    public async Task Admin_can_be_deactivated_when_another_active_admin_exists()
    {
        var s = await SeedAsync();
        await TestData.AddToBuiltInGroupAsync(s.Context, s.DoctorId, SystemGroups.AdminRole);

        var result = await Service(s.Context).SetUserActiveAsync(s.AdminId, isActive: false, actingUserId: s.DoctorId);

        Assert.True(result.IsSuccess);
        Assert.False((await Load(s.Context, s.AdminId)).IsActive);
    }

    [Fact]
    public async Task Deactivating_a_user_renews_their_stamp()
    {
        var s = await SeedAsync();

        var result = await Service(s.Context).SetUserActiveAsync(s.DoctorId, isActive: false, actingUserId: s.AdminId);

        Assert.True(result.IsSuccess);
        var doctor = await Load(s.Context, s.DoctorId);
        Assert.False(doctor.IsActive);
        Assert.NotEqual("doctor-stamp", doctor.SecurityStamp);
    }

    [Fact]
    public async Task Clinician_type_change_renews_the_stamp()
    {
        var s = await SeedAsync();

        var result = await Service(s.Context).SetClinicianTypeAsync(s.DoctorId, ClinicianType.Nutritionist, s.AdminId);

        Assert.True(result.IsSuccess);
        Assert.NotEqual("doctor-stamp", (await Load(s.Context, s.DoctorId)).SecurityStamp);
    }
}
