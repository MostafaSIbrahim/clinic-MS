using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using SafyaClinic.Application.Common.Auditing;
using SafyaClinic.Application.DTOs.Auth;
using SafyaClinic.Application.DTOs.Patient;
using SafyaClinic.Application.Security;
using SafyaClinic.Application.Services;
using SafyaClinic.Domain.Entities.Audit;
using SafyaClinic.Domain.Identity;
using SafyaClinic.Infrastructure.Data;
using SafyaClinic.Infrastructure.Repositories;
using Xunit;

namespace SafyaClinic.Tests.Application;

/// <summary>F3b: security events are written to AuditLogs with who, what, where — never secrets.</summary>
public class AuditTrailTests
{
    private const string AdminPhone = "0100";
    private const string AdminPassword = "Audit-Test-Password-1";

    private sealed class FixedContext : IAuditContext
    {
        public int? UserId { get; init; }
        public string? IpAddress => "10.0.0.1";
        public string? RequestId => "REQ-1";
    }

    private static async Task<SafyaDbContext> SeededAsync()
    {
        var context = new SafyaDbContext(new DbContextOptionsBuilder<SafyaDbContext>()
            .UseInMemoryDatabase($"audit-{Guid.NewGuid()}").Options);
        await DbSeeder.SeedAsync(context, new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { [DbSeeder.AdminPasswordSettingKey] = AdminPassword })
            .Build());
        context.ChangeTracker.Clear();
        return context;
    }

    private static AuthService Auth(SafyaDbContext context, int? actingUserId = null)
    {
        context.ChangeTracker.Clear();   // each call is a new request in the app
        var uow = new UnitOfWork(context);
        return new AuthService(uow, NullLogger<AuthService>.Instance,
            new LockoutSettings { MaxFailedAttempts = 3, LockoutMinutes = 15 },
            new AuditTrail(uow, new FixedContext { UserId = actingUserId }));
    }

    private static UserService Users(SafyaDbContext context, int actingUserId)
    {
        context.ChangeTracker.Clear();
        var uow = new UnitOfWork(context);
        return new UserService(uow, new AuditTrail(uow, new FixedContext { UserId = actingUserId }));
    }

    private static Task<List<AuditLog>> EntriesAsync(SafyaDbContext context) =>
        context.AuditLogs.AsNoTracking().OrderBy(a => a.Id).ToListAsync();

    private static Task<int> AdminIdAsync(SafyaDbContext context) =>
        context.Users.Where(u => u.PhoneNumber == AdminPhone).Select(u => u.Id).SingleAsync();

    [Fact]
    public async Task Successful_sign_in_is_recorded_with_who_and_where()
    {
        await using var context = await SeededAsync();
        var adminId = await AdminIdAsync(context);

        await Auth(context).LoginAsync(new LoginRequest { PhoneNumber = AdminPhone, Password = AdminPassword });

        var entry = Assert.Single(await EntriesAsync(context));
        Assert.Equal(AuditActions.SignInSucceeded, entry.Action);
        Assert.Equal(adminId, entry.EntityId);
        Assert.Equal(adminId, entry.UserId);
        Assert.Equal("10.0.0.1", entry.IpAddress);
        Assert.Equal("REQ-1", entry.RequestId);
    }

    [Fact]
    public async Task Wrong_password_is_recorded_without_the_password()
    {
        await using var context = await SeededAsync();

        await Auth(context).LoginAsync(new LoginRequest { PhoneNumber = AdminPhone, Password = "wrong-password-xyz" });

        var entry = Assert.Single(await EntriesAsync(context));
        Assert.Equal(AuditActions.SignInFailed, entry.Action);
        Assert.Null(entry.UserId);
        Assert.DoesNotContain("wrong-password-xyz", entry.Details ?? "");
    }

    [Fact]
    public async Task Unknown_account_is_recorded_without_the_phone_number()
    {
        await using var context = await SeededAsync();

        await Auth(context).LoginAsync(new LoginRequest { PhoneNumber = "0999999", Password = "whatever-123" });

        var entry = Assert.Single(await EntriesAsync(context));
        Assert.Equal(AuditActions.SignInFailed, entry.Action);
        Assert.Null(entry.EntityId);
        Assert.DoesNotContain("0999999", entry.Details ?? "");
    }

    [Fact]
    public async Task Lockout_is_recorded()
    {
        await using var context = await SeededAsync();
        for (var i = 0; i < 3; i++)
            await Auth(context).LoginAsync(new LoginRequest { PhoneNumber = AdminPhone, Password = "wrong-password" });

        var actions = (await EntriesAsync(context)).Select(e => e.Action).ToList();

        Assert.Equal(new[] { AuditActions.SignInFailed, AuditActions.SignInFailed, AuditActions.SignInLockedOut }, actions);
    }

    [Fact]
    public async Task Deactivation_is_recorded_with_the_acting_admin()
    {
        await using var context = await SeededAsync();
        var adminId = await AdminIdAsync(context);
        var doctor = new User { FullName = "Doctor", PhoneNumber = "0301", PasswordHash = "x", SecurityStamp = "s" };
        context.Users.Add(doctor);
        await context.SaveChangesAsync();

        await Users(context, adminId).SetUserActiveAsync(doctor.Id, isActive: false, actingUserId: adminId);

        var entry = Assert.Single(await EntriesAsync(context));
        Assert.Equal(AuditActions.UserDeactivated, entry.Action);
        Assert.Equal(doctor.Id, entry.EntityId);
        Assert.Equal(adminId, entry.UserId);
    }

    [Fact]
    public async Task Password_reset_is_recorded_without_the_password()
    {
        await using var context = await SeededAsync();
        var adminId = await AdminIdAsync(context);

        await Users(context, adminId).ResetPasswordAsync(new ResetPasswordRequest
        {
            UserId = adminId,
            NewPassword = "Temporary-pass-9",
            ConfirmPassword = "Temporary-pass-9"
        });

        var entry = Assert.Single(await EntriesAsync(context));
        Assert.Equal(AuditActions.PasswordReset, entry.Action);
        Assert.DoesNotContain("Temporary-pass-9", entry.Details ?? "");
    }

    [Fact]
    public async Task Role_assignment_is_recorded()
    {
        await using var context = await SeededAsync();
        var adminId = await AdminIdAsync(context);
        var receptionRoleId = await context.Roles.Where(r => r.RoleName == "Reception").Select(r => r.Id).SingleAsync();

        await Users(context, adminId).AssignRoleAsync(adminId, receptionRoleId, adminId);

        var entry = Assert.Single(await EntriesAsync(context));
        Assert.Equal(AuditActions.RoleAssigned, entry.Action);
        Assert.Contains(receptionRoleId.ToString(), entry.Details);
    }
}