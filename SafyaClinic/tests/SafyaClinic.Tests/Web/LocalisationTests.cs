using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using SafyaClinic.Web;
using SafyaClinic.Web.Controllers;
using SafyaClinic.Web.Localization;
using SafyaClinic.Web.Navigation;
using Xunit;

namespace SafyaClinic.Tests.Web;

/// <summary>F9: English by default, Arabic labels on request, right-to-left layout, Western digits.</summary>
public class LocalisationTests
{
    private static string WebProject([CallerFilePath] string source = "") =>
        Path.GetFullPath(Path.Combine(Path.GetDirectoryName(source)!, "..", "..", "..", "SafyaClinic.Web"));

    private static Dictionary<string, string> Arabic() =>
        XDocument.Load(Path.Combine(WebProject(), "Resources", "SharedResource.ar.resx"))
            .Root!.Elements("data")
            .ToDictionary(d => (string)d.Attribute("name")!, d => (string?)d.Element("value") ?? "");

    // ── Translations ────────────────────────────────────────────

    [Fact]
    public void Every_menu_label_has_an_Arabic_translation()
    {
        var arabic = Arabic();
        var missing = NavigationCatalogue.AllLabels.Where(label => !arabic.ContainsKey(label));
        Assert.Empty(missing);
    }

    [Fact]
    public void Every_translated_string_in_the_views_has_an_Arabic_translation()
    {
        var arabic = Arabic();
        var views = Directory.GetFiles(Path.Combine(WebProject(), "Views"), "*.cshtml", SearchOption.AllDirectories);
        var keys = views
            .SelectMany(v => Regex.Matches(File.ReadAllText(v), "T\\[\"([^\"]+)\"[\\],]").Select(m => m.Groups[1].Value))
            .Distinct()
            .ToList();

        Assert.Contains("Sign out", keys);
        Assert.Contains("Yes, continue", keys);
        Assert.Contains("Showing {0}–{1} of {2}", keys);
        Assert.Empty(keys.Where(k => !arabic.ContainsKey(k)));
    }

    [Fact]
    public void Every_status_badge_has_an_Arabic_label()
    {
        var arabic = Arabic();
        var missing = SafyaClinic.Web.Components.StatusStyles.Known
            .Select(SafyaClinic.Application.Common.Labels.Humanize)
            .Where(label => !arabic.ContainsKey(label));
        Assert.Empty(missing);
    }

    [Fact]
    public void Component_and_patient_header_words_have_Arabic_translations()
    {
        var arabic = Arabic();
        var words = new[] { "Search", "Clear", "Male", "Female" };
        Assert.Empty(words.Where(w => !arabic.ContainsKey(w)));
    }

    [Fact]
    public void Arabic_text_keeps_the_same_placeholders_as_the_English()
    {
        var wrong = Arabic()
            .Where(pair => !Regex.Matches(pair.Key, @"\{\d+\}").Select(m => m.Value).OrderBy(x => x)
                .SequenceEqual(Regex.Matches(pair.Value, @"\{\d+\}").Select(m => m.Value).OrderBy(x => x)))
            .Select(pair => pair.Key);
        Assert.Empty(wrong);
    }

    [Fact]
    public void Arabic_translations_are_not_blank()
    {
        Assert.Empty(Arabic().Where(pair => string.IsNullOrWhiteSpace(pair.Value)).Select(pair => pair.Key));
    }

