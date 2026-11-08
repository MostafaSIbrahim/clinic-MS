using SafyaClinic.Web.Models;
using Xunit;

namespace SafyaClinic.Tests.Web;

/// <summary>P5b: the consultation workspace opens on the requested tab, or on Notes.</summary>
public class VisitTabsTests
{
    [Theory]
    [InlineData("treatments", "treatments")]
    [InlineData("Prescriptions", "prescriptions")]
    [InlineData(" LAB ", "lab")]
    [InlineData("notes", "notes")]
    [InlineData(null, "notes")]
    [InlineData("", "notes")]
    [InlineData("billing", "notes")]
    [InlineData("<script>", "notes")]
    public void Unknown_or_missing_tabs_open_Notes(string? requested, string expected) =>
        Assert.Equal(expected, VisitTabs.Normalize(requested));

    [Fact]
    public void The_workspace_has_four_tabs_in_order() =>
        Assert.Equal(new[] { "notes", "treatments", "prescriptions", "lab" }, VisitTabs.All);
}
