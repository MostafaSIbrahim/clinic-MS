using SafyaClinic.Application.Clinical;
using SafyaClinic.Domain.Enums;
using Xunit;

namespace SafyaClinic.Tests.Application;

/// <summary>P9a: the prescription route comes from a list; routes typed before are still shown.</summary>
public class AdministrationRoutesTests
{
    [Fact]
    public void The_list_has_every_route_once()
    {
        Assert.Equal(Enum.GetNames<AdministrationRoute>(), AdministrationRoutes.All.Select(r => r.Value));
    }

    [Theory]
    [InlineData(null, true)]
    [InlineData("", true)]
    [InlineData("Oral", true)]
    [InlineData("Intravenous", true)]
    [InlineData("by mouth", false)]    // free text is no longer accepted for new drugs
    [InlineData("oral", false)]
    public void Only_a_route_from_the_list_or_none_is_accepted(string? value, bool allowed)
    {
        Assert.Equal(allowed, AdministrationRoutes.IsAllowed(value));
    }

    [Theory]
    [InlineData("Intramuscular", "Intramuscular (IM)")]
    [InlineData("Other", "Other route")]
    [InlineData("after meals, by mouth", "after meals, by mouth")]   // typed before P9a: shown as typed
    [InlineData(null, null)]
    public void Stored_routes_are_shown_by_their_label(string? stored, string? label)
    {
        Assert.Equal(label, AdministrationRoutes.Label(stored));
    }
}
