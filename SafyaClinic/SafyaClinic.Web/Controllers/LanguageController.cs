using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SafyaClinic.Application.Interfaces.Services;
using SafyaClinic.Web.Localization;

namespace SafyaClinic.Web.Controllers;

/// <summary>
/// F9: the language switch (top bar and sign-in page). Remembers the choice on this browser for a year
/// and, for a signed-in user, saves it to their account so it follows them to other browsers (F9c).
/// Open to everyone so the sign-in page can switch too; the anti-forgery token is still required.
/// </summary>
[Authorize]
public class LanguageController : BaseController
{
    private readonly IUserPreferenceService _preferences;
    private readonly ILogger<LanguageController> _logger;

    public LanguageController(IUserPreferenceService preferences, ILogger<LanguageController> logger)
    {
        _preferences = preferences;
        _logger = logger;
    }

    [AllowAnonymous]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Set(string? culture, string? returnUrl)
    {
        if (!AppCultures.IsSupported(culture)) return BadRequest();

        AppCultures.AppendCookie(Response, culture!);

        if (User.Identity?.IsAuthenticated == true)
        {
            var saved = await _preferences.SetLanguageAsync(CurrentUserId, culture);
            if (!saved.IsSuccess)
                _logger.LogWarning("Could not save the language for user {UserId}: {Error}", CurrentUserId, saved.Errors.FirstOrDefault());
        }

        return LocalRedirect(AppCultures.SafeReturnUrl(returnUrl));
    }
}
