using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using SafyaClinic.Application.Common;
using SafyaClinic.Application.DTOs.Auth;
using SafyaClinic.Application.DTOs.Common;
using SafyaClinic.Application.Interfaces.Services;
using SafyaClinic.Application.Security;
using SafyaClinic.Application.Services;
using SafyaClinic.Domain.Identity;
using SafyaClinic.Infrastructure.Data;
using SafyaClinic.Infrastructure.Repositories;
using SafyaClinic.Web.Controllers;
using SafyaClinic.Web.Localization;
using Xunit;

namespace SafyaClinic.Tests.Web;

/// <summary>F9c: a signed-in user's language is saved to their account and restored when they sign in.</summary>
public class PreferredLanguageTests
{
    private const string AdminPhone = "0100";
    private const string AdminPassword = "Language-Test-Password-1";

    internal sealed class RecordingPreferences : IUserPreferenceService
    {
        public List<(int UserId, string? Language)> Saved { get; } = new();

        public Task<ServiceResult> SetLanguageAsync(int userId, string? language)
        {
            Saved.Add((userId, language));
            return Task.FromResult(ServiceResult.Success());
        }
    }

    internal static LanguageController Controller(IUserPreferenceService preferences, int? signedInUserId)
    {
        var http = new DefaultHttpContext();
        if (signedInUserId is int id)
            http.User = new ClaimsPrincipal(new ClaimsIdentity(
                new[] { new Claim(ClaimTypes.NameIdentifier, id.ToString()) }, "test"));
        return new LanguageController(preferences, NullLogger<LanguageController>.Instance)
        {
            ControllerContext = new ControllerContext { HttpContext = http },
        };
    }

    private static async Task<SafyaDbContext> SeededAsync()
    {
        var context = new SafyaDbContext(new DbContextOptionsBuilder<SafyaDbContext>()
            .UseInMemoryDatabase($"language-{Guid.NewGuid()}").Options);
        await DbSeeder.SeedAsync(context, new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { [DbSeeder.AdminPasswordSettingKey] = AdminPassword })
            .Build());
        context.ChangeTracker.Clear();
        return context;
    }

    private static UserPreferenceService Preferences(SafyaDbContext context)
    {
        context.ChangeTracker.Clear();   // each call is a new request in the app
        return new UserPreferenceService(new UnitOfWork(context));
    }

    private static Task<User> Admin(SafyaDbContext context) =>
        context.Users.AsNoTracking().SingleAsync(u => u.PhoneNumber == AdminPhone);

    // ── Switch ──────────────────────────────────────────────────

    [Fact]
    public async Task Signed_in_user_choosing_a_language_saves_it_to_their_account()
    {
        var preferences = new RecordingPreferences();

        await Controller(preferences, signedInUserId: 7).Set("ar", "/");

        Assert.Equal((7, (string?)"ar"), Assert.Single(preferences.Saved));
    }

    [Fact]
    public async Task Choosing_a_language_on_the_sign_in_page_saves_nothing()
    {
        var preferences = new RecordingPreferences();
        var controller = Controller(preferences, signedInUserId: null);

        await controller.Set("ar", "/Auth/Login");

        Assert.Empty(preferences.Saved);
        Assert.Contains(Uri.EscapeDataString("c=en-US|uic=ar"), controller.Response.Headers.SetCookie.ToString());
    }

    // ── Saving ──────────────────────────────────────────────────

    [Fact]
    public async Task Language_is_saved_in_lower_case()
    {
        await using var context = await SeededAsync();
        var admin = await Admin(context);

        var result = await Preferences(context).SetLanguageAsync(admin.Id, " AR ");

        Assert.True(result.IsSuccess);
        Assert.Equal("ar", (await Admin(context)).PreferredLanguage);
    }

    [Theory]
    [InlineData("fr")]
    [InlineData("")]
    [InlineData(null)]
    public async Task Other_languages_are_refused_and_nothing_changes(string? language)
    {
        await using var context = await SeededAsync();
        var admin = await Admin(context);

        var result = await Preferences(context).SetLanguageAsync(admin.Id, language);

        Assert.False(result.IsSuccess);
        Assert.Null((await Admin(context)).PreferredLanguage);
    }

    [Fact]
    public async Task Unknown_user_is_refused()
    {
        await using var context = await SeededAsync();

        Assert.False((await Preferences(context).SetLanguageAsync(987654, "ar")).IsSuccess);
    }

    [Fact]
    public async Task Changing_the_language_does_not_sign_the_user_out_elsewhere()
    {
        await using var context = await SeededAsync();
        var before = await Admin(context);

        await Preferences(context).SetLanguageAsync(before.Id, "ar");

        Assert.Equal(before.SecurityStamp, (await Admin(context)).SecurityStamp);
    }

    // ── Sign-in ─────────────────────────────────────────────────

    [Fact]
    public async Task Sign_in_returns_the_saved_language()
    {
        await using var context = await SeededAsync();
        await Preferences(context).SetLanguageAsync((await Admin(context)).Id, "ar");
        context.ChangeTracker.Clear();

        var auth = new AuthService(new UnitOfWork(context), NullLogger<AuthService>.Instance,
            new LockoutSettings { MaxFailedAttempts = 5, LockoutMinutes = 15 });
        var result = await auth.LoginAsync(new LoginRequest { PhoneNumber = AdminPhone, Password = AdminPassword });

        Assert.True(result.IsSuccess);
        Assert.Equal("ar", result.Data!.PreferredLanguage);
    }

    [Fact]
    public async Task A_new_user_has_no_saved_language_so_English_applies()
    {
        await using var context = await SeededAsync();

        Assert.Null((await Admin(context)).PreferredLanguage);
        Assert.Equal("en", AppCultures.English);
    }

    // ── One list of languages ───────────────────────────────────

    [Fact]
    public void The_switch_and_the_account_offer_the_same_languages()
    {
        Assert.Equal(UserLanguages.All, AppCultures.UiCultures);
        Assert.Null(UserLanguages.Normalize("fr"));
        Assert.Equal("ar", UserLanguages.Normalize("Ar"));
    }

    [Fact]
    public void Preferred_language_is_a_short_optional_column()
    {
        var model = new SafyaDbContext(new DbContextOptionsBuilder<SafyaDbContext>()
            .UseSqlServer("Server=model-only;Database=ModelOnly;Integrated Security=True").Options).Model;

        var property = model.FindEntityType(typeof(User))!.FindProperty(nameof(User.PreferredLanguage))!;

        Assert.True(property.IsNullable);
        Assert.Equal(5, property.GetMaxLength());
    }
}
