using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using SafyaClinic.Application.DTOs.Patient;
using SafyaClinic.Application.Services;
using SafyaClinic.Domain.Enums;
using SafyaClinic.Domain.Identity;
using SafyaClinic.Infrastructure.Data;
using SafyaClinic.Infrastructure.Repositories;
using Xunit;

namespace SafyaClinic.Tests.Application;

/// <summary>F4a: doctor pickers use ClinicianType, which follows the Doctor and Nutritionist roles.</summary>
public class ClinicianTypeTests
{
    private static async Task<(SafyaDbContext Context, int AdminId)> SeedAsync()
    {
        var context = new SafyaDbContext(new DbContextOptionsBuilder<SafyaDbContext>()
            .UseInMemoryDatabase($"clinicians-{Guid.NewGuid()}")
            .Options);
        await DbSeeder.SeedAsync(context, new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { [DbSeeder.AdminPasswordSettingKey] = "Clinician-Test-Password-1" })
            .Build());
        var adminId = await context.Users.Select(u => u.Id).SingleAsync();
        context.ChangeTracker.Clear();
        return (context, adminId);
    }

    private static UserService Service(SafyaDbContext context)
    {
        context.ChangeTracker.Clear();   // each call is a new request in the app
        return new UserService(new UnitOfWork(context));
    }

    private static Task<int> RoleId(SafyaDbContext context, string name) =>
        context.Roles.Where(r => r.RoleName == name).Select(r => r.Id).SingleAsync();

    private static async Task<int> AddUserAsync(SafyaDbContext context, string phone,
        ClinicianType type = ClinicianType.None, bool active = true)
    {
        var user = new User
        {
            FullName = "User " + phone,
            PhoneNumber = phone,
            PasswordHash = "x",
            SecurityStamp = "stamp",
            ClinicianType = type,
            IsActive = active
        };
        context.Users.Add(user);
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();
        return user.Id;
    }

    private static Task<ClinicianType> TypeOf(SafyaDbContext context, int userId) =>
        context.Users.AsNoTracking().Where(u => u.Id == userId).Select(u => u.ClinicianType).SingleAsync();

    [Fact]
    public async Task Assigning_and_removing_the_doctor_role_updates_the_clinician_type()
    {
        var (context, adminId) = await SeedAsync();
        var userId = await AddUserAsync(context, "0155");
        var doctorRoleId = await RoleId(context, "Doctor");

        Assert.True((await Service(context).AssignRoleAsync(userId, doctorRoleId, adminId)).IsSuccess);
        Assert.Equal(ClinicianType.Doctor, await TypeOf(context, userId));

        Assert.True((await Service(context).RemoveRoleAsync(userId, doctorRoleId)).IsSuccess);
        Assert.Equal(ClinicianType.None, await TypeOf(context, userId));
    }

    [Fact]
    public async Task Creating_a_nutritionist_sets_the_clinician_type()
    {
        var (context, adminId) = await SeedAsync();
        var request = new CreateUserRequest
        {
            FullName = "Nutritionist",
            PhoneNumber = "0166",
            Password = "Create-Test-Password-1",
            ConfirmPassword = "Create-Test-Password-1",
            RoleIds = new() { await RoleId(context, "Nutritionist") }
        };

        var result = await Service(context).CreateUserAsync(request, adminId);

        Assert.True(result.IsSuccess);
        Assert.Equal(ClinicianType.Nutritionist, await TypeOf(context, result.Data!.Id));
    }

    [Fact]
    public async Task Doctor_list_contains_only_active_clinicians()
    {
        var (context, _) = await SeedAsync();
        var doctorId = await AddUserAsync(context, "0177", ClinicianType.Doctor);
        var nutritionistId = await AddUserAsync(context, "0188", ClinicianType.Nutritionist);
        await AddUserAsync(context, "0199", ClinicianType.Doctor, active: false);
        await AddUserAsync(context, "0111");

        var result = await Service(context).GetDoctorsAsync();

        Assert.True(result.IsSuccess);
        Assert.Equal(new[] { doctorId, nutritionistId }, result.Data!.Select(d => d.Id).OrderBy(id => id));
    }
}