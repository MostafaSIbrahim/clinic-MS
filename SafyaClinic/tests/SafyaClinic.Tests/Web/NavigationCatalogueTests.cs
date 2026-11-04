using System.Reflection;
using Microsoft.AspNetCore.Mvc;
using SafyaClinic.Application.Security;
using SafyaClinic.Tests.Web.Security;
using SafyaClinic.Web.Controllers;
using SafyaClinic.Web.Navigation;
using Xunit;

namespace SafyaClinic.Tests.Web;

/// <summary>F7: the sidebar comes from the navigation catalogue and shows each group exactly its pages.</summary>
public class NavigationCatalogueTests
{
    private static IReadOnlyList<NavSection> MenuFor(string role, string? controller = "Dashboard", string? action = "Index") =>
        NavigationCatalogue.For(RolePermissions.Of(new[] { role }), controller, action);

    private static string Labels(IReadOnlyList<NavSection> menu) =>
        string.Join("|", menu.SelectMany(s => s.Links).Select(l => l.Label));

    [Theory]
    [InlineData(SystemGroups.AdminRole,
        "Dashboard|Patients|Appointment Board|Reservations|Doctor Queues|Follow-ups|My schedule|Doctor schedules|Analysis|Nutrition|Payment Dashboard|Payment Reports|Users|Permission Groups|Lookups|Patient Sources|Clinics")]
    [InlineData(SystemGroups.DoctorRole,
        "Dashboard|Patients|Appointment Board|Reservations|My Queue|Follow-ups|My schedule|Analysis|Payment Reports")]
    [InlineData(SystemGroups.NutritionistRole,
        "Dashboard|Patients|Appointment Board|Reservations|My schedule|Nutrition")]
    [InlineData(SystemGroups.ReceptionRole,
        "Dashboard|Patients|Appointment Board|Reservations|Doctor Queues|Follow-ups|Unpaid Payments|Payment Reports")]
    [InlineData(SystemGroups.ReceptionAdminRole,
        "Dashboard|Doctor schedules|Unpaid Payments|Payment Reports")]
    public void Each_group_sees_its_pages(string role, string expected)
    {
        Assert.Equal(expected, Labels(MenuFor(role)));
    }

    [Fact]
    public void Empty_sections_are_left_out()
    {
        var sections = MenuFor(SystemGroups.ReceptionAdminRole).Select(s => s.Label).ToList();

        Assert.Equal(new string?[] { null, "Front desk", "Billing" }, sections);
    }

    [Theory]
    [InlineData("Reservations", "Queue", "Doctor Queues")]
    [InlineData("Reservations", "Board", "Appointment Board")]
    [InlineData("Reservations", "Edit", "Reservations")]
    [InlineData("Payments", "Report", "Payment Reports")]
    [InlineData("Payments", "Collect", "Payment Dashboard")]
    [InlineData("Nutrition", "EnrollmentDetails", "Nutrition")]
    [InlineData("Schedules", "Mine", "My schedule")]
    [InlineData("Schedules", "Edit", "Doctor schedules")]
    public void Exactly_one_link_is_marked_as_the_current_page(string controller, string action, string expected)
    {
        var active = MenuFor(SystemGroups.AdminRole, controller, action)
            .SelectMany(s => s.Links).Where(l => l.IsActive).Select(l => l.Label).ToList();

        Assert.Equal(new[] { expected }, active);
    }

    [Fact]
    public void Menu_permissions_are_in_the_catalogue()
    {
        var known = PermissionCatalogue.All.Select(p => p.Code).ToHashSet();
        Assert.All(NavigationCatalogue.RelevantPermissions, code => Assert.Contains(code, known));
    }

    [Fact]
    public void Every_link_points_to_an_existing_action()
    {
        var controllers = typeof(BaseController).Assembly.GetTypes()
            .Where(t => typeof(ControllerBase).IsAssignableFrom(t) && !t.IsAbstract)
            .ToDictionary(t => t.Name[..^"Controller".Length]);

        Assert.All(NavigationCatalogue.Destinations, d =>
        {
            Assert.True(controllers.TryGetValue(d.Controller, out var type), $"No controller {d.Controller}");
            Assert.Contains(type!.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly),
                m => m.Name == d.Action);
        });
    }
}
