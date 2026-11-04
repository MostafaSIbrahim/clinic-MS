using SafyaClinic.Application.DTOs.Patient;
using SafyaClinic.Web.Components;
using Xunit;

namespace SafyaClinic.Tests.Web;

/// <summary>F8: patient header content and status badge styles.</summary>
public class ComponentTests
{
    private static PatientDto Patient(string? allergies, string? chronic = null, params PatientPhoneDto[] phones) => new()
    {
        Id = 5,
        FirstName = "Mona",
        LastName = "Adel",
        DateOfBirth = DateTime.Today.AddYears(-40).AddDays(-10),
        Gender = "Female",
        BloodType = "O+",
        Allergies = allergies,
        ChronicDiseases = chronic,
        Phones = phones
    };

    [Fact]
    public void Allergies_are_shown_in_full()
    {
        var header = PatientHeaderModel.From(Patient("  Penicillin, sulfa drugs  "));

        Assert.True(header.HasAllergies);
        Assert.Equal("Penicillin, sulfa drugs", header.Allergies);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Blank_allergies_mean_none_recorded(string? allergies)
    {
        var header = PatientHeaderModel.From(Patient(allergies));

        Assert.False(header.HasAllergies);
        Assert.Null(header.Allergies);
    }

    [Fact]
    public void Header_shows_age_sex_and_the_primary_phone()
    {
        var header = PatientHeaderModel.From(Patient(null, "Diabetes",
            new PatientPhoneDto { PhoneNumber = "01000000002", IsPrimary = false },
            new PatientPhoneDto { PhoneNumber = "01000000001", IsPrimary = true }));

        Assert.Equal("Mona Adel", header.Name);
        Assert.Equal("40 y · Female", header.AgeAndSex);
        Assert.Equal("01000000001", header.Phone);
        Assert.Equal("Diabetes", header.ChronicDiseases);
    }

    [Theory]
    [InlineData("Confirmed", "booked")]
    [InlineData("Waiting", "waiting")]
    [InlineData("InConsultation", "in-consultation")]
    [InlineData("Completed", "completed")]
    [InlineData("Cancelled", "cancelled")]
    [InlineData("NoShow", "no-show")]
    [InlineData("Covered", "paid")]
    [InlineData("Not fully covered", "due")]
    [InlineData("Something else", "neutral")]
    [InlineData(null, "neutral")]
    public void Status_words_map_to_status_styles(string? status, string expected)
    {
        Assert.Equal(expected, StatusStyles.For(status));
    }
}
