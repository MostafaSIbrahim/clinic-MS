using System.Runtime.CompilerServices;
using System.Xml.Linq;
using SafyaClinic.Application.Security;
using Xunit;

namespace SafyaClinic.Tests.Web;

/// <summary>M5b: the permission group screens show every permission, area and built-in group in Arabic too.</summary>
public class PermissionTranslationTests
{
    private static HashSet<string> ArabicKeys([CallerFilePath] string source = "") =>
        XDocument.Load(Path.GetFullPath(Path.Combine(Path.GetDirectoryName(source)!, "..", "..", "..",
                "SafyaClinic.Web", "Resources", "SharedResource.ar.resx")))
            .Root!.Elements("data").Select(d => (string)d.Attribute("name")!).ToHashSet();

    [Fact]
    public void Every_permission_and_area_has_an_Arabic_translation()
    {
        var arabic = ArabicKeys();
        var missing = PermissionCatalogue.All.SelectMany(p => new[] { p.Area, p.Description })
            .Distinct().Where(text => !arabic.Contains(text));
        Assert.Empty(missing);
    }

    [Fact]
    public void Every_built_in_group_name_and_description_has_an_Arabic_translation()
    {
        var arabic = ArabicKeys();
        var missing = SystemGroups.All.SelectMany(g => new[] { g.Name, g.Description }).Where(text => !arabic.Contains(text));
        Assert.Empty(missing);
    }
}
