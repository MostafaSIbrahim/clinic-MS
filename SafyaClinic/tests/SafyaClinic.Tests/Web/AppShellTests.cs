using System.Runtime.CompilerServices;
using Xunit;

namespace SafyaClinic.Tests.Web;

/// <summary>F7c: one confirm dialog, toasts and one h1 per page come from the shared layout.</summary>
public class AppShellTests
{
    private static string Views([CallerFilePath] string source = "") =>
        Path.GetFullPath(Path.Combine(Path.GetDirectoryName(source)!, "..", "..", "..", "SafyaClinic.Web", "Views"));

    [Fact]
    public void Views_use_the_shared_confirm_dialog_not_the_browser_confirm()
    {
        var offenders = Directory.EnumerateFiles(Views(), "*.cshtml", SearchOption.AllDirectories)
            .Where(file => File.ReadAllText(file).Contains("confirm(", StringComparison.Ordinal))
            .Select(file => Path.GetRelativePath(Views(), file))
            .ToList();

        Assert.Empty(offenders);
    }

    [Fact]
    public void Layout_provides_toasts_confirm_dialog_and_a_fallback_h1()
    {
        var layout = File.ReadAllText(Path.Combine(Views(), "Shared", "_Layout.cshtml"));

        Assert.Contains("<partial name=\"_Toasts\" />", layout);
        Assert.Contains("<partial name=\"_ConfirmDialog\" />", layout);
        Assert.Contains("<h1 class=\"app-topbar-title", layout);
        Assert.DoesNotContain("TempData[", layout);
    }
}
