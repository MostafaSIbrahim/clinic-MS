using SafyaClinic.Application.Security;
using SafyaClinic.Domain.Enums;
using Xunit;

namespace SafyaClinic.Tests.Application;

/// <summary>F4a: the catalogue is consistent and the system groups grant exactly today's access.</summary>
public class PermissionCatalogueTests
{
    // Today's access per permission, read from the Program.cs policies, the [Authorize] attributes and
    // the role checks in controllers (F4a guide, appendix). A = Admin, D = Doctor, N = Nutritionist,
    // R = Reception, RA = ReceptionAdmin.
    private static readonly Dictionary<string, string> TodaysAccess = new()
    {
        [Permissions.Patients.View] = "A D N R",
        [Permissions.Patients.Edit] = "A D N R",
        [Permissions.Patients.ViewTimeline] = "A D",
        [Permissions.Patients.EditMedical] = "A D",
        [Permissions.Reservations.View] = "A D N R",
        [Permissions.Reservations.Manage] = "A D N R",
        [Permissions.Reservations.ViewQueue] = "A D R",
        [Permissions.Reservations.ViewAllQueues] = "A R",
        [Permissions.Reservations.CheckIn] = "A R",
        [Permissions.Reservations.StartConsultation] = "A D",
        [Permissions.Reservations.OverridePrice] = "A",
        [Permissions.Reservations.SetCompleted] = "A",
        [Permissions.Reservations.ChangeAnyDoctorStatus] = "A R",
        [Permissions.Records.View] = "A D",
        [Permissions.Records.Write] = "A D",
        [Permissions.Records.Lock] = "A",
        [Permissions.Consultations.ActOnBehalf] = "A",
        [Permissions.Medicines.Search] = "A D N R",
        [Permissions.Analyses.View] = "A D",
        [Permissions.Analyses.Manage] = "A D",
        [Permissions.Attachments.Delete] = "A",
        [Permissions.FollowUps.View] = "A D R",
        [Permissions.FollowUps.Manage] = "A D R",
        [Permissions.FollowUps.ViewAll] = "A R",
        [Permissions.Nutrition.View] = "A N",
        [Permissions.Nutrition.Manage] = "A N",
        [Permissions.Nutrition.ManagePackages] = "A",
        [Permissions.Payments.View] = "A R RA",
        [Permissions.Payments.Collect] = "A R RA",
        [Permissions.Payments.Adjust] = "A R RA",
        [Permissions.Payments.ViewReport] = "A D R RA",
        [Permissions.Payments.ReportAnyDate] = "A D",
        [Permissions.Payments.ReportAllDoctors] = "A R RA",
        [Permissions.Payments.ViewFullDashboard] = "A",
        [Permissions.Payments.Maintenance] = "A",
        [Permissions.Settings.Clinics] = "A",
        [Permissions.Settings.PatientSources] = "A",
        [Permissions.Settings.Lookups] = "A",
        [Permissions.Users.Manage] = "A",
    };

    private static readonly Dictionary<string, string> RoleOfLetter = new()
    {
        ["A"] = SystemGroups.AdminRole,
        ["D"] = SystemGroups.DoctorRole,
        ["N"] = SystemGroups.NutritionistRole,
        ["R"] = SystemGroups.ReceptionRole,
        ["RA"] = SystemGroups.ReceptionAdminRole,
    };

    private static IEnumerable<string> AllCodes => PermissionCatalogue.All.Select(p => p.Code).OrderBy(c => c);

    [Fact]
    public void Codes_are_unique_and_described()
    {
        Assert.Equal(PermissionCatalogue.All.Count, AllCodes.Distinct().Count());
        Assert.All(PermissionCatalogue.All, p =>
        {
            Assert.Matches(@"^[A-Z][A-Za-z]+\.[A-Z][A-Za-z]+$", p.Code);
            Assert.False(string.IsNullOrWhiteSpace(p.Area));
            Assert.False(string.IsNullOrWhiteSpace(p.Description));
        });
    }

    [Fact]
    public void System_groups_only_grant_known_permissions_once()
    {
        var known = AllCodes.ToHashSet();
        Assert.All(SystemGroups.All, g =>
        {
            Assert.All(g.Permissions, code => Assert.Contains(code, known));
            Assert.Equal(g.Permissions.Count, g.Permissions.Distinct().Count());
        });
    }

    [Fact]
    public void Administrators_have_every_permission()
    {
        var admin = SystemGroups.All.Single(g => g.RoleName == SystemGroups.AdminRole);
        Assert.Equal(AllCodes, admin.Permissions.OrderBy(c => c));
    }

    [Fact]
    public void The_access_table_covers_every_permission()
    {
        Assert.Equal(AllCodes, TodaysAccess.Keys.OrderBy(c => c));
    }

    [Fact]
    public void System_groups_grant_exactly_todays_access()
    {
        var mismatches = new List<string>();
        foreach (var (code, letters) in TodaysAccess)
        {
            var expected = letters.Split(' ').Select(l => RoleOfLetter[l]).OrderBy(r => r).ToList();
            var actual = SystemGroups.All.Where(g => g.Permissions.Contains(code)).Select(g => g.RoleName).OrderBy(r => r).ToList();
            if (!expected.SequenceEqual(actual))
                mismatches.Add($"{code}: expected {string.Join(",", expected)} but got {string.Join(",", actual)}");
        }

        Assert.Empty(mismatches);
    }

}