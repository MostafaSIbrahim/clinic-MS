using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using Xunit;

namespace SafyaClinic.Tests.Web;

/// <summary>F6: colours come from the design tokens; the app's tokens file is the approved specification.</summary>
public class DesignTokensTests
{
    private static string Here([CallerFilePath] string source = "") => Path.GetDirectoryName(source)!;

    private static string WebRoot => Path.GetFullPath(Path.Combine(Here(), "..", "..", "..", "SafyaClinic.Web", "wwwroot"));
    private static string RepoRoot => Path.GetFullPath(Path.Combine(Here(), "..", "..", "..", ".."));

    private static string Read(string path) => File.ReadAllText(path).Replace("\r\n", "\n");

    [Fact]
    public void App_tokens_match_the_design_specification()
    {
        Assert.Equal(
            Read(Path.Combine(RepoRoot, "Docs", "design", "tokens.css")),
            Read(Path.Combine(WebRoot, "css", "tokens.css")));
    }

    [Theory]
    [InlineData("css/site.css")]
    [InlineData("css/branding.css")]
    [InlineData("css/bootstrap-bridge.css")]
    public void Shared_styles_use_tokens_not_colour_values(string file)
    {
        var css = Read(Path.Combine(WebRoot, file));
        var colours = Regex.Matches(css, @"#[0-9a-fA-F]{3,8}\b|rgba?\(").Select(m => m.Value).ToList();

        Assert.Empty(colours);
    }

    [Fact]
    public void Layout_loads_tokens_then_bootstrap_bridge_then_app_styles()
    {
        var layout = Read(Path.GetFullPath(Path.Combine(WebRoot, "..", "Views", "Shared", "_Layout.cshtml")));
        int At(string asset) => layout.IndexOf(asset, StringComparison.Ordinal);

        Assert.True(At("bootstrap.min.css") >= 0 && At("css/tokens.css") > 0 && At("bootstrap-bridge.css") > 0);
        Assert.True(At("bootstrap.min.css") < At("bootstrap-bridge.css"));
        Assert.True(At("css/tokens.css") < At("css/site.css"));
        Assert.True(At("bootstrap-bridge.css") < At("css/site.css"));
    }
}
