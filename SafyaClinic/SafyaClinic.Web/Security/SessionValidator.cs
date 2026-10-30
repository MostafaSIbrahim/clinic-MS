using System.Globalization;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using SafyaClinic.Application.Interfaces.Services;

namespace SafyaClinic.Web.Security;

/// <summary>
/// Ends a signed-in session when the user is deactivated or their security stamp has changed
/// (password change or reset, role change). The database is checked at most once per
/// <see cref="CheckInterval"/> for each browser session (S6).
/// </summary>
public static class SessionValidator
{
    public static readonly TimeSpan CheckInterval = TimeSpan.FromMinutes(1);
    public const string LastCheckKey = "safya.stamp_checked";

    public static async Task ValidateAsync(CookieValidatePrincipalContext context)
    {
        var now = DateTimeOffset.UtcNow;
        if (context.Properties.Items.TryGetValue(LastCheckKey, out var text)
            && DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var lastCheck)
            && now - lastCheck < CheckInterval)
        {
            return;
        }

        var stamp = context.Principal?.FindFirstValue(AuthClaims.SecurityStamp);
        if (!int.TryParse(context.Principal?.FindFirstValue(ClaimTypes.NameIdentifier), out var userId)
            || string.IsNullOrEmpty(stamp))
        {
            await RejectAsync(context);   // e.g. a cookie issued before S6
            return;
        }

        var auth = context.HttpContext.RequestServices.GetRequiredService<IAuthService>();
        var current = await auth.GetActiveSecurityStampAsync(userId);
        if (current != stamp)
        {
            await RejectAsync(context);   // deactivated, or stamp renewed
            return;
        }

        context.Properties.Items[LastCheckKey] = now.ToString("O", CultureInfo.InvariantCulture);
        context.ShouldRenew = true;      // store the check time in the cookie
    }

    private static async Task RejectAsync(CookieValidatePrincipalContext context)
    {
        context.RejectPrincipal();
        await context.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    }
}