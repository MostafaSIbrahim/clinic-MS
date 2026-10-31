using System.Globalization;
using SafyaClinic.Application.Common;
using Xunit;

namespace SafyaClinic.Tests.Application;

/// <summary>F2 / F13, F14: clinic time, money and labels.</summary>
public class ClinicFormattingTests
{
    private static ClinicClock ClockAt(DateTime utcNow) =>
        new(ClinicClock.FindZone(ClinicClock.DefaultTimeZoneId), () => utcNow);

    [Fact]
    public void Today_is_the_clinic_date_not_the_utc_date()
    {
        // 22:30 UTC on 1 Dec = 00:30 on 2 Dec in Cairo (winter, UTC+2).
        var clock = ClockAt(new DateTime(2026, 12, 1, 22, 30, 0, DateTimeKind.Utc));

        Assert.Equal(new DateTime(2026, 12, 2), clock.Today);
    }

    [Fact]
    public void Winter_day_starts_at_22_00_utc_the_day_before()
    {
        Assert.Equal(new DateTime(2026, 12, 1, 22, 0, 0), ClinicClock.Cairo.DayStartUtc(new DateTime(2026, 12, 2)));
    }

    [Fact]
    public void Summer_day_starts_at_21_00_utc_the_day_before()
    {
        Assert.Equal(new DateTime(2026, 7, 14, 21, 0, 0), ClinicClock.Cairo.DayStartUtc(new DateTime(2026, 7, 15)));
    }

    [Fact]
    public void Stored_utc_times_are_shown_in_clinic_time()
    {
        Assert.Equal(new DateTime(2026, 12, 1, 12, 6, 0), ClinicClock.Cairo.ToClinicTime(new DateTime(2026, 12, 1, 10, 6, 0)));
    }

    [Fact]
    public void Money_uses_egp_and_western_digits_in_any_culture()
    {
        var previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("ar-EG");
            Assert.Equal("1,250.50 EGP", 1250.5m.ToMoney());
            Assert.Equal("0.00 EGP", Money.Format(0m));
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }

    [Theory]
    [InlineData("InternalMedicine", "Internal Medicine")]
    [InlineData("NoShow", "No Show")]
    [InlineData("InConsultation", "In Consultation")]
    [InlineData("", "")]
    public void Code_values_become_readable_labels(string value, string expected)
    {
        Assert.Equal(expected, Labels.Humanize(value));
    }
}