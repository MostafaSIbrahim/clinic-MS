using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using SafyaClinic.Application.DTOs.Auth;
using SafyaClinic.Application.Services;
using SafyaClinic.Infrastructure.Data;
using SafyaClinic.Infrastructure.Repositories;
using Xunit;

namespace SafyaClinic.Tests.Application;

/// <summary>
/// Sign-in must work with no Jwt settings at all (slice S2).
/// Uses the seeded administrator on an in-memory database.
/// </summary>
public class AuthServiceLoginTests
{
    private const string AdminPhone = "0100";
    private const string AdminPassword = "Login-Test-Password-1";

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

    [Fact]
    public async Task Admin_can_sign_in_without_jwt_configuration()
    {
        await using var context = await SeededContextAsync();
        var service = new AuthService(new UnitOfWork(context));

        var result = await service.LoginAsync(new LoginRequest { PhoneNumber = AdminPhone, Password = AdminPassword });

        Assert.True(result.IsSuccess);
        Assert.Contains("Admin", result.Data!.Roles);
        Assert.True(result.Data.ExpiresAt > DateTime.UtcNow);
    }

    [Fact]
    public async Task Wrong_password_is_rejected()
    {
        await using var context = await SeededContextAsync();
        var service = new AuthService(new UnitOfWork(context));

        var result = await service.LoginAsync(new LoginRequest { PhoneNumber = AdminPhone, Password = "wrong-password" });

        Assert.False(result.IsSuccess);
    }
}