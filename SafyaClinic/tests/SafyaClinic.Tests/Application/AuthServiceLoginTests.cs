using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using SafyaClinic.Application.DTOs.Auth;
using SafyaClinic.Application.DTOs.Patient;
using SafyaClinic.Application.Security;
using SafyaClinic.Application.Services;
using SafyaClinic.Infrastructure.Data;
using SafyaClinic.Infrastructure.Repositories;
using Xunit;

namespace SafyaClinic.Tests.Application;

/// <summary>
/// S2: sign-in works with no Jwt settings. S5: lockout, hash upgrade, admin reset.
/// Uses the seeded administrator on an in-memory database.
/// </summary>
public class AuthServiceLoginTests
{
    private const string AdminPhone = "0100";
    private const string AdminPassword = "Login-Test-Password-1";
    private const int MaxAttempts = 3;

    private static async Task<SafyaDbContext> SeededContextAsync()
    {
        var context = new SafyaDbContext(new DbContextOptionsBuilder<SafyaDbContext>()
            .UseInMemoryDatabase($"login-{Guid.NewGuid()}")
            .Options);

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [DbSeeder.AdminPasswordSettingKey] = AdminPassword
            })
            .Build();

        await DbSeeder.SeedAsync(context, configuration);

        // A real request starts with an empty change tracker; the seeder left
        // its entities tracked, which would clash with the login's Update().
        context.ChangeTracker.Clear();
        return context;
    }

    private static AuthService Auth(SafyaDbContext context) =>
        new(new UnitOfWork(context), NullLogger<AuthService>.Instance,
            new LockoutSettings { MaxFailedAttempts = MaxAttempts, LockoutMinutes = 15 });

    // In the app every sign-in is a new request with a fresh DbContext; mimic that here.
    private static Task<SafyaClinic.Application.DTOs.Common.ServiceResult<LoginResponse>> SignIn(
        SafyaDbContext context, string password)
    {
        context.ChangeTracker.Clear();
        return Auth(context).LoginAsync(new LoginRequest { PhoneNumber = AdminPhone, Password = password });
    }

    private static Task<SafyaClinic.Domain.Identity.User> Admin(SafyaDbContext context) =>
        context.Users.AsNoTracking().SingleAsync(u => u.PhoneNumber == AdminPhone);

    [Fact]
    public async Task Admin_can_sign_in_without_jwt_configuration()
    {
        await using var context = await SeededContextAsync();

        var result = await SignIn(context, AdminPassword);

        Assert.True(result.IsSuccess);
        Assert.Contains("Admin", result.Data!.Roles);
        Assert.True(result.Data.ExpiresAt > DateTime.UtcNow);
        Assert.False(result.Data.MustChangePassword);
    }

    [Fact]
    public async Task Wrong_password_is_rejected()
    {
        await using var context = await SeededContextAsync();

        var result = await SignIn(context, "wrong-password");

        Assert.False(result.IsSuccess);
        Assert.Equal(1, (await Admin(context)).FailedLoginCount);
    }

    [Fact]
    public async Task Repeated_failures_lock_the_account_even_for_the_right_password()
    {
        await using var context = await SeededContextAsync();
        for (var i = 0; i < MaxAttempts; i++)
            await SignIn(context, "wrong-password");

        var result = await SignIn(context, AdminPassword);

        Assert.False(result.IsSuccess);
        Assert.Contains("locked", result.Errors.First());
        Assert.NotNull((await Admin(context)).LockoutEndUtc);
    }

    [Fact]
    public async Task Expired_lockout_allows_sign_in_and_clears_the_counter()
    {
        await using var context = await SeededContextAsync();
        var admin = await context.Users.SingleAsync(u => u.PhoneNumber == AdminPhone);
        admin.LockoutEndUtc = DateTime.UtcNow.AddMinutes(-1);
        admin.FailedLoginCount = 2;
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        var result = await SignIn(context, AdminPassword);

        Assert.True(result.IsSuccess);
        var stored = await Admin(context);
        Assert.Equal(0, stored.FailedLoginCount);
        Assert.Null(stored.LockoutEndUtc);
    }

    [Fact]
    public async Task Old_hash_is_upgraded_at_sign_in()
    {
        await using var context = await SeededContextAsync();
        var salt = RandomNumberGenerator.GetBytes(16);
        var legacy = Convert.ToBase64String(salt) + "." + Convert.ToBase64String(
            SHA256.HashData(Encoding.UTF8.GetBytes(AdminPassword).Concat(salt).ToArray()));
        var admin = await context.Users.SingleAsync(u => u.PhoneNumber == AdminPhone);
        admin.PasswordHash = legacy;
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        var result = await SignIn(context, AdminPassword);

        Assert.True(result.IsSuccess);
        var stored = await Admin(context);
        Assert.DoesNotContain(".", stored.PasswordHash);
        Assert.Equal(PasswordCheck.Success, PasswordHashing.Verify(AdminPassword, stored.PasswordHash));
    }

    [Fact]
    public async Task Admin_reset_forces_a_new_password_at_next_sign_in()
    {
        await using var context = await SeededContextAsync();
        var adminId = (await Admin(context)).Id;
        const string temporary = "Temporary-pass-1";
        const string chosen = "Brand-new-pass-1";

        var reset = await new UserService(new UnitOfWork(context)).ResetPasswordAsync(
            new ResetPasswordRequest { UserId = adminId, NewPassword = temporary, ConfirmPassword = temporary });
        context.ChangeTracker.Clear();
        var firstSignIn = await SignIn(context, temporary);
        var change = await Auth(context).ChangePasswordAsync(adminId,
            new ChangePasswordRequest { CurrentPassword = temporary, NewPassword = chosen, ConfirmPassword = chosen });
        var secondSignIn = await SignIn(context, chosen);

        Assert.True(reset.IsSuccess);
        Assert.True(firstSignIn.Data!.MustChangePassword);
        Assert.True(change.IsSuccess);
        Assert.False(secondSignIn.Data!.MustChangePassword);
    }

    [Fact]
    public async Task Short_new_password_is_refused()
    {
        await using var context = await SeededContextAsync();
        var adminId = (await Admin(context)).Id;

        var change = await Auth(context).ChangePasswordAsync(adminId,
            new ChangePasswordRequest { CurrentPassword = AdminPassword, NewPassword = "short", ConfirmPassword = "short" });

        Assert.False(change.IsSuccess);
    }

    [Fact]
    public async Task Sign_in_returns_the_users_security_stamp()
    {
        await using var context = await SeededContextAsync();

        var result = await SignIn(context, AdminPassword);

        Assert.False(string.IsNullOrEmpty(result.Data!.SecurityStamp));
        Assert.Equal((await Admin(context)).SecurityStamp, result.Data.SecurityStamp);
    }

    [Fact]
    public async Task Password_change_renews_the_stamp()
    {
        await using var context = await SeededContextAsync();
        var before = await Admin(context);

        context.ChangeTracker.Clear();
        var change = await Auth(context).ChangePasswordAsync(before.Id,
            new ChangePasswordRequest { CurrentPassword = AdminPassword, NewPassword = "Brand-new-pass-1", ConfirmPassword = "Brand-new-pass-1" });

        Assert.True(change.IsSuccess);
        Assert.NotEqual(before.SecurityStamp, (await Admin(context)).SecurityStamp);
    }
}