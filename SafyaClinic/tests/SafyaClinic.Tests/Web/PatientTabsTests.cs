using SafyaClinic.Application.Security;
using SafyaClinic.Tests.Web.Security;
using SafyaClinic.Web.Models;
using Xunit;

namespace SafyaClinic.Tests.Web;

/// <summary>M1a: which patient workspace tabs a person sees, and which one opens first.</summary>
public class PatientTabsTests
{
    private static Func<string, Task<bool>> As(params string[] roles) => new RolePermissions(roles).HasAsync;

    [Theory]
    [InlineData(SystemGroups.ReceptionRole, "overview|visits|payments")]
    [InlineData(SystemGroups.DoctorRole, "overview|visits|records|lab|timeline")]
    [InlineData(SystemGroups.NutritionistRole, "overview|visits|nutrition")]
    [InlineData(SystemGroups.AdminRole, "overview|visits|records|lab|nutrition|payments|timeline")]
    [InlineData(SystemGroups.ReceptionAdminRole, "overview|visits|payments")]
    public async Task Each_group_sees_the_tabs_it_may_use(string role, string expected)
    {
        var tabs = await PatientTabs.VisibleAsync(As(role));

        Assert.Equal(expected, string.Join("|", tabs.Select(t => t.Key)));
    }

    [Fact]
    public async Task Without_reservation_access_only_the_overview_shows()
    {
        Func<string, Task<bool>> can = code => Task.FromResult(code == Permissions.Patients.View);

        var tabs = await PatientTabs.VisibleAsync(can);

        Assert.Equal(PatientTabs.Overview, Assert.Single(tabs).Key);
        Assert.Equal(PatientTabs.Overview, await PatientTabs.ChooseAsync("visits", tabs, can));
    }

    [Theory]
    [InlineData(SystemGroups.ReceptionRole, null, "visits")]       // the front desk starts on Visits
    [InlineData(SystemGroups.ReceptionAdminRole, null, "visits")]
    [InlineData(SystemGroups.DoctorRole, null, "records")]          // doctors start on Records (M1b)
    [InlineData(SystemGroups.AdminRole, null, "visits")]
    [InlineData(SystemGroups.ReceptionRole, "Overview", "overview")] // a chosen tab wins, in any case
    [InlineData(SystemGroups.DoctorRole, "visits", "visits")]
    [InlineData(SystemGroups.ReceptionRole, "nonsense", "visits")]  // an unknown tab falls back
    [InlineData(SystemGroups.ReceptionRole, "records", "visits")]   // so does a tab the person can't see
    [InlineData(SystemGroups.DoctorRole, "LAB", "lab")]
    [InlineData(SystemGroups.NutritionistRole, null, "nutrition")]    // nutritionists start on Nutrition (M1c)
    [InlineData(SystemGroups.ReceptionRole, "payments", "payments")]
    [InlineData(SystemGroups.DoctorRole, "payments", "records")]      // doctors don't see payments
    [InlineData(SystemGroups.DoctorRole, "timeline", "timeline")]     // M1d
    [InlineData(SystemGroups.ReceptionRole, "timeline", "visits")]
    public async Task The_first_tab_fits_the_work_and_a_chosen_tab_wins(string role, string? requested, string expected)
    {
        var can = As(role);
        var tabs = await PatientTabs.VisibleAsync(can);

        Assert.Equal(expected, await PatientTabs.ChooseAsync(requested, tabs, can));
    }
}
