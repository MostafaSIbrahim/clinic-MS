using System.Runtime.CompilerServices;
using Xunit;

namespace SafyaClinic.Tests.Web.Security;

/// <summary>F4c: what a user may see or do comes from permissions, never from role names.</summary>
public class NoRoleChecksTests
{
    private static string WebProject([CallerFilePath] string source = "") =>
        Path.GetFullPath(Path.Combine(Path.GetDirectoryName(source)!, "..", "..", "..", "..", "SafyaClinic.Web"));

    [Fact]
    public void Views_and_controllers_do_not_check_roles()
    {
        var web = WebProject();
        var files = Directory.EnumerateFiles(Path.Combine(web, "Views"), "*.cshtml", SearchOption.AllDirectories)
            .Concat(Directory.EnumerateFiles(Path.Combine(web, "Controllers"), "*.cs", SearchOption.AllDirectories))
            .ToList();

        var offenders = files
            .Where(file => File.ReadAllText(file).Contains("IsInRole("))
            .Select(file => Path.GetRelativePath(web, file))
            .ToList();

        Assert.NotEmpty(files);
        Assert.Empty(offenders);
    }
}
