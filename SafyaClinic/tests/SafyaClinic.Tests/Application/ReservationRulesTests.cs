using SafyaClinic.Application.Services;
using Xunit;

namespace SafyaClinic.Tests.Application;

/// <summary>S7: D4 (price, completion) and D6 (other doctors' reservations).</summary>
public class ReservationRulesTests
{
    private const int Me = 10;
    private const int OtherDoctor = 20;

    private static string[] Roles(params string[] roles) => roles;

    [Fact]
    public void Admin_may_set_completed()
    {
        Assert.Null(ReservationRules.CheckStatusChange(Roles("Admin"), Me, OtherDoctor, "Completed"));
    }

    [Fact]
    public void Reception_may_not_set_completed()
    {
        Assert.NotNull(ReservationRules.CheckStatusChange(Roles("Reception"), Me, OtherDoctor, "Completed"));
    }

    [Fact]
    public void Doctor_may_not_set_completed_through_the_status_form()
    {
        Assert.NotNull(ReservationRules.CheckStatusChange(Roles("Doctor"), Me, Me, "Completed"));
    }

    [Fact]
    public void Doctor_may_change_the_status_of_their_own_reservation()
    {
        Assert.Null(ReservationRules.CheckStatusChange(Roles("Doctor"), Me, Me, "Confirmed"));
    }

    [Theory]
    [InlineData("Doctor")]
    [InlineData("Nutritionist")]
    public void Clinician_may_not_change_another_doctors_reservation(string role)
    {
        Assert.NotNull(ReservationRules.CheckStatusChange(Roles(role), Me, OtherDoctor, "Cancelled"));
    }

    [Fact]
    public void Reception_may_change_any_reservation()
    {
        Assert.Null(ReservationRules.CheckStatusChange(Roles("Reception"), Me, OtherDoctor, "Cancelled"));
    }

    [Fact]
    public void Admin_who_is_also_a_doctor_may_change_any_reservation()
    {
        Assert.Null(ReservationRules.CheckStatusChange(Roles("Admin", "Doctor"), Me, OtherDoctor, "NoShow"));
    }

    [Theory]
    [InlineData("Admin", true)]
    [InlineData("Reception", false)]
    [InlineData("Doctor", false)]
    public void Only_admin_may_override_the_price(string role, bool expected)
    {
        Assert.Equal(expected, ReservationRules.CanOverridePrice(Roles(role)));
    }
}