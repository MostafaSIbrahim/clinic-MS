using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc;
using SafyaClinic.Web.Localization;

namespace SafyaClinic.Web.Controllers;

/// <summary>
/// F9: the language switch (top bar and sign-in page). Remembers the choice for a year on this browser.
/// Open to everyone so the sign-in page can switch too; the anti-forgery token is still required.
/// </summary>
[Authorize]
public class LanguageController : BaseController
{
    [AllowAnonymous]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Set(string? culture, string? returnUrl)
    {
        if (!AppCultures.IsSupported(culture)) return BadRequest();

        Response.Cookies.Append(
            CookieRequestCultureProvider.DefaultCookieName,
            AppCultures.CookieValue(culture!),
            new CookieOptions
            {
                Expires = DateTimeOffset.UtcNow.AddYears(1),
                IsEssential = true,
                HttpOnly = true,
                Secure = Request.IsHttps,
                SameSite = SameSiteMode.Lax,
            });

        return LocalRedirect(AppCultures.SafeReturnUrl(returnUrl));
    }
}
