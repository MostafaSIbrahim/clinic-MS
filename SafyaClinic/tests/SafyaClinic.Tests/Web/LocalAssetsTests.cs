using System.Runtime.CompilerServices;
using Xunit;

namespace SafyaClinic.Tests.Web;

/// <summary>F6: the app works without internet access: no CDN or web-font links, no Font Awesome.</summary>
public class LocalAssetsTests
{
    private static string WebProject([CallerFilePath] string source = "") =>
        Path.GetFullPath(Path.Combine(Path.GetDirectoryName(source)!, "..", "..", "..", "SafyaClinic.Web"));

    private static readonly string[] External =
    {
        "cdn.jsdelivr.net", "cdnjs.cloudflare.com", "unpkg.com", "fonts.googleapis.com", "fonts.gstatic.com", "font-awesome", "fa-solid", "fa-brands"
    };

    [Fact]
    public void Views_styles_and_scripts_use_no_external_assets()
    {
        var web = WebProject();
        var files = Directory.EnumerateFiles(Path.Combine(web, "Views"), "*.cshtml", SearchOption.AllDirectories)
            .Concat(Directory.EnumerateFiles(Path.Combine(web, "wwwroot", "css"), "*.css", SearchOption.AllDirectories))
            .Concat(Directory.EnumerateFiles(Path.Combine(web, "wwwroot", "js"), "*.js", SearchOption.AllDirectories))
            .ToList();

        var offenders = files
            .SelectMany(file => External
                .Where(marker => File.ReadAllText(file).Contains(marker, StringComparison.OrdinalIgnoreCase))
                .Select(marker => $"{Path.GetRelativePath(web, file)}: {marker}"))
            .ToList();

        Assert.NotEmpty(files);
        Assert.Empty(offenders);
    }

    [Theory]
    [InlineData("lib/bootstrap/dist/css/bootstrap.min.css")]
    [InlineData("lib/bootstrap/dist/css/bootstrap.rtl.min.css")]
    [InlineData("lib/bootstrap/dist/css/bootstrap.rtl.min.css")]
    [InlineData("lib/bootstrap/dist/js/bootstrap.bundle.min.js")]
    [InlineData("lib/bootstrap-icons/font/bootstrap-icons.min.css")]
    [InlineData("lib/bootstrap-icons/font/fonts/bootstrap-icons.woff2")]
    [InlineData("lib/jquery/dist/jquery.min.js")]
    [InlineData("lib/jquery-validation/dist/jquery.validate.min.js")]
    [InlineData("lib/jquery-validation-unobtrusive/jquery.validate.unobtrusive.min.js")]
    [InlineData("lib/fonts/inter/inter-latin-wght-normal.woff2")]
    [InlineData("lib/fonts/ibm-plex-sans-arabic/ibm-plex-sans-arabic-arabic-400-normal.woff2")]
    [InlineData("css/fonts.css")]
    public void Local_asset_exists(string path)
    {
        Assert.True(File.Exists(Path.Combine(WebProject(), "wwwroot", path)), path);
    }
}
