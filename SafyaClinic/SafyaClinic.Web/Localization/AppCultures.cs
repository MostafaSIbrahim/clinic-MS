using System.Globalization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Localization;

namespace SafyaClinic.Web.Localization;

/// <summary>
/// F9: English by default, Arabic labels on request (K5). Only the UI language changes: numbers, dates and
/// money are always formatted (and form values parsed) with one culture, so digits stay Western (D10).
/// The choice is kept in the standard culture cookie; browsers' language settings are ignored.
/// </summary>
public static class AppCultures
{
    public const string English = "en";
    public const string Arabic = "ar";

    /// <summary>The one culture used for formatting and for reading form values.</summary>
    public const string FormattingCulture = "en-US";

    public static IReadOnlyList<string> UiCultures { get; } = new[] { English, Arabic };

    public static bool IsSupported(string? uiCulture) =>
        uiCulture is not null && UiCultures.Contains(uiCulture, StringComparer.OrdinalIgnoreCase);

    public static void Configure(RequestLocalizationOptions options)
    {
        options.DefaultRequestCulture = new RequestCulture(FormattingCulture, English);
        options.SupportedCultures = new List<CultureInfo> { new(FormattingCulture) };
        options.SupportedUICultures = UiCultures.Select(c => new CultureInfo(c)).ToList();
        options.FallBackToParentCultures = false;
        options.FallBackToParentUICultures = true;
        options.RequestCultureProviders = new List<IRequestCultureProvider> { new CookieRequestCultureProvider() };
    }

    /// <summary>The culture cookie value for a UI language.</summary>
    public static string CookieValue(string uiCulture) =>
        CookieRequestCultureProvider.MakeCookieValue(new RequestCulture(FormattingCulture, uiCulture.ToLowerInvariant()));

    /// <summary>A return address on this site, or "/" (never an address on another site).</summary>
    public static string SafeReturnUrl(string? returnUrl) =>
        !string.IsNullOrEmpty(returnUrl)
        && returnUrl[0] == '/'
        && (returnUrl.Length == 1 || (returnUrl[1] != '/' && returnUrl[1] != '\\'))
            ? returnUrl
            : "/";
}