    [Fact]
    public void Localizer_reads_Arabic_from_the_resource_files_and_falls_back_to_English()
    {
        var factory = new ResourceManagerStringLocalizerFactory(
            Options.Create(new LocalizationOptions { ResourcesPath = "Resources" }), NullLoggerFactory.Instance);
        var localizer = factory.Create(typeof(SharedResource));
        var original = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentUICulture = new CultureInfo("ar");
            Assert.Equal("المرضى", localizer["Patients"].Value);
            Assert.Equal("Not translated yet", localizer["Not translated yet"].Value);

            CultureInfo.CurrentUICulture = new CultureInfo("en");
            Assert.Equal("Patients", localizer["Patients"].Value);
        }
        finally
        {
            CultureInfo.CurrentUICulture = original;
        }
    }

    // ── Culture settings ────────────────────────────────────────

    [Fact]
    public void English_is_the_default_and_only_the_language_cookie_can_change_it()
    {
        var options = new RequestLocalizationOptions();
        AppCultures.Configure(options);

        Assert.Equal("en", options.DefaultRequestCulture.UICulture.Name);
        Assert.Equal(new[] { "en", "ar" }, options.SupportedUICultures!.Select(c => c.Name));
        Assert.IsType<CookieRequestCultureProvider>(Assert.Single(options.RequestCultureProviders));
    }

    [Fact]
    public void Numbers_and_dates_are_always_formatted_with_Western_digits()
    {
        var options = new RequestLocalizationOptions();
        AppCultures.Configure(options);

        var formatting = Assert.Single(options.SupportedCultures!);
        Assert.Equal("en-US", formatting.Name);
        Assert.Equal("en-US", options.DefaultRequestCulture.Culture.Name);
        Assert.Equal("1,250.50", 1250.5m.ToString("N2", formatting));
    }

    [Fact]
    public void Cookie_keeps_the_formatting_culture_and_changes_only_the_language()
    {
        Assert.Equal("c=en-US|uic=ar", AppCultures.CookieValue("ar"));
        Assert.Equal("c=en-US|uic=en", AppCultures.CookieValue("EN"));
    }

    [Theory]
    [InlineData("/Patients?search=mona", "/Patients?search=mona")]
    [InlineData("/", "/")]
    [InlineData("//evil.example", "/")]
    [InlineData("/\\evil.example", "/")]
    [InlineData("https://evil.example/", "/")]
    [InlineData("", "/")]
    [InlineData(null, "/")]
    public void Return_address_must_be_on_this_site(string? returnUrl, string expected)
    {
        Assert.Equal(expected, AppCultures.SafeReturnUrl(returnUrl));
    }

    // ── Language switch ─────────────────────────────────────────

    [Fact]
    public async Task Switching_to_Arabic_sets_the_culture_cookie_and_returns_to_the_page()
    {
        var controller = PreferredLanguageTests.Controller(new PreferredLanguageTests.RecordingPreferences(), signedInUserId: null);

        var result = await controller.Set("ar", "/Patients?search=mona");

        Assert.Equal("/Patients?search=mona", Assert.IsType<LocalRedirectResult>(result).Url);
        var cookie = controller.Response.Headers.SetCookie.ToString();
        Assert.StartsWith(CookieRequestCultureProvider.DefaultCookieName + "=", cookie);
        Assert.Contains(Uri.EscapeDataString("c=en-US|uic=ar"), cookie);
        Assert.Contains("httponly", cookie, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("fr")]
    [InlineData("")]
    [InlineData(null)]
    public async Task Unsupported_language_is_refused(string? culture)
    {
        var preferences = new PreferredLanguageTests.RecordingPreferences();
        var controller = PreferredLanguageTests.Controller(preferences, signedInUserId: 7);

        Assert.IsType<BadRequestResult>(await controller.Set(culture, "/"));
        Assert.Empty(preferences.Saved);
        Assert.Empty(controller.Response.Headers.SetCookie.ToString());
    }

    [Fact]
    public void The_sign_in_page_has_the_language_switch_too()
    {
        var views = Path.Combine(WebProject(), "Views");
        Assert.Contains("<partial name=\"_LanguageSwitch\" />", File.ReadAllText(Path.Combine(views, "Auth", "Login.cshtml")));
        Assert.Contains("asp-controller=\"Language\" asp-action=\"Set\"",
            File.ReadAllText(Path.Combine(views, "Shared", "_LanguageSwitch.cshtml")));
    }

    // ── Right-to-left layout ────────────────────────────────────

    [Fact]
    public void Layout_sets_language_and_direction_and_loads_the_right_to_left_Bootstrap()
    {
        var layout = File.ReadAllText(Path.Combine(WebProject(), "Views", "Shared", "_Layout.cshtml"));

        Assert.Contains("<html lang=\"@pageLang\" dir=\"@pageDir\">", layout);
        Assert.Contains("bootstrap.rtl.min.css", layout);
        Assert.Contains("<partial name=\"_LanguageSwitch\" />", layout);
        Assert.True(layout.IndexOf("@Url.Content(bootstrapCss)", StringComparison.Ordinal)
                    < layout.IndexOf("css/tokens.css", StringComparison.Ordinal));
    }

    [Fact]
    public void App_styles_use_logical_sides_so_they_mirror_in_Arabic()
    {
        var css = File.ReadAllText(Path.Combine(WebProject(), "wwwroot", "css", "site.css"));
        var physical = Regex.Matches(css, @"(?<![\w-])(left|right|margin-left|margin-right|padding-left|padding-right|border-left|border-right|border-left-color|border-right-color)\s*:")
            .Select(m => m.Value);

        Assert.Empty(physical);
    }
}
