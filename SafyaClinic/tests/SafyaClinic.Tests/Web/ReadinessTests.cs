using SafyaClinic.Web.Startup;
using Xunit;

namespace SafyaClinic.Tests.Web;

/// <summary>D2a: the readiness page says what must be fixed before real patients are entered.</summary>
public class ReadinessTests
{
    private static ReadinessFacts Ready() => new(
        Environment: "Production", IsHttps: true, LastMigration: "20261109_WriteOffReasons", PendingMigrations: 0,
        KeysFolderWritable: true, KeyFiles: 1, UploadsWritable: true, TodaysLogExists: true,
        SeedPasswordConfigured: false, ClinicName: "Safya Clinic", ClinicAddress: "14 Abbas El Akkad, Nasr City",
        ClinicPhone: "+20 2 2401 0000", ClinicEmail: "info@safya.clinic",
        PricedVisitTypes: 6, ActiveAdmins: 1, TimeZone: "Africa/Cairo", ClinicNow: new DateTime(2026, 11, 9, 23, 0, 0));

    private static ReadinessLevel LevelOf(ReadinessFacts facts, string name) =>
        Readiness.Evaluate(facts).Single(i => i.Name == name).Level;

    [Fact]
    public void A_correctly_set_up_site_passes_every_check() =>
        Assert.All(Readiness.Evaluate(Ready()), i => Assert.Equal(ReadinessLevel.Ok, i.Level));

    [Fact]
    public void Missing_https_pending_migrations_and_unwritable_folders_are_problems()
    {
        Assert.Equal(ReadinessLevel.Problem, LevelOf(Ready() with { IsHttps = false }, "Encrypted connection (HTTPS)"));
        Assert.Equal(ReadinessLevel.Problem, LevelOf(Ready() with { PendingMigrations = 2 }, "Database"));
        Assert.Equal(ReadinessLevel.Problem, LevelOf(Ready() with { KeysFolderWritable = false }, "Sign-in keys"));
        Assert.Equal(ReadinessLevel.Problem, LevelOf(Ready() with { UploadsWritable = false }, "Attachments folder"));
        Assert.Equal(ReadinessLevel.Problem, LevelOf(Ready() with { PricedVisitTypes = 0 }, "Visit types"));
        Assert.Equal(ReadinessLevel.Problem, LevelOf(Ready() with { ActiveAdmins = 0 }, "Administrators"));
    }

    [Fact]
    public void Leftover_setup_values_are_flagged_for_review()
    {
        Assert.Equal(ReadinessLevel.Warning, LevelOf(Ready() with { SeedPasswordConfigured = true }, "First admin password setting"));
        Assert.Equal(ReadinessLevel.Warning, LevelOf(Ready() with { Environment = "Development" }, "Environment"));
        Assert.Equal(ReadinessLevel.Warning, LevelOf(Ready() with { ActiveAdmins = 5 }, "Administrators"));
    }

    [Theory]
    [InlineData("123 Corniche St, Cairo, Egypt", "info@safya.clinic")]   // the example address shipped in appsettings.json
    [InlineData("14 Abbas El Akkad, Nasr City", "info@safyaclinic.example")]
    [InlineData("", "info@safya.clinic")]
    public void Example_or_missing_clinic_details_are_flagged(string address, string email) =>
        Assert.Equal(ReadinessLevel.Warning, LevelOf(Ready() with { ClinicAddress = address, ClinicEmail = email }, "Clinic details"));
}
