using System.Globalization;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using SafyaClinic.Application.DTOs.Auth;
using SafyaClinic.Application.DTOs.Common;
using SafyaClinic.Application.DTOs.Patient;
using SafyaClinic.Application.Interfaces.Services;
using SafyaClinic.Web.Security;
using Xunit;

namespace SafyaClinic.Tests.Web.Security;

/// <summary>S6 / F04: open sessions end when a user is deactivated or their stamp changes.</summary>
public class SessionValidatorTests
{
    private const int UserId = 7;

    private sealed class StampSource : IAuthService
    {
        public string? Stamp { get; set; }
        public int Calls { get; private set; }

        public Task<string?> GetActiveSecurityStampAsync(int userId)
        {
            Calls++;
            return Task.FromResult(userId == UserId ? Stamp : null);
        }

        public Task<ServiceResult<LoginResponse>> LoginAsync(LoginRequest request) => throw new NotSupportedException();
        public Task<ServiceResult> ChangePasswordAsync(int userId, ChangePasswordRequest request) => throw new NotSupportedException();
        public Task<ServiceResult<UserDto>> GetCurrentUserAsync(int userId) => throw new NotSupportedException();
    }

    private static CookieValidatePrincipalContext Context(StampSource source, string? stampClaim, DateTimeOffset? lastCheck = null)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme).AddCookie();
        services.AddSingleton<IAuthService>(source);
        var httpContext = new DefaultHttpContext { RequestServices = services.BuildServiceProvider() };

        var claims = new List<Claim> { new(ClaimTypes.NameIdentifier, UserId.ToString()) };
        if (stampClaim is not null) claims.Add(new Claim(AuthClaims.SecurityStamp, stampClaim));
        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme));

        var properties = new AuthenticationProperties();
        if (lastCheck is not null)
            properties.Items[SessionValidator.LastCheckKey] = lastCheck.Value.ToString("O", CultureInfo.InvariantCulture);

        var scheme = new AuthenticationScheme(CookieAuthenticationDefaults.AuthenticationScheme, null, typeof(CookieAuthenticationHandler));
        return new CookieValidatePrincipalContext(httpContext, scheme, new CookieAuthenticationOptions(),
            new AuthenticationTicket(principal, properties, scheme.Name));
    }

    [Fact]
    public async Task Matching_stamp_keeps_the_session_and_records_the_check()
    {
        var context = Context(new StampSource { Stamp = "abc" }, "abc");

        await SessionValidator.ValidateAsync(context);

        Assert.NotNull(context.Principal);
        Assert.True(context.ShouldRenew);
        Assert.True(context.Properties.Items.ContainsKey(SessionValidator.LastCheckKey));
    }

    [Fact]
    public async Task Changed_stamp_ends_the_session()
    {
        var context = Context(new StampSource { Stamp = "new" }, "old");

        await SessionValidator.ValidateAsync(context);

        Assert.Null(context.Principal);
    }

    [Fact]
    public async Task Deactivated_user_is_signed_out()
    {
        var context = Context(new StampSource { Stamp = null }, "abc");

        await SessionValidator.ValidateAsync(context);

        Assert.Null(context.Principal);
    }

    [Fact]
    public async Task Cookie_without_a_stamp_is_signed_out()
    {
        var context = Context(new StampSource { Stamp = "abc" }, stampClaim: null);

        await SessionValidator.ValidateAsync(context);

        Assert.Null(context.Principal);
    }

    [Fact]
    public async Task Recently_checked_session_does_not_hit_the_database()
    {
        var source = new StampSource { Stamp = "new" };
        var context = Context(source, "old", lastCheck: DateTimeOffset.UtcNow.AddSeconds(-10));

        await SessionValidator.ValidateAsync(context);

        Assert.Equal(0, source.Calls);
        Assert.NotNull(context.Principal);
    }

    [Fact]
    public async Task Check_runs_again_after_the_interval()
    {
        var source = new StampSource { Stamp = "new" };
        var context = Context(source, "old",
            lastCheck: DateTimeOffset.UtcNow - SessionValidator.CheckInterval - TimeSpan.FromSeconds(1));

        await SessionValidator.ValidateAsync(context);

        Assert.Equal(1, source.Calls);
        Assert.Null(context.Principal);
    }
}